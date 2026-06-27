using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using KaiROS.AI.WinUI.Models;

namespace KaiROS.AI.WinUI.Services;

public class UserPreferencesService : IUserPreferencesService
{
    private static readonly string PreferencesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KaiROS.AI", "user_preferences.json");

    private UserPreferencesData _data;

    public UserPreferencesService()
    {
        _data = Load();
    }

    public ContextWindowOption ContextWindowPreference
    {
        get => _data.ContextWindowPreference;
        set { _data.ContextWindowPreference = value; Save(); }
    }

    public string SystemPrompt
    {
        get => _data.SystemPrompt;
        set { _data.SystemPrompt = value; Save(); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PreferencesPath)!);
            var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(PreferencesPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[KaiROS] Failed to save preferences: {ex.Message}");
        }
    }

    private static UserPreferencesData Load()
    {
        try
        {
            if (File.Exists(PreferencesPath))
            {
                var json = File.ReadAllText(PreferencesPath);
                return JsonSerializer.Deserialize<UserPreferencesData>(json) ?? new UserPreferencesData();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[KaiROS] Failed to load preferences: {ex.Message}");
        }
        return new UserPreferencesData();
    }

    private class UserPreferencesData
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ContextWindowOption ContextWindowPreference { get; set; } = ContextWindowOption.Auto;
        public string SystemPrompt { get; set; } = "You are a helpful, friendly AI assistant. Be concise and clear.";
    }
}
