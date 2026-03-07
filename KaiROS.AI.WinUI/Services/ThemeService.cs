using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

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

    public void SetTheme(string themeName, FrameworkElement? root = null)
    {
        var app = Microsoft.UI.Xaml.Application.Current;
        if (app == null) return;

        var isLight = themeName == "Light";

        // Flip WinUI ThemeResource tokens by setting RequestedTheme on the content root
        if (root != null)
            root.RequestedTheme = isLight ? ElementTheme.Light : ElementTheme.Dark;

        UpdateBrush(app, "SurfaceBrush",      isLight ? Color.FromArgb(255, 255, 255, 255) : Color.FromArgb(255, 26, 26, 46));
        UpdateBrush(app, "SurfaceLightBrush", isLight ? Color.FromArgb(255, 241, 245, 249) : Color.FromArgb(255, 37, 37, 58));
        UpdateBrush(app, "CardBrush",         isLight ? Color.FromArgb(255, 255, 255, 255) : Color.FromArgb(255, 22, 22, 42));
        UpdateBrush(app, "BorderBrush",       isLight ? Color.FromArgb(255, 226, 232, 240) : Color.FromArgb(255, 45, 45, 68));
        UpdateBrush(app, "TextPrimaryBrush",  isLight ? Color.FromArgb(255, 30, 41, 59)   : Color.FromArgb(255, 249, 250, 251));
        UpdateBrush(app, "TextSecondaryBrush",isLight ? Color.FromArgb(255, 100, 116, 139): Color.FromArgb(255, 156, 163, 175));
        UpdateBrush(app, "TextMutedBrush",    isLight ? Color.FromArgb(255, 148, 163, 184): Color.FromArgb(255, 107, 114, 128));

        CurrentTheme = themeName;

        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_settingsPath)!);
            File.WriteAllText(_settingsPath, themeName);
        }
        catch { /* Ignore save errors */ }
    }

    private static void UpdateBrush(Microsoft.UI.Xaml.Application app, string key, Color color)
    {
        // Find the existing brush instance in MergedDictionaries so StaticResource bindings update
        object existing = null;
        if (app.Resources.TryGetValue(key, out var val))
        {
            existing = val;
        }
        else
        {
            foreach (var dict in app.Resources.MergedDictionaries)
            {
                if (dict.TryGetValue(key, out var dictVal))
                {
                    existing = dictVal;
                    break;
                }
            }
        }

        if (existing is SolidColorBrush brush)
        {
            brush.Color = color;
        }
        else
        {
            app.Resources[key] = new SolidColorBrush(color);
        }
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
