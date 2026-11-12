using KaiROS.Mobile.Models;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace KaiROS.Mobile.Services;

/// <summary>
/// Service for downloading and managing GGUF models.
/// Downloads resume from a partial file and retry on transient network failures.
/// </summary>
public class ModelDownloadService
{
    public const string HttpClientName = "ModelDownloads";

    private const int MaxAttempts = 5;
    private const string PartialExtension = ".part";
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(30);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ModelDownloadService> _logger;
    private readonly string _modelsDirectory;
    private CancellationTokenSource? _currentDownloadCts;

    public event EventHandler<double>? ProgressChanged;
    public event EventHandler<string>? StatusChanged;

    public ModelDownloadService(IHttpClientFactory httpClientFactory, ILogger<ModelDownloadService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        // Store models in app's local data directory
        _modelsDirectory = Path.Combine(
            FileSystem.Current.AppDataDirectory,
            "Models");

        Directory.CreateDirectory(_modelsDirectory);
    }

    /// <summary>
    /// Get the list of available models from the catalog.
    /// </summary>
    public async Task<List<LLMModelInfo>> GetModelCatalogAsync()
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("model_catalog.json");
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            var models = JsonSerializer.Deserialize<List<LLMModelInfo>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (models != null)
            {
                // Check which models are already downloaded
                foreach (var model in models)
                {
                    var localPath = Path.Combine(_modelsDirectory, model.FileName);
                    if (File.Exists(localPath))
                    {
                        model.LocalPath = localPath;
                    }
                }
            }

