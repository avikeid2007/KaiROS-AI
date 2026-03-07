using System.IO;
using Microsoft.UI.Xaml;

namespace KaiROS.AI.WinUI.Services;

public interface IThemeService
{
    string CurrentTheme { get; }
    void SetTheme(string themeName, FrameworkElement? root = null);
    void LoadSavedTheme(FrameworkElement? root = null);
}

public class ThemeService : IThemeService
{
    private readonly string _settingsPath;

    public string CurrentTheme { get; private set; } = "Dark";

    public ThemeService()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _settingsPath = System.IO.Path.Combine(localAppData, "KaiROS.AI", "theme.txt");
    }

    /// <summary>
    /// Switches the app theme by setting RequestedTheme on the root element.
    /// All {ThemeResource} bindings — brushes, NavigationView backgrounds, WinUI controls —
    /// automatically resolve from the correct ThemeDictionary (Light/Dark) with no extra code.
    /// </summary>
    public void SetTheme(string themeName, FrameworkElement? root = null)
    {
        var isLight = themeName == "Light";

        if (root != null)
            root.RequestedTheme = isLight ? ElementTheme.Light : ElementTheme.Dark;

        CurrentTheme = themeName;

        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_settingsPath)!);
            File.WriteAllText(_settingsPath, themeName);
        }
        catch { /* Ignore save errors */ }
    }

    public void LoadSavedTheme(FrameworkElement? root = null)
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var savedTheme = File.ReadAllText(_settingsPath).Trim();
                SetTheme(savedTheme == "Light" ? "Light" : "Dark", root);
            }
        }
        catch { /* Ignore load errors */ }
    }
}
