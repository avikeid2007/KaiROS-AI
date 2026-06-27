using KaiROS.AI.WinUI.Models;
using Microsoft.Extensions.Configuration;
using LLama;
using LLama.Common;
using LLama.Native;
using System.IO;
using System.Runtime.InteropServices;
using System.Net.Http;
using System.Text.Json;

namespace KaiROS.AI.WinUI.Services;

public class ModelManagerService : IModelManagerService, IDisposable
{
    private static bool _nativeLibConfigured;

    private readonly IDownloadService _downloadService;
    private readonly IConfiguration _configuration;
    private readonly IDatabaseService _databaseService;
    private readonly IHardwareDetectionService _hardwareService;
    private readonly List<LLMModelInfo> _models = [];
    private string _modelsDirectory;
    private LLamaWeights? _loadedWeights;
    private MtmdWeights? _loadedLlavaWeights;
    private LLMModelInfo? _activeModel;
    private int _currentGpuLayers;

    public IReadOnlyList<LLMModelInfo> Models => _models.AsReadOnly();
    public LLMModelInfo? ActiveModel => _activeModel;
    public string ModelsDirectory => _modelsDirectory;
    public int CurrentGpuLayers => _currentGpuLayers;
    public bool IsVisionModelLoaded => _loadedLlavaWeights is not null;

    public event EventHandler<LLMModelInfo>? ModelDownloadStarted;
    public event EventHandler<LLMModelInfo>? ModelDownloadCompleted;
    public event EventHandler<LLMModelInfo>? ModelLoaded;
    public event EventHandler? ModelUnloaded;
    public event EventHandler<double>? ModelLoadProgress;

    private string _lastModelSettingsPath;
    private readonly string _cachedCatalogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KaiROS.AI", "models_catalog.json");
    private const string RemoteCatalogUrl = "https://raw.githubusercontent.com/avikeid2007/Kairos.local/main/models.json";

    public ModelManagerService(IConfiguration configuration, IDownloadService downloadService, IDatabaseService databaseService, IHardwareDetectionService hardwareService)
    {
        _configuration = configuration;
        _downloadService = downloadService;
        _databaseService = databaseService;
        _hardwareService = hardwareService;

        // Use LocalAppData for MSIX compatibility (installation folder is read-only)
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _modelsDirectory = Path.Combine(localAppData, "KaiROS.AI", "Models");
        _lastModelSettingsPath = Path.Combine(localAppData, "KaiROS.AI", "last_model.txt");
        Directory.CreateDirectory(_modelsDirectory);
    }

    public async Task InitializeAsync()
    {
        // Initialize database
        await _databaseService.InitializeAsync();

        // Load model catalog dynamically from remote repository, with local caching and appsettings fallback
        var modelConfigs = await LoadModelCatalogAsync();

        _models.Clear();
        foreach (var model in modelConfigs)
        {
            // Check if model is already downloaded
            var localPath = Path.Combine(_modelsDirectory, model.Name);
            model.LocalPath = localPath;
            model.IsDownloaded = File.Exists(localPath);

            if (model.IsDownloaded)
            {
                model.DownloadState = DownloadState.Completed;
                model.DownloadProgress = 100;

                // Reconstruct MmProjLocalPath for vision models if the file exists
                if (model.IsVisionModel && !string.IsNullOrEmpty(model.MmProjDownloadUrl))
                {
                    var mmProjName = Path.GetFileName(new Uri(model.MmProjDownloadUrl).LocalPath);
                    var mmProjPath = Path.Combine(_modelsDirectory, mmProjName);
                    if (File.Exists(mmProjPath))
                    {
                        model.MmProjLocalPath = mmProjPath;
                    }
                }
            }
            else if (_downloadService.HasPartialDownload(model.Name))
            {
                model.DownloadState = DownloadState.Paused;
            }

            _models.Add(model);
        }

        // Load custom models from SQLite
        var customModels = await _databaseService.GetCustomModelsAsync();
        foreach (var custom in customModels)
        {
            var model = new LLMModelInfo
            {
                Name = custom.Name,
                DisplayName = custom.DisplayName,
                Description = custom.Description,
                DownloadUrl = custom.DownloadUrl,
                LocalPath = custom.IsLocal ? custom.FilePath : Path.Combine(_modelsDirectory, custom.Name),
                SizeBytes = custom.SizeBytes,
                IsDownloaded = custom.IsLocal || File.Exists(Path.Combine(_modelsDirectory, custom.Name)),
                IsCustomModel = true,
                CustomModelId = custom.Id,
                Organization = "Local",
                OrgLogoUrl = "pack://application:,,,/Assets/logo.png",
                Family = "Custom",
                Variant = "All"
            };

            if (model.IsDownloaded)
            {
                model.DownloadState = DownloadState.Completed;
                model.DownloadProgress = 100;
            }

            _models.Add(model);
        }

        // Auto-load last used model
        await LoadLastUsedModelAsync();
    }

