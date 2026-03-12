using System.Diagnostics.CodeAnalysis;
using KaiROS.AI.Uno.ViewModels;
using KaiROS.AI.Uno.Services;
using KaiROS.AI.Uno.Presentation;
using Uno.Resizetizer;
using System.Diagnostics;

namespace KaiROS.AI.Uno;

public partial class App : Application
{
    private IHost? _host;

    /// <summary>
    /// Gets the current App instance
    /// </summary>
    public static new App Current => (App)Application.Current;

    /// <summary>
    /// Gets the service provider for DI
    /// </summary>
    public IServiceProvider Services => _host?.Services ?? throw new InvalidOperationException("Host not initialized");

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        this.InitializeComponent();
    }

    protected Window? MainWindow { get; private set; }

    [SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Uno.Extensions APIs are used in a way that is safe for trimming in this template context.")]
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Debug.WriteLine("App.OnLaunched started");
        
        var builder = this.CreateBuilder(args)
            .Configure(host => host
#if DEBUG
                // Switch to Development environment when running in DEBUG
                .UseEnvironment(Environments.Development)
#endif
                .UseLogging(configure: (context, logBuilder) =>
                {
                    logBuilder
                        .SetMinimumLevel(
                            context.HostingEnvironment.IsDevelopment() ?
                                LogLevel.Information :
                                LogLevel.Warning)
                        .CoreLogLevel(LogLevel.Warning);
                }, enableUnoLogging: true)
                .ConfigureServices((context, services) =>
                {
                    // Register services
                    services.AddSingleton<IHardwareDetectionService, HardwareDetectionService>();
                    services.AddSingleton<IDatabaseService, DatabaseService>();
                    services.AddSingleton<IDownloadService, DownloadService>();
                    services.AddSingleton<IModelManagerService, ModelManagerService>();
                    services.AddSingleton<IChatService, ChatService>();
                    services.AddSingleton<IRaasService, RaasService>();
                    services.AddSingleton<ISessionService, SessionService>();
                    services.AddSingleton<IExportService, ExportService>();
                    services.AddSingleton<IDocumentService, DocumentService>();
                    services.AddSingleton<IApiService, ApiService>();
                    services.AddSingleton<IWebSearchService, WebSearchService>();
                    services.AddSingleton<IKairosThemeService, KairosThemeService>();

                    // Register ViewModels
                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<ModelCatalogViewModel>();
                    services.AddSingleton<ChatViewModel>();
                    services.AddSingleton<SettingsViewModel>();
                    services.AddSingleton<DocumentViewModel>();
                })
            );

        Debug.WriteLine("Builder configured");

        MainWindow = builder.Window;

#if DEBUG
        MainWindow.UseStudio();
#endif
        MainWindow.SetWindowIcon();

        // Build the host
        Debug.WriteLine("Building host...");
        _host = builder.Build();
        Debug.WriteLine("Host built successfully");

        // Create MainPage with ViewModel from DI
        var mainPage = new MainPage();
        var viewModel = _host.Services.GetRequiredService<MainViewModel>();
        mainPage.DataContext = viewModel;
        Debug.WriteLine("MainPage created with ViewModel");

        MainWindow.Content = mainPage;
        MainWindow.Activate();
        Debug.WriteLine("Window activated");
    }
}
