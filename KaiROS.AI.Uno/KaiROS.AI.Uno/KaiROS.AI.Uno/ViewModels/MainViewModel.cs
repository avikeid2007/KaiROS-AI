using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KaiROS.AI.Uno.Models;
using KaiROS.AI.Uno.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace KaiROS.AI.Uno.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IModelManagerService _modelManager;
    private readonly IHardwareDetectionService _hardwareService;
    private readonly IDatabaseService _databaseService;

    [ObservableProperty]
    private ViewModelBase? _currentView;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _hardwareInfo = "Detecting hardware...";

    [ObservableProperty]
    private string? _activeModelName;

    [ObservableProperty]
    private HardwareInfo? _hardware;

    [ObservableProperty]
    private int _selectedNavigationIndex;

    public ModelCatalogViewModel CatalogViewModel { get; }
    public ChatViewModel ChatViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }
    public DocumentViewModel DocumentViewModel { get; }

    public MainViewModel(
        IModelManagerService modelManager,
        IHardwareDetectionService hardwareService,
        IDatabaseService databaseService,
        ModelCatalogViewModel catalogViewModel,
        ChatViewModel chatViewModel,
        SettingsViewModel settingsViewModel,
        DocumentViewModel documentViewModel)
    {
        _modelManager = modelManager;
        _hardwareService = hardwareService;
        _databaseService = databaseService;
        CatalogViewModel = catalogViewModel;
        ChatViewModel = chatViewModel;
        SettingsViewModel = settingsViewModel;
        DocumentViewModel = documentViewModel;

        _modelManager.ModelLoaded += (s, m) =>
        {
            ActiveModelName = m.DisplayName;
            StatusText = $"Model loaded: {m.DisplayName}";
            SelectedNavigationIndex = 1;
        };

        _modelManager.ModelUnloaded += (s, e) =>
        {
            ActiveModelName = null;
            StatusText = "Model unloaded";
        };
    }

    public override async Task InitializeAsync()
    {
        IsLoading = true;
        StatusText = "Initializing...";
        Debug.WriteLine("MainViewModel.InitializeAsync started");

        try
        {
            // Initialize database first
            StatusText = "Initializing database...";
            Debug.WriteLine("Initializing database...");
            await _databaseService.InitializeAsync();
            Debug.WriteLine("Database initialized");

            // Detect hardware
            StatusText = "Detecting hardware...";
            Debug.WriteLine("Detecting hardware...");
            Hardware = await _hardwareService.DetectHardwareAsync();
            HardwareInfo = Hardware.StatusMessage;
            Debug.WriteLine($"Hardware detected: {HardwareInfo}");

            // Initialize model catalog
            StatusText = "Loading models...";
            Debug.WriteLine("Loading models...");
            await _modelManager.InitializeAsync();
            Debug.WriteLine($"Models loaded: {_modelManager.Models.Count}");

            // Initialize child view models
            Debug.WriteLine("Initializing CatalogViewModel...");
            await CatalogViewModel.InitializeAsync();
            Debug.WriteLine("Initializing ChatViewModel...");
            await ChatViewModel.InitializeAsync();
            Debug.WriteLine("Initializing SettingsViewModel...");
            await SettingsViewModel.InitializeAsync();
            Debug.WriteLine("Initializing DocumentViewModel...");
            await DocumentViewModel.InitializeAsync();
            Debug.WriteLine("All ViewModels initialized");

            if (_modelManager.ActiveModel != null)
            {
                SelectedNavigationIndex = 1;
                CurrentView = ChatViewModel;
            }
            else
            {
                SelectedNavigationIndex = 0;
                CurrentView = CatalogViewModel;
            }

            StatusText = "Ready";
            Debug.WriteLine("MainViewModel.InitializeAsync completed successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"MainViewModel.InitializeAsync error: {ex}");
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
