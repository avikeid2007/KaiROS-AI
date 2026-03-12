using KaiROS.AI.Uno.Models;
using System.Text.Json;
using System.Diagnostics;

namespace KaiROS.AI.Uno.Services;

public class ModelManagerService : IModelManagerService
{
    private readonly IDownloadService _downloadService;
    private readonly IDatabaseService _databaseService;
    private readonly IHardwareDetectionService _hardwareService;

    private List<LLMModelInfo> _models = [];
    private LLMModelInfo? _activeModel;
    private bool _isInitialized;

    public IReadOnlyList<LLMModelInfo> Models => _models;
    public LLMModelInfo? ActiveModel => _activeModel;
    public string ModelsDirectory { get; private set; } = string.Empty;
    public bool IsVisionModelLoaded => _activeModel?.IsVisionModel ?? false;

    // Native backend only available on desktop
    public bool IsNativeBackendAvailable =>
#if DESKTOP
        true;
#else
        false;
#endif

    public event EventHandler<LLMModelInfo>? ModelDownloadStarted;
    public event EventHandler<LLMModelInfo>? ModelDownloadCompleted;
    public event EventHandler<LLMModelInfo>? ModelLoaded;
    public event EventHandler? ModelUnloaded;
    public event EventHandler<double>? ModelLoadProgress;

    public ModelManagerService(
        IDownloadService downloadService,
        IDatabaseService databaseService,
        IHardwareDetectionService hardwareService)
    {
        _downloadService = downloadService;
        _databaseService = databaseService;
        _hardwareService = hardwareService;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        _models.Clear();

        ModelsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KaiROS", "Models");

#if DESKTOP
        if (!Directory.Exists(ModelsDirectory))
        {
            Directory.CreateDirectory(ModelsDirectory);
        }
#endif

        // Load models from configuration
        await LoadModelsFromConfigAsync();

        // Load custom models from database (desktop only)
#if DESKTOP
        try
        {
            var customModels = await _databaseService.GetCustomModelsAsync();
            foreach (var custom in customModels)
            {
                _models.Add(new LLMModelInfo
                {
                    Name = custom.Name,
                    DisplayName = custom.DisplayName,
                    Description = custom.Description,
                    SizeBytes = custom.SizeBytes,
                    LocalPath = custom.FilePath,
                    DownloadUrl = custom.DownloadUrl,
                    IsDownloaded = File.Exists(custom.FilePath),
                    IsCustomModel = true,
                    CustomModelId = custom.Id,
                    IsVisionModel = custom.IsVisionModel
                });
            }
        }
        catch
        {
            // Database might not be initialized yet
        }

        // Check which models are downloaded
        foreach (var model in _models)
        {
            if (string.IsNullOrEmpty(model.LocalPath))
            {
                model.LocalPath = Path.Combine(ModelsDirectory, $"{model.Name}.gguf");
            }
            model.IsDownloaded = File.Exists(model.LocalPath);
        }
#endif

        _isInitialized = true;
    }

