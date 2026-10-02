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

    public bool IsVoiceInputEnabled
    {
        get => _data.IsVoiceInputEnabled;
        set { _data.IsVoiceInputEnabled = value; Save(); }
    }

    public string WhisperModelSize
    {
        get => _data.WhisperModelSize;
        set { _data.WhisperModelSize = value; Save(); }
    }

    public string SelectedInputDevice
    {
        get => _data.SelectedInputDevice;
        set { _data.SelectedInputDevice = value; Save(); }
    }

    public bool IsAutoSendEnabled
    {
        get => _data.IsAutoSendEnabled;
        set { _data.IsAutoSendEnabled = value; Save(); }
    }

    public double SilenceDurationSeconds
    {
        get => _data.SilenceDurationSeconds;
        set { _data.SilenceDurationSeconds = value; Save(); }
    }

    public bool IsTtsEnabled
    {
        get => _data.IsTtsEnabled;
        set { _data.IsTtsEnabled = value; Save(); }
    }

    public string SelectedVoiceId
    {
        get => _data.SelectedVoiceId;
        set { _data.SelectedVoiceId = value; Save(); }
    }

    public double TtsSpeechRate
    {
        get => _data.TtsSpeechRate;
        set { _data.TtsSpeechRate = value; Save(); }
    }

    public double TtsVolume
    {
        get => _data.TtsVolume;
        set { _data.TtsVolume = value; Save(); }
    }

    public bool IsAutoReadEnabled
    {
        get => _data.IsAutoReadEnabled;
        set { _data.IsAutoReadEnabled = value; Save(); }
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
        public bool IsVoiceInputEnabled { get; set; } = false;
        public string WhisperModelSize { get; set; } = "base";
        public string SelectedInputDevice { get; set; } = string.Empty;
        public bool IsAutoSendEnabled { get; set; } = false;
        public double SilenceDurationSeconds { get; set; } = 2.0;
        public bool IsTtsEnabled { get; set; } = false;
        public string SelectedVoiceId { get; set; } = string.Empty;
        public double TtsSpeechRate { get; set; } = 1.0;
        public double TtsVolume { get; set; } = 1.0;
        public bool IsAutoReadEnabled { get; set; } = false;
    }
}
