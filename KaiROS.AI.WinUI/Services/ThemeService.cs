using System.IO;
using Microsoft.UI.Xaml;

namespace KaiROS.AI.WinUI.Services;

public interface IThemeService
{
    /// <summary>The currently active theme name ("Dark" or "Light").</summary>
    string CurrentTheme { get; }

    /// <summary>
    /// Fires when the theme changes. The argument is the new ElementTheme.
    /// MainWindow subscribes and applies RequestedTheme to the NavigationView.
    /// </summary>
    event EventHandler<ElementTheme>? ThemeChanged;

    /// <summary>Change theme and persist preference.</summary>
    void SetTheme(string themeName);

    /// <summary>Read persisted preference and raise ThemeChanged so the UI updates.</summary>
    void LoadSavedTheme();
}

public class ThemeService : IThemeService
{
    private readonly string _settingsPath;

    public string CurrentTheme { get; private set; } = "Dark";

    public event EventHandler<ElementTheme>? ThemeChanged;

    public ThemeService()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _settingsPath = Path.Combine(localAppData, "KaiROS.AI", "theme.txt");
    }

    public void SetTheme(string themeName)
    {
        CurrentTheme = themeName == "Light" ? "Light" : "Dark";
        ThemeChanged?.Invoke(this, CurrentTheme == "Light" ? ElementTheme.Light : ElementTheme.Dark);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            File.WriteAllText(_settingsPath, CurrentTheme);
        }
        catch { /* ignore */ }
    }

    public void LoadSavedTheme()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var saved = File.ReadAllText(_settingsPath).Trim();
                SetTheme(saved == "Light" ? "Light" : "Dark");
                return;
            }
        }
        catch { /* ignore */ }

        // No saved preference — emit the default (Dark) so subscribers initialise correctly
        ThemeChanged?.Invoke(this, ElementTheme.Dark);
    }
}