    private async Task LoadModelsFromConfigAsync()
    {
        bool loadedFromConfig = false;

        try
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (File.Exists(configPath))
            {
                var json = await File.ReadAllTextAsync(configPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (config?.Models != null && config.Models.Count > 0)
                {
                    foreach (var model in config.Models)
                    {
                        _models.Add(model);
                    }
                    loadedFromConfig = true;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load models from config: {ex.Message}");
        }

        // Add demo models if no models were loaded
        if (!loadedFromConfig || _models.Count == 0)
        {
            AddDemoModels();
        }
    }

    private void AddDemoModels()
    {
        // Demo models with real Hugging Face download URLs (Q4_K_M quantization)
        _models.Add(new LLMModelInfo
        {
            Name = "llama-3.2-3b",
            DisplayName = "Llama 3.2 3B",
            Description = "Meta's Llama 3.2 3B - Great for general tasks",
            SizeText = "2.0 GB",
            SizeBytes = 2_000_000_000,
            DownloadUrl = "https://huggingface.co/bartowski/Llama-3.2-3B-Instruct-GGUF/resolve/main/Llama-3.2-3B-Instruct-Q4_K_M.gguf",
            Organization = "Meta",
            Family = "Llama",
            IsRecommended = true,
            IsDownloaded = false,
            MinRam = "8 GB",
            Category = "General"
        });

        _models.Add(new LLMModelInfo
        {
            Name = "phi-3-mini",
            DisplayName = "Phi-3 Mini 4K",
            Description = "Microsoft's Phi-3 Mini - Compact and efficient",
            SizeText = "2.4 GB",
            SizeBytes = 2_400_000_000,
            DownloadUrl = "https://huggingface.co/maziyarpanahi/Phi-3-mini-4k-instruct-GGUF/resolve/main/Phi-3-mini-4k-instruct-Q4_K_M.gguf",
            Organization = "Microsoft",
            Family = "Phi",
            IsRecommended = true,
            IsDownloaded = false,
            MinRam = "6 GB",
            Category = "General"
        });

        _models.Add(new LLMModelInfo
        {
            Name = "mistral-7b",
            DisplayName = "Mistral 7B v0.3",
            Description = "Mistral AI's 7B model - Excellent performance",
            SizeText = "4.1 GB",
            SizeBytes = 4_100_000_000,
            DownloadUrl = "https://huggingface.co/bartowski/Mistral-7B-Instruct-v0.3-GGUF/resolve/main/Mistral-7B-Instruct-v0.3-Q4_K_M.gguf",
            Organization = "Mistral AI",
            Family = "Mistral",
            IsRecommended = false,
            IsDownloaded = false,
            MinRam = "16 GB",
            Category = "General"
        });

        _models.Add(new LLMModelInfo
        {
            Name = "gemma-2-9b",
            DisplayName = "Gemma 2 9B",
            Description = "Google's Gemma 2 9B - High quality responses",
            SizeText = "5.4 GB",
            SizeBytes = 5_400_000_000,
            DownloadUrl = "https://huggingface.co/bartowski/gemma-2-9b-it-GGUF/resolve/main/gemma-2-9b-it-Q4_K_M.gguf",
            Organization = "Google",
            Family = "Gemma",
            IsRecommended = false,
            IsDownloaded = false,
            MinRam = "16 GB",
            Category = "General"
        });

        _models.Add(new LLMModelInfo
        {
            Name = "qwen2.5-7b",
            DisplayName = "Qwen 2.5 7B",
            Description = "Alibaba's Qwen 2.5 - Multilingual capabilities",
            SizeText = "4.3 GB",
            SizeBytes = 4_300_000_000,
            DownloadUrl = "https://huggingface.co/bartowski/Qwen2.5-7B-Instruct-GGUF/resolve/main/Qwen2.5-7B-Instruct-Q4_K_M.gguf",
            Organization = "Alibaba",
            Family = "Qwen",
            IsRecommended = false,
            IsDownloaded = false,
            MinRam = "16 GB",
            Category = "General"
        });
    }

    public async Task<bool> DownloadModelAsync(LLMModelInfo model, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!IsNativeBackendAvailable)
        {
            model.LoadError = "Model downloads are only available on desktop platforms";
            return false;
        }

        if (string.IsNullOrEmpty(model.DownloadUrl))
        {
            model.LoadError = "No download URL available for this model";
            return false;
        }

        ModelDownloadStarted?.Invoke(this, model);

        var localPath = Path.Combine(ModelsDirectory, $"{model.Name}.gguf");
        var success = await _downloadService.DownloadFileAsync(model.DownloadUrl, localPath, progress, cancellationToken);

        if (success)
        {
            model.LocalPath = localPath;
            model.IsDownloaded = true;
            model.DownloadProgress = 100;
            model.LoadError = null;
            ModelDownloadCompleted?.Invoke(this, model);
        }
        else
        {
            model.LoadError ??= "Download failed. Please check your internet connection and try again.";
        }

        return success;
    }

    public Task PauseDownloadAsync(LLMModelInfo model)
    {
        return _downloadService.PauseDownloadAsync(model.Name);
    }

    public Task ResumeDownloadAsync(LLMModelInfo model)
    {
        return _downloadService.ResumeDownloadAsync(model.Name);
    }

    public async Task<bool> DeleteModelAsync(LLMModelInfo model)
    {
        if (!IsNativeBackendAvailable)
            return false;

        if (!string.IsNullOrEmpty(model.LocalPath) && File.Exists(model.LocalPath))
        {
            File.Delete(model.LocalPath);
        }

        model.IsDownloaded = false;
        model.LocalPath = null;

        if (model.IsActive)
        {
            await UnloadModelAsync();
        }

        return true;
    }

    public async Task<bool> SetActiveModelAsync(LLMModelInfo model, IProgress<double>? progress = null)
    {
        Debug.WriteLine($"SetActiveModelAsync: model={model.Name}");
        Debug.WriteLine($"  IsNativeBackendAvailable={IsNativeBackendAvailable}");
        Debug.WriteLine($"  IsDownloaded={model.IsDownloaded}");
        Debug.WriteLine($"  LocalPath={model.LocalPath ?? "null"}");

        if (!IsNativeBackendAvailable)
        {
            model.LoadError = "Model loading is only available on desktop platforms";
            Debug.WriteLine("  Failed: Not native backend");
            return false;
        }

        if (!model.IsDownloaded || string.IsNullOrEmpty(model.LocalPath))
        {
            model.LoadError = "Model not downloaded";
            Debug.WriteLine("  Failed: Model not downloaded or no local path");
            return false;
        }

#if DESKTOP
        if (!File.Exists(model.LocalPath))
        {
            model.LoadError = $"Model file not found at {model.LocalPath}";
            Debug.WriteLine($"  Failed: File not found at {model.LocalPath}");
            return false;
        }
#endif

        try
        {
            await UnloadModelAsync();

            progress?.Report(10);
            ModelLoadProgress?.Invoke(this, 10);

            // Desktop would load actual LLamaSharp model here
            await Task.Delay(100);

            progress?.Report(50);
            ModelLoadProgress?.Invoke(this, 50);

            await Task.Delay(100);

            progress?.Report(100);
            ModelLoadProgress?.Invoke(this, 100);

            _activeModel = model;
            model.IsActive = true;

            Debug.WriteLine($"  Success: ActiveModel set to {model.Name}");
            ModelLoaded?.Invoke(this, model);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"  Exception: {ex}");
            model.LoadError = ex.Message;
            return false;
        }
    }

    public Task UnloadModelAsync()
    {
        if (_activeModel != null)
        {
            _activeModel.IsActive = false;
            _activeModel = null;
            ModelUnloaded?.Invoke(this, EventArgs.Empty);
        }

        return Task.CompletedTask;
    }

    public Task<bool> VerifyModelAsync(LLMModelInfo model)
    {
        if (!IsNativeBackendAvailable)
            return Task.FromResult(false);

        if (string.IsNullOrEmpty(model.LocalPath) || !File.Exists(model.LocalPath))
            return Task.FromResult(false);

        return _downloadService.VerifyFileIntegrityAsync(model.LocalPath, model.SizeBytes);
    }

    public void SetModelsDirectory(string path)
    {
        ModelsDirectory = path;
    }

    private class AppConfig
    {
        public List<LLMModelInfo>? Models { get; set; }
    }
}
