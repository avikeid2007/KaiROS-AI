using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KaiROS.AI.WinUI;
using KaiROS.AI.WinUI.Models;
using KaiROS.AI.WinUI.Services;
using Microsoft.Extensions.DependencyInjection;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Collections.ObjectModel;

namespace KaiROS.AI.WinUI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IHardwareDetectionService _hardwareService;
    private readonly IModelManagerService _modelManager;
    private readonly ChatViewModel _chatViewModel;
    private readonly IThemeService _themeService;
    private readonly IApiService _apiService;

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

    // API can only be enabled when a model is loaded
    public bool CanEnableApi => _modelManager.ActiveModel != null;

    // System tray only enabled when API is running
    public bool IsMinimizeToTrayEnabled => IsApiEnabled && _apiService.IsRunning;

    public string ApiStatus => _apiService.IsRunning
        ? $"Running on http://localhost:{_apiService.Port}/"
        : CanEnableApi ? "Stopped (ready to start)" : "Disabled (load a model first)";

    public SettingsViewModel(IHardwareDetectionService hardwareService, IModelManagerService modelManager, ChatViewModel chatViewModel, IThemeService themeService, IApiService apiService)
    {
        _hardwareService = hardwareService;
        _modelManager = modelManager;
        _chatViewModel = chatViewModel;
        _themeService = themeService;
        _apiService = apiService;

        // Initialize system prompt from ChatViewModel
        SystemPrompt = chatViewModel.SystemPrompt;

        // Initialize theme from service
        IsDarkTheme = _themeService.CurrentTheme == "Dark";

        // Initialize API status
        IsApiEnabled = _apiService.IsRunning;

        // Subscribe to model events to update CanEnableApi
        _modelManager.ModelLoaded += (s, e) =>
        {
            OnPropertyChanged(nameof(CanEnableApi));
            OnPropertyChanged(nameof(ApiStatus));
        };
        _modelManager.ModelUnloaded += (s, e) =>
        {
            OnPropertyChanged(nameof(CanEnableApi));
            OnPropertyChanged(nameof(ApiStatus));
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
}
