using System.IO;

namespace KaiROS.AI.WinUI.Helpers;

/// <summary>
/// Thread-safe file logger that works in both Debug and Release builds.
/// Writes to %LocalAppData%\KaiROS.AI\logs\kairos.log
/// </summary>
public static class AppLogger
{
    private static readonly string LogPath;
    private static readonly Lock _lock = new();

    static AppLogger()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KaiROS.AI", "logs");
        Directory.CreateDirectory(dir);
        LogPath = Path.Combine(dir, "kairos.log");

        // Rotate: keep last 200 KB to avoid unbounded growth
        try
        {
            if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 200_000)
                File.Delete(LogPath);
        }
        catch { }
    }

    public static void Log(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
        lock (_lock)
        {
            try { File.AppendAllText(LogPath, line + Environment.NewLine); } catch { }
        }
        System.Diagnostics.Trace.WriteLine(line);
    }

    public static void LogException(string context, Exception ex)
    {
        Log($"[ERROR] {context}: {ex.GetType().Name}: {ex.Message}");
        Log($"[STACK] {ex.StackTrace}");
        if (ex.InnerException is not null)
            Log($"[INNER] {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
    }

    public static string GetLogPath() => LogPath;
}
