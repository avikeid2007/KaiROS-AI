using System.IO;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using KaiROS.AI.Services;
using KaiROS.AI.ViewModels;

namespace KaiROS.AI;

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
            root.DataContext = viewModel;

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
}
