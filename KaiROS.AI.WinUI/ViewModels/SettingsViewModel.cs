using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KaiROS.AI.WinUI;
using KaiROS.AI.WinUI.Models;
using KaiROS.AI.WinUI.Services;
using Microsoft.Extensions.DependencyInjection;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Collections.ObjectModel;
using NAudio.Wave;

namespace KaiROS.AI.WinUI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IHardwareDetectionService _hardwareService;
    private readonly IModelManagerService _modelManager;
    private readonly ChatViewModel _chatViewModel;
    private readonly IThemeService _themeService;
    private readonly IApiService _apiService;
    private readonly IAgentService _agentService;
    private readonly IUserPreferencesService _preferences;
    private readonly IChatService _chatService;
    private readonly ISpeechToTextService _sttService;
    private readonly ITextToSpeechService _ttsService;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

    [ObservableProperty]
    public partial bool IsVoiceInputEnabled { get; set; }

    [ObservableProperty]
    public partial string WhisperModelSize { get; set; } = "base";

    [ObservableProperty]
    public partial string SelectedInputDevice { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsAutoSendEnabled { get; set; }

    [ObservableProperty]
    public partial double SilenceDurationSeconds { get; set; } = 2.0;

    [ObservableProperty]
    public partial bool IsTtsEnabled { get; set; }

    [ObservableProperty]
    public partial string SelectedVoiceId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double TtsSpeechRate { get; set; } = 1.0;

    [ObservableProperty]
    public partial double TtsVolume { get; set; } = 1.0;

    [ObservableProperty]
    public partial bool IsAutoReadEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsWhisperDownloading { get; set; }

    [ObservableProperty]
    public partial double WhisperDownloadProgress { get; set; }

    public ObservableCollection<string> AvailableInputDevices { get; } = new();
    public ObservableCollection<VoiceOption> AvailableVoices { get; } = new();
    public ObservableCollection<string> AvailableModelSizes { get; } = new() { "tiny", "base", "small" };

    public class VoiceOption
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    [ObservableProperty]
    public partial bool IsFileReaderEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsFileWriterEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsWebFetchEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsCalculatorEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsSystemInfoEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsDateTimeEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsClipboardEnabled { get; set; }

    private const string DefaultSystemPrompt = "You are a helpful, friendly AI assistant. Be concise and clear.";

    [ObservableProperty]
    public partial HardwareInfo? Hardware { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<ExecutionBackend> AvailableBackends { get; set; } = [];

    [ObservableProperty]
    public partial ExecutionBackend SelectedBackend { get; set; }

    [ObservableProperty]
    public partial string ModelsDirectory { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GpuInfo { get; set; } = "Detecting...";

    [ObservableProperty]
    public partial string RamInfo { get; set; } = "Detecting...";

    [ObservableProperty]
    public partial string BackendStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SystemPrompt { get; set; } = DefaultSystemPrompt;

    [ObservableProperty]
    public partial bool IsDarkTheme { get; set; } = true;

    // API Settings
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ApiStatus))]
    [NotifyPropertyChangedFor(nameof(IsMinimizeToTrayEnabled))]
    public partial bool IsApiEnabled { get; set; } = false;

    [ObservableProperty]
    public partial int ApiPort { get; set; } = 5000;

    // Context Window Settings
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveContextInfo))]
    public partial ContextWindowOption SelectedContextWindow { get; set; } = ContextWindowOption.Auto;

    public ObservableCollection<ContextWindowOption> ContextWindowOptions { get; } =
        new(Enum.GetValues<ContextWindowOption>());

    public string ActiveContextInfo
    {
        get
        {
            if (_chatService == null) return "Load a model to see effective context size";
            try
            {
                var size = ((ChatService)_chatService).CalculateSafeContextSize();
                return $"Effective size: {size:N0} tokens ({size / 1024.0:F1}K)";
            }
            catch { return "N/A"; }
        }
    }

    // API can only be enabled when a model is loaded
    public bool CanEnableApi => _modelManager.ActiveModel != null;

    // System tray only enabled when API is running
    public bool IsMinimizeToTrayEnabled => IsApiEnabled && _apiService.IsRunning;

    public string ApiStatus => _apiService.IsRunning
        ? $"Running on http://localhost:{_apiService.Port}/"
        : CanEnableApi ? "Stopped (ready to start)" : "Disabled (load a model first)";

    public SettingsViewModel(
        IHardwareDetectionService hardwareService,
        IModelManagerService modelManager,
        ChatViewModel chatViewModel,
        IThemeService themeService,
        IApiService apiService,
        IAgentService agentService,
        IUserPreferencesService preferences,
        IChatService chatService,
        ISpeechToTextService sttService,
        ITextToSpeechService ttsService)
    {
        _hardwareService = hardwareService;
        _modelManager = modelManager;
        _chatViewModel = chatViewModel;
        _themeService = themeService;
        _apiService = apiService;
        _agentService = agentService;
        _preferences = preferences;
        _chatService = chatService;
        _sttService = sttService;
        _ttsService = ttsService;

        // Initialize voice preferences
        IsVoiceInputEnabled = _preferences.IsVoiceInputEnabled;
        WhisperModelSize = _preferences.WhisperModelSize;
        SelectedInputDevice = _preferences.SelectedInputDevice;
        IsAutoSendEnabled = _preferences.IsAutoSendEnabled;
        SilenceDurationSeconds = _preferences.SilenceDurationSeconds;
        IsTtsEnabled = _preferences.IsTtsEnabled;
        SelectedVoiceId = _preferences.SelectedVoiceId;
        TtsSpeechRate = _preferences.TtsSpeechRate;
        TtsVolume = _preferences.TtsVolume;
        IsAutoReadEnabled = _preferences.IsAutoReadEnabled;

        // Load resources
        LoadAvailableVoices();
        LoadAvailableDevices();

        // Wire download progress
        _sttService.DownloadProgressChanged += (s, p) =>
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                WhisperDownloadProgress = p;
                IsWhisperDownloading = _sttService.IsDownloadingModel;
            });
        };

        // Initialize tool toggles from AgentService
        IsFileReaderEnabled = _agentService.IsFileReaderEnabled;
        IsFileWriterEnabled = _agentService.IsFileWriterEnabled;
        IsWebFetchEnabled = _agentService.IsWebFetchEnabled;
        IsCalculatorEnabled = _agentService.IsCalculatorEnabled;
        IsSystemInfoEnabled = _agentService.IsSystemInfoEnabled;
        IsDateTimeEnabled = _agentService.IsDateTimeEnabled;
        IsClipboardEnabled = _agentService.IsClipboardEnabled;

        // Initialize system prompt from ChatViewModel
        SystemPrompt = chatViewModel.SystemPrompt;

        // Initialize theme from service
        IsDarkTheme = _themeService.CurrentTheme == "Dark";

        // Initialize API status
        IsApiEnabled = _apiService.IsRunning;

        // Initialize context window from persisted preference
        SelectedContextWindow = _preferences.ContextWindowPreference;

        // Subscribe to model events to update CanEnableApi
        _modelManager.ModelLoaded += (s, e) =>
        {
            OnPropertyChanged(nameof(CanEnableApi));
            OnPropertyChanged(nameof(ApiStatus));
            OnPropertyChanged(nameof(ActiveContextInfo));
        };
        _modelManager.ModelUnloaded += (s, e) =>
        {
            OnPropertyChanged(nameof(CanEnableApi));
            OnPropertyChanged(nameof(ApiStatus));
            OnPropertyChanged(nameof(ActiveContextInfo));
            // Disable API if model is unloaded
            if (IsApiEnabled)
            {
                IsApiEnabled = false;
            }
        };
    }

    partial void OnSystemPromptChanged(string value)
    {
        // Sync to ChatViewModel
        _chatViewModel.SystemPrompt = value;
        _preferences.SystemPrompt = value;
    }

    partial void OnSelectedContextWindowChanged(ContextWindowOption value)
    {
        _preferences.ContextWindowPreference = value;
        // Reinitialize context with new size if a model is loaded
        _chatService.ClearContext();
        OnPropertyChanged(nameof(ActiveContextInfo));
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        _themeService.SetTheme(value ? "Dark" : "Light");
    }

    async partial void OnIsApiEnabledChanged(bool value)
    {
        try
        {
            if (value)
            {
                await _apiService.StartAsync(ApiPort);
            }
            else
            {
                await _apiService.StopAsync();
            }
            OnPropertyChanged(nameof(ApiStatus));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[KaiROS] API toggle failed: {ex.Message}");
            // Revert toggle to avoid inconsistent state
            if (value) IsApiEnabled = false;
            OnPropertyChanged(nameof(ApiStatus));
        }
    }

    public override async Task InitializeAsync()
    {
        IsLoading = true;

        try
        {
            Hardware = await _hardwareService.DetectHardwareAsync();

            AvailableBackends.Clear();
            foreach (var backend in Hardware.AvailableBackends)
            {
                AvailableBackends.Add(backend);
            }

            SelectedBackend = Hardware.SelectedBackend;
            ModelsDirectory = _modelManager.ModelsDirectory;

            GpuInfo = !string.IsNullOrEmpty(Hardware.GpuName)
                ? $"{Hardware.GpuName} ({Hardware.GpuMemoryText})"
                : "No dedicated GPU detected";

            RamInfo = $"{Hardware.TotalRamText} total, {Hardware.AvailableRamText} available";

            UpdateBackendStatus();
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedBackendChanged(ExecutionBackend value)
    {
        if (Hardware != null)
        {
            Hardware.SelectedBackend = value;
            // Also update the service's cached copy so model loading respects this selection
            _hardwareService.SetSelectedBackend(value);
            UpdateBackendStatus();
        }
    }

    private void UpdateBackendStatus()
    {
        BackendStatus = SelectedBackend switch
        {
            ExecutionBackend.Cpu => "✓ CPU mode: Compatible with all systems. Slower but reliable.",
            ExecutionBackend.Cuda => Hardware?.HasCuda == true
                ? "✓ CUDA: NVIDIA GPU acceleration enabled."
                : "⚠ CUDA not available. Install CUDA toolkit.",
            ExecutionBackend.Vulkan => Hardware?.HasVulkan == true
                ? "✓ Vulkan: High-performance GPU acceleration enabled (Best for Intel Arc/AMD)."
                : "⚠ Vulkan not available.",
            ExecutionBackend.Npu => Hardware?.HasNpu == true
                ? "✓ NPU: Neural processing unit detected."
                : "⚠ NPU not available on this system.",
            _ => "Select a backend"
        };
    }

    [RelayCommand]
    private async Task BrowseModelsDirectory()
    {
        var picker = new FolderPicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add("*");
        var mainWindow = App.Current.Services.GetRequiredService<MainWindow>();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(mainWindow));

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            ModelsDirectory = folder.Path;
            _modelManager.SetModelsDirectory(folder.Path);
        }
    }

    [RelayCommand]
    private void UseRecommendedBackend()
    {
        if (Hardware != null)
        {
            SelectedBackend = Hardware.RecommendedBackend;
        }
    }

    [RelayCommand]
    private async Task RefreshHardwareInfo()
    {
        _hardwareService.ClearCache();
        await InitializeAsync();
    }

    [RelayCommand]
    private void ResetSystemPrompt()
    {
        SystemPrompt = DefaultSystemPrompt;
    }

    partial void OnIsFileReaderEnabledChanged(bool value) => _agentService.IsFileReaderEnabled = value;
    partial void OnIsFileWriterEnabledChanged(bool value) => _agentService.IsFileWriterEnabled = value;
    partial void OnIsWebFetchEnabledChanged(bool value) => _agentService.IsWebFetchEnabled = value;
    partial void OnIsCalculatorEnabledChanged(bool value) => _agentService.IsCalculatorEnabled = value;
    partial void OnIsSystemInfoEnabledChanged(bool value) => _agentService.IsSystemInfoEnabled = value;
    partial void OnIsDateTimeEnabledChanged(bool value) => _agentService.IsDateTimeEnabled = value;
    partial void OnIsClipboardEnabledChanged(bool value) => _agentService.IsClipboardEnabled = value;

    // Voice Settings Handlers
    partial void OnIsVoiceInputEnabledChanged(bool value) => _preferences.IsVoiceInputEnabled = value;
    partial void OnWhisperModelSizeChanged(string value) => _preferences.WhisperModelSize = value;
    partial void OnSelectedInputDeviceChanged(string value) => _preferences.SelectedInputDevice = value;
    partial void OnIsAutoSendEnabledChanged(bool value) => _preferences.IsAutoSendEnabled = value;
    partial void OnSilenceDurationSecondsChanged(double value) => _preferences.SilenceDurationSeconds = value;
    partial void OnIsTtsEnabledChanged(bool value) => _preferences.IsTtsEnabled = value;

    partial void OnSelectedVoiceIdChanged(string value)
    {
        _preferences.SelectedVoiceId = value;
        _ = _ttsService.SetVoiceAsync(value);
    }

    partial void OnTtsSpeechRateChanged(double value)
    {
        _preferences.TtsSpeechRate = value;
        _ = _ttsService.SetRateAsync(value);
    }

    partial void OnTtsVolumeChanged(double value)
    {
        _preferences.TtsVolume = value;
        _ = _ttsService.SetVolumeAsync(value);
    }

    partial void OnIsAutoReadEnabledChanged(bool value) => _preferences.IsAutoReadEnabled = value;

    private void LoadAvailableVoices()
    {
        AvailableVoices.Clear();
        foreach (var voice in _ttsService.GetAvailableVoices())
        {
            AvailableVoices.Add(new VoiceOption
            {
                Id = voice.Id,
                DisplayName = $"{voice.DisplayName} ({voice.Language})"
            });
        }
    }

    private void LoadAvailableDevices()
    {
        AvailableInputDevices.Clear();
        for (int i = 0; i < WaveIn.DeviceCount; i++)
        {
            var capabilities = WaveIn.GetCapabilities(i);
            AvailableInputDevices.Add(capabilities.ProductName);
        }

        if (string.IsNullOrEmpty(SelectedInputDevice) && AvailableInputDevices.Count > 0)
        {
            SelectedInputDevice = AvailableInputDevices[0];
            _preferences.SelectedInputDevice = SelectedInputDevice;
        }
    }

    [RelayCommand]
    private async Task DownloadWhisperModel()
    {
        if (IsWhisperDownloading) return;

        IsWhisperDownloading = true;
        WhisperDownloadProgress = 0;

        var success = await _sttService.EnsureModelDownloadedAsync(WhisperModelSize);

        IsWhisperDownloading = false;

        var mainWindow = App.Current.Services.GetRequiredService<MainWindow>();
        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            Title = success ? "Success" : "Download Failed",
            Content = success 
                ? $"Whisper model ({WhisperModelSize}) downloaded successfully." 
                : $"Failed to download Whisper model ({WhisperModelSize}). Please check your internet connection.",
            CloseButtonText = "OK",
            XamlRoot = mainWindow.Content.XamlRoot
        };
        await dialog.ShowAsync();
    }
}
