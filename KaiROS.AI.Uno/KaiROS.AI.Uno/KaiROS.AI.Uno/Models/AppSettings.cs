namespace KaiROS.AI.Uno.Models;

/// <summary>
/// Application settings from configuration
/// </summary>
public class AppSettings
{
    public string ModelsDirectory { get; set; } = "Models";
    public string DefaultBackend { get; set; } = "Auto";
}
