using KaiROS.AI.WinUI.Models;

namespace KaiROS.AI.WinUI.Services;

public interface IUserPreferencesService
{
    ContextWindowOption ContextWindowPreference { get; set; }
    string SystemPrompt { get; set; }
    void Save();
}
