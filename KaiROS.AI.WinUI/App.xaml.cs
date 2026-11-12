using System.IO;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using KaiROS.AI.WinUI.Services;
using KaiROS.AI.WinUI.ViewModels;
using KaiROS.AI.WinUI.Models;
using Sentry;

namespace KaiROS.AI.WinUI;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    // Expose services and current app for use throughout the app
    public static new App Current => (App)Application.Current;
    public IServiceProvider Services => _serviceProvider!;

    public async Task DisposeServicesAsync()
    {
        if (_serviceProvider == null) return;
        try
        {
            var apiService = _serviceProvider.GetService(typeof(IApiService)) as IApiService;
            if (apiService?.IsRunning == true)
                await apiService.StopAsync();
        }
        catch { }
        try
        {
            var modelManager = _serviceProvider.GetService(typeof(ModelManagerService)) as ModelManagerService;
            if (modelManager != null)
                await modelManager.UnloadModelAsync();
        }
        catch { }
        var sp = _serviceProvider;
        _serviceProvider = null;
        try { sp.Dispose(); } catch { }
        try { SentrySdk.Close(); } catch { }
    }

    // Baked in at build time via AssemblyMetadataAttribute from the KAIROS_SENTRY_DSN env var
    // (see csproj) so the real DSN never has to be committed to this public repo.
    private static string? GetSentryDsn() =>
        Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "SentryDsn" && !string.IsNullOrWhiteSpace(a.Value))
            ?.Value;

    // ── Crash log helpers ─────────────────────────────────────────────────
    internal static readonly string CrashLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KaiROS.AI", "crash.log");

    // Shared with FileLogger below (same target file) so concurrent writes from the UI
    // thread, AppDomain handler, and logging provider can't collide with a sharing IOException.
    internal static readonly object CrashLogLock = new();
    private const long MaxCrashLogBytes = 5 * 1024 * 1024; // 5 MB

    private static void RotateCrashLogIfTooLarge()
    {
        var info = new FileInfo(CrashLogPath);
        if (info.Exists && info.Length > MaxCrashLogBytes)
            File.WriteAllText(CrashLogPath, $"[{DateTimeOffset.Now:o}] --- log rotated (exceeded {MaxCrashLogBytes:N0} bytes) ---{Environment.NewLine}");
    }

    // Local forensic log only - does not report to Sentry. Call SentrySdk.CaptureException
    // explicitly at call sites that aren't already covered by Sentry's own default integrations
    // (AppDomain.UnhandledException and TaskScheduler.UnobservedTaskException are auto-captured
    // by the SDK once initialized, so doing it again here would double-report every crash).
    internal static void WriteCrashLog(string source, Exception? ex)
    {
        try
        {
            lock (CrashLogLock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);
                RotateCrashLogIfTooLarge();
                var entry = $"[{DateTimeOffset.Now:o}] UNHANDLED in {source}{Environment.NewLine}{ex}{Environment.NewLine}---{Environment.NewLine}";
                File.AppendAllText(CrashLogPath, entry);
            }
        }
        catch { }
    }

    // Lets the app survive an occasional UI-thread exception without masking a genuine crash loop.
    private static readonly TimeSpan UnhandledExceptionWindow = TimeSpan.FromMinutes(1);
    private const int MaxUnhandledExceptionsPerWindow = 10;
    private static int _unhandledExceptionCount;
    private static DateTime _unhandledExceptionWindowStart = DateTime.UtcNow;

    private static bool ShouldSuppressUnhandledException()
    {
        var now = DateTime.UtcNow;
        if (now - _unhandledExceptionWindowStart > UnhandledExceptionWindow)
        {
            _unhandledExceptionWindowStart = now;
            _unhandledExceptionCount = 0;
        }
        return ++_unhandledExceptionCount <= MaxUnhandledExceptionsPerWindow;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var sentryDsn = GetSentryDsn();
        if (sentryDsn != null)
        {
            try
            {
                SentrySdk.Init(o =>
                {
                    o.Dsn = sentryDsn;
                    o.Release = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
                    o.Environment = System.Diagnostics.Debugger.IsAttached ? "development" : "production";
                    o.TracesSampleRate = 0.0;
                    o.AutoSessionTracking = true;
                    // Persist events to disk so a fatal crash or offline device doesn't silently drop them.
                    o.CacheDirectoryPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "KaiROS.AI", "sentry-cache");
                });
            }
            catch (Exception initEx)
            {
                // A bad DSN or Sentry internal failure must never prevent app startup.
                WriteCrashLog("SentrySdk.Init", initEx);
            }
        }

        // Global crash handlers — fire in Release where no debugger is attached.
        // WinUI's Application.UnhandledException is not covered by Sentry's default
        // integrations, so it's the one case reported explicitly.
        UnhandledException += (_, e) =>
        {
            WriteCrashLog("Application.UnhandledException", e.Exception);
            try { SentrySdk.CaptureException(e.Exception); } catch { }
            // Keep the app alive unless it's crash-looping, instead of swallowing exceptions forever.
            e.Handled = ShouldSuppressUnhandledException();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            WriteCrashLog("AppDomain.UnhandledException", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            e.SetObserved();
            WriteCrashLog("TaskScheduler.UnobservedTaskException", e.Exception);
        };

        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Setup dependency injection
        var services = new ServiceCollection();
        ConfigureServices(services, configuration);
        _serviceProvider = services.BuildServiceProvider();

        // Create and activate main window
        // Theme loading happens inside MainWindow.OnFirstActivated via IThemeService.LoadSavedTheme()
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Activate();
    }

    private void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Configuration
        services.AddSingleton<IConfiguration>(configuration);

        // Get app settings - use LocalAppData root for relative paths (MSIX install folder is read-only)
        var appSettings = configuration.GetSection("AppSettings").Get<AppSettings>() ?? new AppSettings();
        var modelsDir = ResolveModelsDirectory(appSettings.ModelsDirectory);

        // Logging — file provider works in both Debug and Release without a debugger
        services.AddLogging(b =>
        {
            b.SetMinimumLevel(LogLevel.Debug);
            b.AddProvider(new FileLoggerProvider(CrashLogPath));
        });

        // Services
        services.AddSingleton<IDatabaseService, DatabaseService>();
        services.AddSingleton<IDownloadService>(sp => new DownloadService(modelsDir));
        services.AddSingleton<IHardwareDetectionService, HardwareDetectionService>();
        services.AddSingleton<ISessionService, SessionService>();
        services.AddSingleton<IExportService, ExportService>();
        services.AddSingleton<IDocumentService, DocumentService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IUserPreferencesService, UserPreferencesService>();
        services.AddSingleton<ModelManagerService>();
        services.AddSingleton<IModelManagerService>(sp => sp.GetRequiredService<ModelManagerService>());
        services.AddSingleton<ChatService>();
        services.AddSingleton<IChatService>(sp => sp.GetRequiredService<ChatService>());
        services.AddSingleton<IApiService, ApiService>();
        services.AddSingleton<IAgentService, AgentService>();
        services.AddSingleton<IWebSearchService, WebSearchService>();
        services.AddSingleton<IDeepResearchService, DeepResearchService>();

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

    private static string ResolveModelsDirectory(string? configuredPath)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appRoot = Path.Combine(localAppData, "KaiROS.AI");

        if (string.IsNullOrWhiteSpace(configuredPath))
            return Path.Combine(appRoot, "Models");

        if (Path.IsPathRooted(configuredPath))
            return configuredPath;

        return Path.Combine(appRoot, configuredPath);
    }
}

// ── Minimal file logger (no extra NuGet required) ──────────────────────────
internal sealed class FileLoggerProvider(string path) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new FileLogger(path, categoryName);
    public void Dispose() { }
}

internal sealed class FileLogger(string path, string category) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel level) => level >= LogLevel.Warning;
    public void Log<TState>(LogLevel level, EventId id, TState state, Exception? ex, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(level)) return;
        try
        {
            lock (App.CrashLogLock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var line = $"[{DateTimeOffset.Now:o}] [{level}] {category}: {formatter(state, ex)}";
                if (ex != null) line += $"{Environment.NewLine}{ex}";
                line += Environment.NewLine;
                File.AppendAllText(path, line);
            }
        }
        catch { }
    }
}