            return models ?? new List<LLMModelInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading model catalog");
            return GetDefaultModels();
        }
    }

    /// <summary>
    /// Download a model from the specified URL with progress tracking.
    /// </summary>
    public async Task<string?> DownloadModelAsync(
        LLMModelInfo model,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(model.DownloadUrl))
        {
            StatusChanged?.Invoke(this, "Invalid download URL");
            return null;
        }

        var destinationPath = Path.Combine(_modelsDirectory, model.FileName);

        // Check if already downloaded
        if (File.Exists(destinationPath))
        {
            model.LocalPath = destinationPath;
            StatusChanged?.Invoke(this, "Model already downloaded");
            return destinationPath;
        }

        var partialPath = destinationPath + PartialExtension;

        try
        {
            _currentDownloadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _currentDownloadCts.Token;

            StatusChanged?.Invoke(this, $"Downloading {model.Name}...");
            model.IsDownloading = true;
            model.DownloadProgress = 0;

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await DownloadAttemptAsync(model, partialPath, token);
                    break;
                }
                catch (Exception ex) when (attempt < MaxAttempts && IsTransient(ex, token))
                {
                    _logger.LogWarning(ex, "Download attempt {Attempt}/{Max} for {Model} failed", attempt, MaxAttempts, model.Name);
                    StatusChanged?.Invoke(this, $"Connection lost. Retrying ({attempt}/{MaxAttempts - 1})...");
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt))), token);
                }
            }

            // Final update
            MainThread.BeginInvokeOnMainThread(() => model.DownloadProgress = 1.0);

            File.Move(partialPath, destinationPath, overwrite: true);

            model.LocalPath = destinationPath;
            model.IsDownloading = false;
            model.DownloadProgress = 1.0;

            StatusChanged?.Invoke(this, $"Download complete: {model.Name}");
            ProgressChanged?.Invoke(this, 1.0);

            return destinationPath;
        }
        catch (OperationCanceledException)
        {
            // The partial file is kept so the download can resume later
            StatusChanged?.Invoke(this, "Download paused");
            model.IsDownloading = false;
            model.DownloadProgress = 0;
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Download of {Model} failed", model.Name);
            StatusChanged?.Invoke(this, $"Download failed: {ex.Message}");
            model.IsDownloading = false;
            model.DownloadProgress = 0;
            return null;
        }
    }

    /// <summary>
    /// Performs one download attempt, resuming from the partial file when the server supports ranges.
    /// </summary>
    private async Task DownloadAttemptAsync(LLMModelInfo model, string partialPath, CancellationToken token)
    {
        using var stallCts = CancellationTokenSource.CreateLinkedTokenSource(token);

        try
        {
            var existingBytes = File.Exists(partialPath) ? new FileInfo(partialPath).Length : 0;

            using var request = new HttpRequestMessage(HttpMethod.Get, model.DownloadUrl);
            if (existingBytes > 0)
                request.Headers.Range = new RangeHeaderValue(existingBytes, null);

            var client = _httpClientFactory.CreateClient(HttpClientName);

            stallCts.CancelAfter(StallTimeout);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, stallCts.Token);

            if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                // The partial file is already complete, or no longer matches the remote file
                if (response.Content.Headers.ContentRange?.Length == existingBytes)
                    return;

                File.Delete(partialPath);
                throw new IOException("Partial download no longer valid; restarting.");
            }

            response.EnsureSuccessStatusCode();

            var resumed = response.StatusCode == HttpStatusCode.PartialContent;
            if (!resumed)
                existingBytes = 0; // Server ignored the range request, start over

            var contentLength = response.Content.Headers.ContentLength;
            long? expectedTotal = resumed
                ? response.Content.Headers.ContentRange?.Length ?? (contentLength.HasValue ? existingBytes + contentLength.Value : null)
                : contentLength;

            var progressTotal = expectedTotal ?? model.SizeBytes;

            await using var contentStream = await response.Content.ReadAsStreamAsync(stallCts.Token);
            await using var fileStream = new FileStream(
                partialPath,
                resumed ? FileMode.Append : FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                65536,
                true);

            var buffer = new byte[81920]; // 80KB buffer
            var totalBytesRead = existingBytes;
            var lastProgressUpdate = DateTime.Now;

            while (true)
            {
                stallCts.CancelAfter(StallTimeout);
                var bytesRead = await contentStream.ReadAsync(buffer, stallCts.Token);
                if (bytesRead == 0)
                    break;

                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), stallCts.Token);
                totalBytesRead += bytesRead;

                // Update progress every 250ms
                if (progressTotal > 0 && (DateTime.Now - lastProgressUpdate).TotalMilliseconds > 250)
                {
                    var progress = Math.Min(1.0, (double)totalBytesRead / progressTotal);

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        model.DownloadProgress = progress;
                    });

                    ProgressChanged?.Invoke(this, progress);
                    lastProgressUpdate = DateTime.Now;
                }
            }

            await fileStream.FlushAsync(token);

            if (expectedTotal.HasValue && totalBytesRead != expectedTotal.Value)
                throw new IOException($"Download incomplete: {totalBytesRead} of {expectedTotal.Value} bytes.");
        }
        catch (OperationCanceledException ex) when (!token.IsCancellationRequested)
        {
            throw new TimeoutException("Download stalled.", ex);
        }
    }

    private static bool IsTransient(Exception ex, CancellationToken token)
    {
        if (token.IsCancellationRequested)
            return false;

        return ex switch
        {
            HttpRequestException http when http.StatusCode is { } status =>
                (int)status >= 500 || status == HttpStatusCode.RequestTimeout || status == HttpStatusCode.TooManyRequests,
            HttpRequestException => true,
            IOException => true,
            TimeoutException => true,
            _ => false
        };
    }

    /// <summary>
    /// Cancel the current download.
    /// </summary>
    public void CancelDownload()
    {
        _currentDownloadCts?.Cancel();
    }

    /// <summary>
    /// Delete a downloaded model.
    /// </summary>
    public bool DeleteModel(LLMModelInfo model)
    {
        if (string.IsNullOrEmpty(model.LocalPath) || !File.Exists(model.LocalPath))
            return false;

        try
        {
            File.Delete(model.LocalPath);
            model.LocalPath = null;
            StatusChanged?.Invoke(this, $"Deleted: {model.Name}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting model {Model}", model.Name);
            return false;
        }
    }

    /// <summary>
    /// Get list of downloaded models.
    /// </summary>
    public List<string> GetDownloadedModels()
    {
        if (!Directory.Exists(_modelsDirectory))
            return new List<string>();

        return Directory.GetFiles(_modelsDirectory, "*.gguf").ToList();
    }

    private static List<LLMModelInfo> GetDefaultModels()
    {
        return new List<LLMModelInfo>
        {
            new()
            {
                Name = "TinyLlama 1.1B Chat Q4",
                FileName = "tinyllama-1.1b-chat-v1.0.Q4_K_M.gguf",
                Description = "Fast, compact model ideal for mobile devices",
                SizeBytes = 669_000_000,
                DownloadUrl = "https://huggingface.co/TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF/resolve/main/tinyllama-1.1b-chat-v1.0.Q4_K_M.gguf",
                MinRamGB = 2
            },
            new()
            {
                Name = "Qwen2 0.5B Instruct Q4",
                FileName = "qwen2-0_5b-instruct-q4_k_m.gguf",
                Description = "Ultra-compact model for basic tasks",
                SizeBytes = 400_000_000,
                DownloadUrl = "https://huggingface.co/Qwen/Qwen2-0.5B-Instruct-GGUF/resolve/main/qwen2-0_5b-instruct-q4_k_m.gguf",
                MinRamGB = 2
            }
        };
    }
}
