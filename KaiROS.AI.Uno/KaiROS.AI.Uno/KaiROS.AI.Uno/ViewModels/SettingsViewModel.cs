using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KaiROS.AI.Uno.Models;
using KaiROS.AI.Uno.Services;
using System.Collections.ObjectModel;

namespace KaiROS.AI.Uno.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IHardwareDetectionService _hardwareService;
    private readonly IModelManagerService _modelManager;
    private readonly ChatViewModel _chatViewModel;
    private readonly IKairosThemeService _themeService;
    private readonly IApiService _apiService;

    private const string DefaultSystemPrompt = "You are a helpful, friendly AI assistant. Be concise and clear.";

    [ObservableProperty]
    private HardwareInfo? _hardware;

    [ObservableProperty]
    private ObservableCollection<ExecutionBackend> _availableBackends = [];

    [ObservableProperty]
    private ExecutionBackend _selectedBackend;

    [ObservableProperty]
    private string _modelsDirectory = string.Empty;

    [ObservableProperty]
    private string _gpuInfo = "Detecting...";

    [ObservableProperty]
    private string _ramInfo = "Detecting...";

    [ObservableProperty]
    private string _backendStatus = string.Empty;

    [ObservableProperty]
    private string _systemPrompt = DefaultSystemPrompt;

    [ObservableProperty]
    private bool _isDarkTheme = true;

    // API Settings
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ApiStatus))]
    private bool _isApiEnabled = false;

    [ObservableProperty]
    private int _apiPort = 5000;

    // API can only be enabled when a model is loaded
    public bool CanEnableApi => _modelManager.ActiveModel != null;

    public string ApiStatus => _apiService.IsRunning
        ? $"Running on http://localhost:{_apiService.Port}/"
        : CanEnableApi ? "Stopped (ready to start)" : "Disabled (load a model first)";

    public SettingsViewModel(
        IHardwareDetectionService hardwareService,
        IModelManagerService modelManager,
        ChatViewModel chatViewModel,
        IKairosThemeService themeService,
        IApiService apiService)
    {
        _hardwareService = hardwareService;
        _modelManager = modelManager;
        _chatViewModel = chatViewModel;
        _themeService = themeService;
        _apiService = apiService;

        _systemPrompt = chatViewModel.SystemPrompt;
        _isDarkTheme = _themeService.CurrentTheme == "Dark";
        _isApiEnabled = _apiService.IsRunning;

        _modelManager.ModelLoaded += (s, e) =>
        {
            OnPropertyChanged(nameof(CanEnableApi));
            OnPropertyChanged(nameof(ApiStatus));
        };
        _modelManager.ModelUnloaded += (s, e) =>
        {
            OnPropertyChanged(nameof(CanEnableApi));
            OnPropertyChanged(nameof(ApiStatus));
            if (IsApiEnabled)
            {
                IsApiEnabled = false;
            }
        };
    }

    partial void OnSystemPromptChanged(string value)
    {
        _chatViewModel.SystemPrompt = value;
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        _themeService.SetThemeAsync(value ? "Dark" : "Light");
    }

    async partial void OnIsApiEnabledChanged(bool value)
    {
        if (value)
        {
            await _apiService.StartAsync(ApiPort);
            OnPropertyChanged(nameof(ApiStatus));
        }
        else
        {
            await _apiService.StopAsync();
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
            _hardwareService.SetSelectedBackend(value);
            UpdateBackendStatus();
        }
    }

    private void UpdateBackendStatus()
    {
        BackendStatus = SelectedBackend switch
        {
            ExecutionBackend.Cpu => "CPU mode: Compatible with all systems.",
            ExecutionBackend.Cuda => Hardware?.HasCuda == true
                ? "CUDA: NVIDIA GPU acceleration enabled."
                : "CUDA not available.",
            ExecutionBackend.Vulkan => Hardware?.HasVulkan == true
                ? "Vulkan: GPU acceleration enabled."
                : "Vulkan not available.",
            ExecutionBackend.Npu => Hardware?.HasNpu == true
                ? "NPU: Neural processing unit detected."
                : "NPU not available.",
            _ => "Select a backend"
        };
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
}
