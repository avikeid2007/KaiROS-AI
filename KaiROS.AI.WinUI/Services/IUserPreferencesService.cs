using KaiROS.AI.WinUI.Models;

namespace KaiROS.AI.WinUI.Services;

public interface IUserPreferencesService
{
    ContextWindowOption ContextWindowPreference { get; set; }
    string SystemPrompt { get; set; }
    bool IsVoiceInputEnabled { get; set; }
    string WhisperModelSize { get; set; }
    string SelectedInputDevice { get; set; }
    bool IsAutoSendEnabled { get; set; }
    double SilenceDurationSeconds { get; set; }
    bool IsTtsEnabled { get; set; }
    string SelectedVoiceId { get; set; }
    double TtsSpeechRate { get; set; }
    double TtsVolume { get; set; }
    bool IsAutoReadEnabled { get; set; }
    void Save();
}
