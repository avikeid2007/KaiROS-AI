using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KaiROS.AI.WinUI.Helpers;
using KaiROS.AI.WinUI.Models;
using KaiROS.AI.WinUI.Services;
using Microsoft.UI.Dispatching;
using System.Collections.ObjectModel;

namespace KaiROS.AI.WinUI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IModelManagerService _modelManager;
    private readonly IHardwareDetectionService _hardwareService;
    private readonly DispatcherQueue _dispatcherQueue;

    [ObservableProperty]
    public partial ViewModelBase? CurrentView { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    public partial string HardwareInfo { get; set; }

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
        DocumentViewModel documentViewModel)
    {
        _modelManager = modelManager;
        _hardwareService = hardwareService;
        CatalogViewModel = catalogViewModel;
        ChatViewModel = chatViewModel;
        SettingsViewModel = settingsViewModel;
        DocumentViewModel = documentViewModel;
        // Set default values (can't use field initializers with partial properties)
        StatusText = "Ready";
        HardwareInfo = "Detecting hardware...";
        // Capture UI thread's DispatcherQueue for cross-thread UI updates
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _modelManager.ModelLoaded += (s, m) =>
        {
            // Update UI state
            ActiveModelName = m.DisplayName;
            StatusText = $"Model loaded: {m.DisplayName}";

            // Auto-navigate to Chat whenever a model is loaded (including auto-load on startup)
            // Use DispatcherQueue to ensure UI update if event comes from background thread
            _dispatcherQueue.TryEnqueue(() =>
            {
                SelectedNavigationIndex = 1;
            });
        };

        _modelManager.ModelUnloaded += (s, e) =>
        {
            ActiveModelName = null;
            StatusText = "Model unloaded";
        };
    }

    public override async Task InitializeAsync()
    {
        AppLogger.Log("[MainViewModel] InitializeAsync — start");
        IsLoading = true;
        StatusText = "Initializing...";

        try
        {
            AppLogger.Log("[MainViewModel] Detecting hardware...");
            Hardware = await _hardwareService.DetectHardwareAsync();
            HardwareInfo = Hardware.StatusMessage;
            AppLogger.Log($"[MainViewModel] Hardware detected: {Hardware.StatusMessage}");

            AppLogger.Log("[MainViewModel] Initializing ModelManager...");
            await _modelManager.InitializeAsync();
            AppLogger.Log("[MainViewModel] ModelManager initialized");

            AppLogger.Log("[MainViewModel] Initializing CatalogViewModel...");
            await InitChildVmAsync("CatalogViewModel", CatalogViewModel.InitializeAsync);

            AppLogger.Log("[MainViewModel] Initializing ChatViewModel...");
            await InitChildVmAsync("ChatViewModel", ChatViewModel.InitializeAsync);

            AppLogger.Log("[MainViewModel] Initializing SettingsViewModel...");
            await InitChildVmAsync("SettingsViewModel", SettingsViewModel.InitializeAsync);

            AppLogger.Log("[MainViewModel] Initializing DocumentViewModel...");
            await InitChildVmAsync("DocumentViewModel", DocumentViewModel.InitializeAsync);

            if (_modelManager.ActiveModel != null)
            {
                AppLogger.Log($"[MainViewModel] Active model: {_modelManager.ActiveModel.DisplayName} — navigating to Chat");
                SelectedNavigationIndex = 1;
                CurrentView = ChatViewModel;
            }
            else
            {
                AppLogger.Log("[MainViewModel] No active model — navigating to Catalog");
                SelectedNavigationIndex = 0;
                CurrentView = CatalogViewModel;
            }

            StatusText = "Ready";
            AppLogger.Log("[MainViewModel] InitializeAsync — complete");
        }
        catch (Exception ex)
        {
            AppLogger.LogException("[MainViewModel] InitializeAsync", ex);
            ErrorMessage = ex.Message;
            StatusText = "Initialization failed";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static async Task InitChildVmAsync(string name, Func<Task> init)
    {
        try
        {
            await init();
            AppLogger.Log($"[MainViewModel] {name} — OK");
        }
        catch (Exception ex)
        {
            AppLogger.LogException($"[MainViewModel] {name}", ex);
            throw; // re-throw so outer catch surfaces it
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