    private async Task<List<LLMModelInfo>> LoadModelCatalogAsync()
    {
        // 1. Try to load from remote endpoint
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            client.DefaultRequestHeaders.Add("User-Agent", "KaiROS-AI/1.0");
            
            var json = await client.GetStringAsync(RemoteCatalogUrl);
            
            if (!string.IsNullOrWhiteSpace(json))
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var remoteList = JsonSerializer.Deserialize<List<LLMModelInfo>>(json, options);
                
                if (remoteList != null && remoteList.Count > 0)
                {
                    // Cache the raw JSON string locally in AppData
                    Directory.CreateDirectory(Path.GetDirectoryName(_cachedCatalogPath)!);
                    await File.WriteAllTextAsync(_cachedCatalogPath, json);
                    System.Diagnostics.Debug.WriteLine($"[KaiROS] Successfully fetched and cached remote catalog with {remoteList.Count} models.");
                    return remoteList;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[KaiROS] Remote catalog fetch failed: {ex.Message}");
        }

        // 2. Fallback: Local Cached Copy in AppData
        try
        {
            if (File.Exists(_cachedCatalogPath))
            {
                var cachedJson = await File.ReadAllTextAsync(_cachedCatalogPath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var cachedList = JsonSerializer.Deserialize<List<LLMModelInfo>>(cachedJson, options);
                if (cachedList != null && cachedList.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[KaiROS] Loaded catalog from local AppData cache with {cachedList.Count} models.");
                    return cachedList;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[KaiROS] Reading cached catalog failed: {ex.Message}");
        }

        // 3. Fallback: Hardcoded AppSettings JSON (Offline/First-Run)
        System.Diagnostics.Debug.WriteLine("[KaiROS] Falling back to built-in appsettings.json model catalog.");
        return _configuration.GetSection("LLMModels").Get<List<LLMModelInfo>>() ?? [];
    }

    private async Task LoadLastUsedModelAsync()
    {
        try
        {
            if (File.Exists(_lastModelSettingsPath))
            {
                var lastModelName = File.ReadAllText(_lastModelSettingsPath).Trim();
                var modelToLoad = _models.FirstOrDefault(m => m.Name == lastModelName);

                if (modelToLoad != null && modelToLoad.IsDownloaded)
                {
                    System.Diagnostics.Debug.WriteLine($"[KaiROS] Auto-loading last used model: {modelToLoad.Name}");
                    // Load in background so we don't block startup UI too much, but we need to await it partly or fire and forget?
                    // Better to fire and forget or let the UI handle the loading state via events if InitializeAsync is awaited during splash.
                    // Since SetActiveModelAsync handles its own threading, we can await it here if we want startup to wait,
                    // OR we can just fire it. The user specifically asked for "On run of the App", so waiting is safer to ensure it's ready.
                    // However, we don't want to freeze the UI.
                    // Let's attempt to load it.
                    await SetActiveModelAsync(modelToLoad);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[KaiROS] Failed to auto-load last model: {ex.Message}");
        }
    }

    public async Task<bool> DownloadModelAsync(LLMModelInfo model, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (model.IsDownloaded) return true;

        var localPath = Path.Combine(_modelsDirectory, model.Name);
        model.DownloadState = DownloadState.Downloading;
        ModelDownloadStarted?.Invoke(this, model);

        try
        {
            // For vision models, split progress: 90% for main model, 10% for mm-proj
            double mainModelShare = model.IsVisionModel && !string.IsNullOrEmpty(model.MmProjDownloadUrl) ? 0.9 : 1.0;

            var wrappedProgress = new Progress<double>(p =>
            {
                model.DownloadProgress = p * mainModelShare;
                progress?.Report(p * mainModelShare);
            });

            var success = await _downloadService.DownloadFileAsync(
                model.DownloadUrl,
                localPath,
                wrappedProgress,
                cancellationToken);

            if (success)
            {
                model.DownloadState = DownloadState.Verifying;
                var valid = await _downloadService.VerifyFileIntegrityAsync(localPath, model.SizeBytes);

                if (valid)
                {
                    model.IsDownloaded = true;
                    model.LocalPath = localPath;

                    // Download mm-proj for vision models
                    if (model.IsVisionModel && !string.IsNullOrEmpty(model.MmProjDownloadUrl))
                    {
                        var mmProjName = Path.GetFileName(new Uri(model.MmProjDownloadUrl).LocalPath);
                        var mmProjPath = Path.Combine(_modelsDirectory, mmProjName);
                        var mmProjProgress = new Progress<double>(p =>
                        {
                            var overall = 90 + p * 0.1;
                            model.DownloadProgress = overall;
                            progress?.Report(overall);
                        });
                        var mmSuccess = await _downloadService.DownloadFileAsync(
                            model.MmProjDownloadUrl, mmProjPath, mmProjProgress, cancellationToken);
                        if (mmSuccess)
                        {
                            model.MmProjLocalPath = mmProjPath;
                            System.Diagnostics.Debug.WriteLine($"[KaiROS] mm-proj downloaded: {mmProjPath}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[KaiROS] mm-proj download failed for {model.Name}. Vision disabled.");
                        }
                    }

                    model.DownloadState = DownloadState.Completed;
                    model.DownloadProgress = 100;
                    ModelDownloadCompleted?.Invoke(this, model);
                    return true;
                }
                else
                {
                    model.DownloadState = DownloadState.Failed;
                    model.LoadError = "File verification failed. The download may be corrupted - please try again.";
                    System.Diagnostics.Debug.WriteLine($"Verification failed for {model.Name} at {localPath}");
                    return false;
                }
            }
            else
            {
                model.DownloadState = DownloadState.Paused;
                return false;
            }
        }
        catch (Exception ex)
        {
            model.DownloadState = DownloadState.Failed;
            model.LoadError = ex.Message;
            throw;
        }
    }

    public async Task PauseDownloadAsync(LLMModelInfo model)
    {
        await _downloadService.PauseDownloadAsync(model.Name);
        model.DownloadState = DownloadState.Paused;
    }

    public async Task ResumeDownloadAsync(LLMModelInfo model)
    {
        await _downloadService.ResumeDownloadAsync(model.Name);
    }

    public async Task<bool> DeleteModelAsync(LLMModelInfo model)
    {
        if (_activeModel?.Name == model.Name)
        {
            await UnloadModelAsync();
        }

        try
        {
            if (model.LocalPath != null && File.Exists(model.LocalPath))
            {
                await Task.Run(() => File.Delete(model.LocalPath));
            }

            var partialPath = Path.Combine(_modelsDirectory, model.Name + ".partial");
            if (File.Exists(partialPath))
            {
                await Task.Run(() => File.Delete(partialPath));
            }

            model.IsDownloaded = false;
            model.DownloadState = DownloadState.NotStarted;
            model.DownloadProgress = 0;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SetActiveModelAsync(LLMModelInfo model, IProgress<double>? progress = null)
    {
        if (!model.IsDownloaded || model.LocalPath == null)
            return false;

        // Save as last used model
        try
        {
            File.WriteAllText(_lastModelSettingsPath, model.Name);
        }
        catch { /* ignore save errors */ }

        if (!File.Exists(model.LocalPath))
        {
            model.IsDownloaded = false;
            return false;
        }

        // Report initial progress
        progress?.Report(5);
        ModelLoadProgress?.Invoke(this, 5);

        // Unload current model if any
        await UnloadModelAsync();

        progress?.Report(10);
        ModelLoadProgress?.Invoke(this, 10);

        try
        {
            // Get hardware info for GPU detection
            var hardwareInfo = await _hardwareService.DetectHardwareAsync();

            // Configure LLamaSharp native library search path BEFORE first use.
            // In MSIX packages, the default probing may not find runtimes/<rid>/native/.
            // Must pass the detected backend so we ONLY register directories whose
            // DLLs can actually load on this hardware.  GPU backend folders lack
            // ggml-cpu.dll, so loading from cuda12/ on a CPU-only machine fails in the
            // static constructor and permanently poisons the type (TypeInitializationException).
            ConfigureNativeLibrary(hardwareInfo.SelectedBackend);

            // Calculate optimal GPU layers based on VRAM and model size
            _currentGpuLayers = CalculateOptimalGpuLayers(hardwareInfo, model);

            NativeLog($"Loading model '{model.Name}' from '{model.LocalPath}' " +
                      $"backend={hardwareInfo.SelectedBackend} GpuLayers={_currentGpuLayers}");

            // ----------------------------------------------------------------
            // Pre-flight RAM check (CPU / NPU only).
            // For GPU backends the model lives in VRAM and CalculateOptimalGpuLayers
            // already guards against over-commit, so we only gate the CPU path here.
            // ----------------------------------------------------------------
            double modelSizeGB = model.SizeBytes > 0 ? model.SizeBytes / (1024.0 * 1024.0 * 1024.0) : 0;
            bool isCpuLoad = _currentGpuLayers == 0;
            if (isCpuLoad && modelSizeGB > 0)
            {
                // Refresh available RAM since DetectHardwareAsync caches it from
                // process start; this gives a more accurate snapshot.
                long availableRamBytes;
                try
                {
                    var memInfo = GC.GetGCMemoryInfo();
                    availableRamBytes = Math.Max(
                        memInfo.TotalAvailableMemoryBytes - Environment.WorkingSet,
                        hardwareInfo.AvailableRamBytes);
                }
                catch
                {
                    availableRamBytes = hardwareInfo.AvailableRamBytes;
                }

                double availableRamGB = availableRamBytes / (1024.0 * 1024.0 * 1024.0);
                // Need ~1.2x the file size for weights + ~0.5GB for KV cache + headroom.
                double requiredGB = (modelSizeGB * 1.2) + 1.0;

                if (availableRamGB > 0 && availableRamGB < requiredGB)
                {
                    var msg = $"Not enough free RAM to load this model. " +
                              $"Model needs ~{requiredGB:F1} GB free, only {availableRamGB:F1} GB available. " +
                              $"Close other applications or choose a smaller model.";
                    NativeLog($"Pre-flight RAM check FAILED: {msg}");
                    model.LoadError = msg;
                    progress?.Report(0);
                    ModelLoadProgress?.Invoke(this, 0);
                    return false;
                }
                NativeLog($"Pre-flight RAM check OK: required ~{requiredGB:F1} GB, available {availableRamGB:F1} GB");
            }

            // Scale context size for large CPU models so the KV cache doesn't blow
            // out RAM.  GPU loads keep the full 4096 because VRAM was already sized.
            int contextSize = 4096;
            if (isCpuLoad)
            {
                if (modelSizeGB >= 18.0) contextSize = 1024;        // 30B+
                else if (modelSizeGB >= 7.0) contextSize = 2048;    // 13B+
                else contextSize = 4096;                            // 8B and smaller
            }

            // Scale per-attempt load timeout with model size — a 20 GB model on
            // CPU + slow disk legitimately needs more than 45 seconds to mmap.
            int loadTimeoutSeconds = (int)Math.Clamp(45 + (modelSizeGB * 8), 45, 300);

            // Try loading with decreasing GPU layers on failure.
            // For pure-CPU loads, only attempt CPU once — the original
            // [0, 1, 1, 0] sequence would spuriously try GPU on a CPU-only machine.
            int[] layersToTry = isCpuLoad
                ? new[] { 0 }
                : new[]
                {
                    _currentGpuLayers,
                    Math.Max(1, _currentGpuLayers / 2),  // 50%
                    Math.Max(1, _currentGpuLayers / 4),  // 25%
                    0  // CPU fallback
                };

            Exception? lastException = null;

            foreach (var layers in layersToTry)
            {
                // Declared outside try so catch blocks can dispose on failure.
                LLamaWeights? weights = null;
                MtmdWeights? llavaWeights = null;

                try
                {
                    System.Diagnostics.Debug.WriteLine($"[KaiROS] Attempting to load with {layers} GPU layers (ctx={contextSize}, timeout={loadTimeoutSeconds}s)...");

                    // Strict timeout to prevent hanging on Intel drivers (scaled by model size)
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(loadTimeoutSeconds));

                    await Task.Run(() =>
                    {
                        progress?.Report(20);
                        ModelLoadProgress?.Invoke(this, 20);

                        var parameters = new ModelParams(model.LocalPath)
                        {
                            ContextSize = (uint)contextSize,
                            GpuLayerCount = layers
                        };

                        progress?.Report(30);
                        ModelLoadProgress?.Invoke(this, 30);

                        // This is the heavy operation - loading weights
                        weights = LLamaWeights.LoadFromFile(parameters);

                        // Load LLava mmproj if this is a vision model
                        if (model.IsVisionModel && !string.IsNullOrEmpty(model.MmProjLocalPath) && File.Exists(model.MmProjLocalPath))
                        {
                            System.Diagnostics.Debug.WriteLine($"[KaiROS] Loading MTMD mm-proj: {model.MmProjLocalPath}");
                            llavaWeights = MtmdWeights.LoadFromFile(
                                model.MmProjLocalPath,
                                weights,
                                MtmdContextParams.Default());
                            System.Diagnostics.Debug.WriteLine($"[KaiROS] Vision model ready. Supports vision: {llavaWeights.SupportsVision}");
                        }

                        progress?.Report(90);
                        ModelLoadProgress?.Invoke(this, 90);
                    }, cts.Token).WaitAsync(TimeSpan.FromSeconds(loadTimeoutSeconds));

                    // Only assign to fields after the task completes successfully
                    _loadedWeights = weights;
                    _loadedLlavaWeights = llavaWeights;

                    // Success!
                    _currentGpuLayers = layers;
                    _activeModel = model;
                    model.IsActive = true;

                    progress?.Report(100);
                    ModelLoadProgress?.Invoke(this, 100);

                    if (layers < layersToTry[0])
                    {
                        System.Diagnostics.Debug.WriteLine($"[KaiROS] Model loaded successfully with reduced layers: {layers} (original: {layersToTry[0]})");
                    }

                    ModelLoaded?.Invoke(this, model);
                    return true;
                }
                catch (TypeInitializationException ex)
                {
                    // A TypeInitializationException means the native library static
                    // constructor failed.  The type is permanently poisoned — retrying
                    // with fewer GPU layers will not help.
                    lastException = ex;
                    NativeLog($"FATAL TypeInitializationException at layers={layers}: " +
                              $"{ex.GetType().FullName}: {ex.Message}");
                    NativeLog($"  InnerException: {ex.InnerException?.GetType().FullName}: {ex.InnerException?.Message}");
                    NativeLog($"  Full stack: {ex}");

                    // Clean up the local weights (fields were never assigned)
                    try { weights?.Dispose(); } catch { }
                    try { llavaWeights?.Dispose(); } catch { }
                    break; // No point retrying — type is permanently broken
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    NativeLog($"Load attempt failed at layers={layers}: {ex.GetType().Name}: {ex.Message}");

                    // Clean up local weights (fields were never assigned)
                    try { weights?.Dispose(); } catch { }
                    try { llavaWeights?.Dispose(); } catch { }

                    // If this was already CPU fallback (0 layers), don't retry
                    if (layers == 0) break;
                }
            }

            // All attempts failed
            System.Diagnostics.Debug.WriteLine($"Error loading model: {lastException?.Message}");

            // Give a user-friendly message for the type initializer crash
            if (lastException is TypeInitializationException tie)
            {
                model.LoadError = $"Failed to load native AI engine. " +
                    $"This usually means the required backend libraries are missing or incompatible with your hardware. " +
                    $"Details: {tie.InnerException?.Message ?? tie.Message}";
            }
            else
            {
                model.LoadError = lastException?.Message ?? "Failed to load model after multiple attempts";
            }
            progress?.Report(0);
            ModelLoadProgress?.Invoke(this, 0);
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading model: {ex.Message}");
            // Store error for UI to display
            model.LoadError = ex.Message;
            progress?.Report(0);
            ModelLoadProgress?.Invoke(this, 0);
            return false;
        }
    }

    public async Task UnloadModelAsync()
    {
        if (_loadedWeights != null)
        {
            await Task.Run(() =>
            {
                _loadedLlavaWeights?.Dispose();
                _loadedLlavaWeights = null;
                _loadedWeights.Dispose();
                _loadedWeights = null;
            });

            if (_activeModel != null)
            {
                _activeModel.IsActive = false;
                _activeModel = null;
            }

            ModelUnloaded?.Invoke(this, EventArgs.Empty);
            GC.Collect();
        }
    }

    public async Task<bool> VerifyModelAsync(LLMModelInfo model)
    {
        if (model.LocalPath == null) return false;
        return await _downloadService.VerifyFileIntegrityAsync(model.LocalPath, model.SizeBytes);
    }

    public void SetModelsDirectory(string path)
    {
        _modelsDirectory = path;
        Directory.CreateDirectory(_modelsDirectory);
    }

    public LLamaWeights? GetLoadedWeights() => _loadedWeights;
    public MtmdWeights? GetLoadedLlavaWeights() => _loadedLlavaWeights;

    // ── Win32 P/Invoke for DLL search path manipulation ─────────────
    // MSIX packaged apps have a restricted DLL search order. The PATH
    // environment variable does NOT reliably affect the Windows loader
    // in packaged contexts.  SetDllDirectory adds a single directory
    // to the OS-level search order and works in MSIX.
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDllDirectory(string lpPathName);

    // ── Persistent native-loader diagnostics ────────────────────────
    // Writes to %LOCALAPPDATA%\KaiROS.AI\native_loader.log so we have
    // visibility into Store cert failures (Debug.WriteLine is a no-op
    // in Release builds with no debugger attached).
    private static readonly string _nativeLoaderLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KaiROS.AI", "native_loader.log");

    private static void NativeLog(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[KaiROS] {message}");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_nativeLoaderLogPath)!);
            File.AppendAllText(_nativeLoaderLogPath,
                $"[{DateTimeOffset.Now:o}] {message}{Environment.NewLine}");
        }
        catch { /* never let diagnostics throw */ }
    }

    /// <summary>
    /// Configure native DLL loading for LLamaSharp in an MSIX package.
    ///
    /// <para><b>Why this is needed:</b></para>
    /// <para>
    /// .NET's <c>NativeLibrary.Load(absolutePath)</c> calls <c>LoadLibraryEx</c>
    /// with <c>flags=0</c>.  This loads the DLL itself from the absolute path, but
    /// its <b>transitive dependencies</b> (e.g. <c>ggml-cpu.dll</c> imported by
    /// <c>llama.dll</c>) are resolved using the <em>standard</em> DLL search
    /// order — which does <b>NOT</b> include the loaded DLL's own directory.
    /// In a regular (unpackaged) app, manipulating the PATH environment variable
    /// works around this.  In MSIX, PATH changes are silently ignored by the loader.
    /// </para>
    ///
    /// <para><b>How we fix it (belt-and-suspenders):</b></para>
    /// <list type="number">
    ///   <item>Pick the single best native subdirectory for the detected backend
    ///         and CPU instruction set.</item>
    ///   <item><c>SetDllDirectory(targetDir)</c> — adds the directory to the
    ///         OS-level DLL search order (works in MSIX).</item>
    ///   <item>Pre-load every DLL from that directory in dependency order so they
    ///         are already in the process module list before LLamaSharp asks for them.</item>
    ///   <item>Tell LLamaSharp to search only that one directory.</item>
    /// </list>
    /// </summary>
    private static void ConfigureNativeLibrary(ExecutionBackend selectedBackend)
    {
        if (_nativeLibConfigured) return;

        try
        {
            var baseDir = AppContext.BaseDirectory;

            var arch = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X86   => "win-x86",
                Architecture.Arm64 => "win-arm64",
                _                  => "win-x64"
            };

            // ── Pick the SINGLE best native subdirectory ──────────────────
            string bestSubDir = selectedBackend switch
            {
                ExecutionBackend.Cuda   => "cuda12",
                ExecutionBackend.Vulkan => "vulkan",
                _                       => DetectBestCpuVariant()
            };

            var runtimeNativeDir = Path.Combine(baseDir, "runtimes", arch, "native");
            var targetDir = Path.Combine(runtimeNativeDir, bestSubDir);

            NativeLog($"=== ConfigureNativeLibrary START ===");
            NativeLog($"baseDir={baseDir}");
            NativeLog($"arch={arch}, backend={selectedBackend}, bestSubDir={bestSubDir}");
            NativeLog($"runtimeNativeDir={runtimeNativeDir} (exists={Directory.Exists(runtimeNativeDir)})");

            // Enumerate what's actually deployed so we can diagnose Store cert failures
            if (Directory.Exists(runtimeNativeDir))
            {
                foreach (var sub in Directory.GetDirectories(runtimeNativeDir))
                {
                    var dlls = Directory.GetFiles(sub, "*.dll").Select(Path.GetFileName);
                    NativeLog($"  subdir={Path.GetFileName(sub)} dlls=[{string.Join(",", dlls)}]");
                }
            }

            // Fallback chain: best → noavx → runtimeNativeDir → baseDir
            if (!Directory.Exists(targetDir))
            {
                NativeLog($"targetDir does not exist, falling back to noavx");
                targetDir = Path.Combine(runtimeNativeDir, "noavx");
            }
            if (!Directory.Exists(targetDir))
            {
                NativeLog($"noavx also missing, falling back to runtimeNativeDir / baseDir");
                targetDir = Directory.Exists(runtimeNativeDir) ? runtimeNativeDir : baseDir;
            }

            NativeLog($"FINAL targetDir={targetDir}");

            // ── Fix 1: OS-level DLL search path ──────────────────────────
            // Adds targetDir to the Windows loader search order so transitive
            // dependencies (ggml-base.dll, ggml.dll, ggml-cpu.dll) are found
            // when llama.dll is loaded from that directory.
            if (!SetDllDirectory(targetDir))
            {
                NativeLog($"SetDllDirectory FAILED: Win32 error {Marshal.GetLastWin32Error()}");
            }
            else
            {
                NativeLog($"SetDllDirectory OK");
            }

            // ── Fix 2: Pre-load DLLs in dependency order ─────────────────
            // Once loaded, Windows resolves imports by module name from the
            // already-loaded list — no directory search needed.
            PreloadNativeDlls(targetDir);

            // ── Fix 3: Tell LLamaSharp where to look ─────────────────────
            var config = NativeLibraryConfig.All
                .WithSearchDirectory(targetDir);

            switch (selectedBackend)
            {
                case ExecutionBackend.Cuda:
                    config.WithCuda();
                    break;
                case ExecutionBackend.Vulkan:
                    config.WithVulkan();
                    break;
                default:
                    config.WithCuda(false).WithVulkan(false);
                    break;
            }

            config.WithAutoFallback(true);

            _nativeLibConfigured = true;
            NativeLog($"=== ConfigureNativeLibrary OK ===");
        }
        catch (Exception ex)
        {
            NativeLog($"NativeLibraryConfig setup FAILED: {ex}");
        }
    }

    /// <summary>
    /// Pre-load all native DLLs from the target directory in dependency order.
    /// Leaf dependencies first, then dependents.  Once a DLL is loaded into the
    /// process, Windows resolves subsequent imports of the same module name from
    /// the already-loaded list — bypassing the directory search entirely.
    /// </summary>
    private static void PreloadNativeDlls(string nativeDir)
    {
        // Order matters: ggml-base has no ggml deps → ggml depends on ggml-base →
        // backend DLL depends on ggml-base → llama depends on all of the above.
        string[] dllsInOrder =
        [
            "ggml-base.dll",
            "ggml.dll",
            "ggml-cpu.dll",
            "ggml-cuda.dll",
            "ggml-vulkan.dll",
            "llama.dll"
        ];

        foreach (var dll in dllsInOrder)
        {
            var fullPath = Path.Combine(nativeDir, dll);
            if (!File.Exists(fullPath))
            {
                NativeLog($"Pre-load SKIP (not in dir): {dll}");
                continue;
            }

            try
            {
                NativeLibrary.Load(fullPath);
                NativeLog($"Pre-load OK: {dll}");
            }
            catch (Exception ex)
            {
                NativeLog($"Pre-load FAILED for {dll}: {ex.Message} (HResult=0x{ex.HResult:X8})");
            }
        }
    }

    /// <summary>
    /// Detect the best CPU SIMD instruction set supported by this processor.
    /// Maps to the llama.cpp native subdirectory names.
    /// </summary>
    private static string DetectBestCpuVariant()
    {
        try
        {
            if (System.Runtime.Intrinsics.X86.Avx512F.IsSupported) return "avx512";
            if (System.Runtime.Intrinsics.X86.Avx2.IsSupported)    return "avx2";
            if (System.Runtime.Intrinsics.X86.Avx.IsSupported)     return "avx";
        }
        catch
        {
            // ARM64 or other arch — X86 intrinsics not available
        }
        return "noavx";
    }

    /// <summary>
    /// Calculate optimal GPU layers based on available VRAM and model size.
    /// Uses conservative estimates to prevent OOM crashes.
    /// </summary>
    private int CalculateOptimalGpuLayers(HardwareInfo hardwareInfo, LLMModelInfo model)
    {
        // CPU-only or NPU modes don't use GPU layers
        if (hardwareInfo.SelectedBackend == ExecutionBackend.Cpu ||
            hardwareInfo.SelectedBackend == ExecutionBackend.Npu)
        {
            return 0;
        }

        // Get available VRAM in GB
        double vramGB = hardwareInfo.GpuMemoryBytes / (1024.0 * 1024.0 * 1024.0);
        if (vramGB <= 0) vramGB = 0; // Don't assume VRAM if detection failed - fallback to CPU

        // Get model size in GB
        double modelSizeGB = model.SizeBytes / (1024.0 * 1024.0 * 1024.0);
        if (modelSizeGB <= 0) modelSizeGB = 4.0; // Default assumption for unknown size

        // Estimate total layers based on model size (Q4_K_M quantization typical values)
        int estimatedTotalLayers = modelSizeGB switch
        {
            < 1.0 => 22,    // TinyLlama ~1B
            < 2.0 => 24,    // Phi-2 ~2.7B
            < 3.0 => 26,    // Phi-3 Mini ~3.8B, LLaMA 3.2 3B
            < 5.0 => 32,    // Mistral 7B, LLaMA 3.1 8B
            < 8.0 => 40,    // 13B models
            _ => 60         // Larger models
        };

        System.Diagnostics.Debug.WriteLine($"[GPU] VRAM: {vramGB:F1} GB, Model: {modelSizeGB:F1} GB, Est. layers: {estimatedTotalLayers}");

        // Calculate how many layers can fit in VRAM
        // Rule of thumb: Each layer uses approximately (ModelSize / TotalLayers) * 1.2 (20% overhead)
        double memoryPerLayerGB = (modelSizeGB / estimatedTotalLayers) * 1.2;

        // Reserve 1.5GB VRAM for context, KV cache, and system overhead
        double availableForLayersGB = Math.Max(0, vramGB - 1.5);

        int maxLayersByVram = (int)(availableForLayersGB / memoryPerLayerGB);

        // Take minimum of estimated total layers and what fits in VRAM
        int optimalLayers = Math.Min(estimatedTotalLayers, maxLayersByVram);

        // Ensure we have at least some GPU acceleration if VRAM allows
        if (optimalLayers < 5 && vramGB >= 2.0)
        {
            optimalLayers = 5; // Minimum useful GPU acceleration
        }

        // Cap at reasonable maximum to leave room for other operations
        optimalLayers = Math.Min(optimalLayers, 100);

        // Never go below 0
        optimalLayers = Math.Max(optimalLayers, 0);

        System.Diagnostics.Debug.WriteLine($"[GPU] Calculated optimal layers: {optimalLayers} (max by VRAM: {maxLayersByVram})");

        return optimalLayers;
    }

    public void Dispose()
    {
        try { _loadedLlavaWeights?.Dispose(); } catch { }
        _loadedLlavaWeights = null;
        try { _loadedWeights?.Dispose(); } catch { }
        _loadedWeights = null;
        if (_activeModel != null)
        {
            _activeModel.IsActive = false;
            _activeModel = null;
        }
    }
}
