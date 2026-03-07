using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using KaiROS.AI.WinUI.Services;
using KaiROS.AI.WinUI.ViewModels;
using KaiROS.AI.WinUI.Models;

namespace KaiROS.AI.WinUI;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    // Expose services and current app for use throughout the app
    public static new App Current => (App)Application.Current;
    public IServiceProvider Services => _serviceProvider!;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Setup dependency injection
        var services = new ServiceCollection();
        ConfigureServices(services, configuration);
        _serviceProvider = services.BuildServiceProvider();

        // Load saved theme preference at startup
        _serviceProvider.GetRequiredService<IThemeService>().LoadSavedTheme();

        // Create and show main window
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Activate();
    }

    private void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Configuration
        services.AddSingleton<IConfiguration>(configuration);

        // Get app settings - Use LocalAppData for MSIX compatibility (installation folder is read-only)
        var appSettings = configuration.GetSection("AppSettings").Get<AppSettings>() ?? new AppSettings();
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var modelsDir = Path.Combine(localAppData, "KaiROS.AI", "Models");

        // Services
        services.AddSingleton<IDatabaseService, DatabaseService>();
        services.AddSingleton<IDownloadService>(sp => new DownloadService(modelsDir));
        services.AddSingleton<IHardwareDetectionService, HardwareDetectionService>();
        services.AddSingleton<ISessionService, SessionService>();
        services.AddSingleton<IExportService, ExportService>();
        services.AddSingleton<IDocumentService, DocumentService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<ModelManagerService>();
        services.AddSingleton<IModelManagerService>(sp => sp.GetRequiredService<ModelManagerService>());
        services.AddSingleton<ChatService>();
        services.AddSingleton<IChatService>(sp => sp.GetRequiredService<ChatService>());
        services.AddSingleton<IApiService, ApiService>();
        services.AddSingleton<IWebSearchService, WebSearchService>();

        // RaaS Services
        services.AddSingleton<IRagSourceProvider, FileSourceProvider>();
        services.AddSingleton<IRagSourceProvider, WebSourceProvider>();
        services.AddSingleton<IRaasService, RaasService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<ModelCatalogViewModel>();
        services.AddSingleton<ChatViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<DocumentViewModel>();

        // Views
        services.AddSingleton<MainWindow>();
    }
}
