using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KaiROS.AI.WinUI.Models;
using KaiROS.AI.WinUI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using System.Collections.ObjectModel;
using System.IO;

namespace KaiROS.AI.WinUI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IModelManagerService _modelManager;
    private readonly IHardwareDetectionService _hardwareService;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly ILogger<MainViewModel> _logger;

    [ObservableProperty]
    public partial ViewModelBase? CurrentView { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Ready";

    [ObservableProperty]
    public partial string HardwareInfo { get; set; } = "Detecting hardware...";

    [ObservableProperty]
    public partial string? ActiveModelName { get; set; }

    [ObservableProperty]
    public partial HardwareInfo? Hardware { get; set; }

    [ObservableProperty]
    public partial int SelectedNavigationIndex { get; set; }

    public ModelCatalogViewModel CatalogViewModel { get; }
    public ChatViewModel ChatViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }
    public DocumentViewModel DocumentViewModel { get; }

    public MainViewModel(
        IModelManagerService modelManager,
        IHardwareDetectionService hardwareService,
        ModelCatalogViewModel catalogViewModel,
        ChatViewModel chatViewModel,
        SettingsViewModel settingsViewModel,
        DocumentViewModel documentViewModel,
        ILogger<MainViewModel> logger)
    {
        _modelManager = modelManager;
        _hardwareService = hardwareService;
        _logger = logger;
        CatalogViewModel = catalogViewModel;
        ChatViewModel = chatViewModel;
        SettingsViewModel = settingsViewModel;
        DocumentViewModel = documentViewModel;
        // Capture UI thread's DispatcherQueue for cross-thread UI updates
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _modelManager.ModelLoaded += (s, m) =>
        {
            // All property writes must happen on the UI thread to avoid RPC_E_WRONG_THREAD
            _dispatcherQueue.TryEnqueue(() =>
            {
                ActiveModelName = m.DisplayName;
                StatusText = $"Model loaded: {m.DisplayName}";
                SelectedNavigationIndex = 1;
            });
        };

        _modelManager.ModelUnloaded += (s, e) =>
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                ActiveModelName = null;
                StatusText = "Model unloaded";
            });
        };
    }

    public override async Task InitializeAsync()
    {
        IsLoading = true;
        StatusText = "Initializing...";

        try
        {
            // Detect hardware
            Hardware = await _hardwareService.DetectHardwareAsync();
            HardwareInfo = Hardware.StatusMessage;

            // Initialize model catalog
            await _modelManager.InitializeAsync();

            // Initialize child view models
            await CatalogViewModel.InitializeAsync();
            await ChatViewModel.InitializeAsync();
            await SettingsViewModel.InitializeAsync();
            await DocumentViewModel.InitializeAsync();

            // If a model was auto-loaded, SelectedNavigationIndex would be 1 (Chat)
            // But we shouldn't overwrite it blindly.
            if (_modelManager.ActiveModel != null)
            {
                SelectedNavigationIndex = 1; // Ensure UI reflects this
                CurrentView = ChatViewModel;
            }
            else
            {
                SelectedNavigationIndex = 0;
                CurrentView = CatalogViewModel;
            }

            StatusText = "Ready";
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "InitializeAsync crashed at {Time}", DateTimeOffset.Now);

            // Write persistent crash log to LocalAppData\KaiROS.AI\crash.log
            try
            {
                var entry = $"[{DateTimeOffset.Now:o}] CRASH in MainViewModel.InitializeAsync{Environment.NewLine}{ex}{Environment.NewLine}---{Environment.NewLine}";
                await File.AppendAllTextAsync(App.CrashLogPath, entry);
            }
            catch { /* never swallow original exception */ }

            ErrorMessage = ex.Message;
            StatusText = "Initialization failed";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedNavigationIndexChanged(int value)
    {
        CurrentView = value switch
        {
            0 => CatalogViewModel,
            1 => ChatViewModel,
            2 => DocumentViewModel,
            3 => SettingsViewModel,
            _ => CatalogViewModel
        };
    }

    [RelayCommand]
    private void NavigateToCatalog() => SelectedNavigationIndex = 0;

    [RelayCommand]
    private void NavigateToChat() => SelectedNavigationIndex = 1;

    [RelayCommand]
    private void NavigateToDocuments() => SelectedNavigationIndex = 2;

    [RelayCommand]
    private void NavigateToSettings() => SelectedNavigationIndex = 3;
}
