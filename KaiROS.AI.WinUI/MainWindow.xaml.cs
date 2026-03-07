using System.IO;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using WinRT;
using KaiROS.AI.WinUI.Services;
using KaiROS.AI.WinUI.ViewModels;

namespace KaiROS.AI.WinUI;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IApiService _apiService;
    private bool _isExiting = false;
    private bool _initialized = false;
    private AppWindow? _appWindow;

    public MainWindow(MainViewModel viewModel, IApiService apiService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _apiService = apiService;

        // WinUI 3: set DataContext on the root FrameworkElement (Window has no DataContext itself)
        if (Content is FrameworkElement root)
        {
            root.DataContext = viewModel;
            // Expose root for runtime theme switching via ThemeService
            App.Current.MainWindowRoot = root;
        }

        // Extend content under the title bar for Mica to show through
        ExtendsContentIntoTitleBar = true;

        // Apply Mica backdrop
        TrySetMicaBackdrop();

        _appWindow = this.AppWindow;

        // Set window icon
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(iconPath))
                _appWindow.SetIcon(iconPath);
        }
        catch { /* Ignore icon errors */ }

        // Center window (replaces WPF WindowStartupLocation="CenterScreen")
        var display = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        const int w = 1200, h = 800;
        _appWindow.Move(new PointInt32(
            display.WorkArea.X + (display.WorkArea.Width - w) / 2,
            display.WorkArea.Y + (display.WorkArea.Height - h) / 2));
        _appWindow.Resize(new SizeInt32(w, h));

        // AppWindow.Closing replaces WPF Window.Closing (supports cancellation since WinAppSDK 1.1)
        _appWindow.Closing += AppWindow_Closing;

        // Initialize ViewModel on first activation
        Activated += OnFirstActivated;

        // Sync NavView when ViewModel programmatically changes navigation (tray commands, etc.)
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedNavigationIndex))
                SyncNavViewSelection(_viewModel.SelectedNavigationIndex);
        };

        // NavigationView backgrounds use ThemeDictionaries in XAML — no code-behind needed.
    }

    private async void OnFirstActivated(object sender, WindowActivatedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;
        Activated -= OnFirstActivated;
        await _viewModel.InitializeAsync();
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        // Only minimize to tray if API is running, otherwise close normally
        if (!_isExiting && _apiService.IsRunning)
        {
            args.Cancel = true;
            MinimizeToTray();
        }
        else
        {
            TrayIcon.Dispose();
            App.Current.Services.GetService(typeof(IServiceProvider)); // trigger DI disposal
        }
    }

    private void MinimizeToTray()
    {
        if (_appWindow.Presenter is OverlappedPresenter presenter)
            presenter.Minimize();
        _appWindow.Hide();
        TrayIcon.Visibility = Visibility.Visible;
    }

    private void RestoreWindow()
    {
        _appWindow.Show();
        if (_appWindow.Presenter is OverlappedPresenter presenter)
            presenter.Restore();
        this.Activate();
        TrayIcon.Visibility = Visibility.Collapsed;
    }

    private void TrayMenu_NewChat(object sender, object e)
    {
        RestoreWindow();
        _viewModel.NavigateToChatCommand.Execute(null);
        (_viewModel.CurrentView as ChatViewModel)?.NewSessionCommand.Execute(null);
    }

    private void TrayMenu_Settings(object sender, object e)
    {
        RestoreWindow();
        _viewModel.NavigateToSettingsCommand.Execute(null);
    }

    private void TrayMenu_Restore(object sender, object e)
    {
        RestoreWindow();
    }

    private void TrayMenu_Exit(object sender, object e)
    {
        _isExiting = true;
        this.Close();
    }

    private void TrySetMicaBackdrop()
    {
        if (!MicaController.IsSupported()) return;

        var config = new SystemBackdropConfiguration { IsInputActive = true };

        ((FrameworkElement)Content).ActualThemeChanged += (s, _) =>
        {
            config.Theme = ((FrameworkElement)Content).ActualTheme switch
            {
                ElementTheme.Dark  => SystemBackdropTheme.Dark,
                ElementTheme.Light => SystemBackdropTheme.Light,
                _                  => SystemBackdropTheme.Default
            };
        };

        var micaController = new MicaController();
        micaController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
        micaController.SetSystemBackdropConfiguration(config);
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag
            && int.TryParse(tag, out int index))
        {
            _viewModel.SelectedNavigationIndex = index;
        }
    }

    private void SyncNavViewSelection(int index)
    {
        var allItems = NavView.MenuItems.OfType<NavigationViewItem>()
            .Concat(NavView.FooterMenuItems.OfType<NavigationViewItem>());
        foreach (var item in allItems)
        {
            if (item.Tag is string tag && int.TryParse(tag, out int i) && i == index)
            {
                NavView.SelectedItem = item;
                return;
            }
        }
    }
}

