using System.Diagnostics;

namespace KaiROS.AI.Uno.Services;

public class DownloadService : IDownloadService
{
    private readonly HttpClient _httpClient;

    public DownloadService()
    {
        _httpClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10
        });
        
        // Set timeout for large file downloads
        _httpClient.Timeout = TimeSpan.FromHours(2);
    }

    public async Task<bool> DownloadFileAsync(string url, string destinationPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
#if DESKTOP
        string? errorMessage = null;
        try
        {
            if (string.IsNullOrEmpty(url))
            {
                Debug.WriteLine("Download failed: URL is null or empty");
                return false;
            }

            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            Debug.WriteLine($"Starting download from: {url}");
            Debug.WriteLine($"Destination: {destinationPath}");

            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                errorMessage = $"HTTP error: {(int)response.StatusCode} {response.ReasonPhrase}";
                Debug.WriteLine($"Download failed: {errorMessage}");
                return false;
            }

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = totalBytes != -1 && progress != null;
            
            Debug.WriteLine($"Content length: {totalBytes} bytes");

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            var totalBytesRead = 0L;
            int bytesRead;
            var lastProgressReport = DateTime.MinValue;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalBytesRead += bytesRead;

                // Report progress at most every 500ms to avoid UI flooding
                if (canReportProgress && (DateTime.Now - lastProgressReport).TotalMilliseconds > 500)
                {
                    var percentComplete = (double)totalBytesRead / totalBytes * 100;
                    progress?.Report(percentComplete);
                    lastProgressReport = DateTime.Now;
                }
            }

            // Final progress report
            if (canReportProgress)
            {
                progress?.Report(100);
            }

            Debug.WriteLine($"Download completed: {totalBytesRead} bytes");
            return true;
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("Download was cancelled");
            // Clean up partial file
            if (File.Exists(destinationPath))
            {
                try { File.Delete(destinationPath); } catch { }
            }
            throw;
        }
        catch (HttpRequestException ex)
        {
            errorMessage = $"Network error: {ex.Message}";
            Debug.WriteLine($"Download failed: {errorMessage}");
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = $"Download error: {ex.Message}";
            Debug.WriteLine($"Download failed: {errorMessage}");
            return false;
        }
#else
        // WASM cannot download files to local filesystem
        Debug.WriteLine("Download not supported on WASM platform");
        return false;
#endif
    }

    public Task PauseDownloadAsync(string modelName)
    {
        // Would implement pause logic with partial file saving
        return Task.CompletedTask;
    }

    public Task ResumeDownloadAsync(string modelName)
    {
        // Would implement resume logic
        return Task.CompletedTask;
    }

    public async Task<bool> VerifyFileIntegrityAsync(string filePath, long expectedSize)
    {
#if DESKTOP
        if (!File.Exists(filePath))
            return false;

        var fileInfo = new FileInfo(filePath);
        return fileInfo.Length == expectedSize;
#else
        return false;
#endif
    }

    public bool HasPartialDownload(string modelName)
    {
        // Would check for partial download files
        return false;
    }
}
