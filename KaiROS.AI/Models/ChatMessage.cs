namespace KaiROS.AI.Models;

/// <summary>
/// Represents a chat message in the conversation
/// </summary>
public class ChatMessage
{
    public ChatRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool IsStreaming { get; set; }

    /// <summary>Optional: absolute path to an image attached by the user.</summary>
    public string? AttachedImagePath { get; set; }

    public static ChatMessage System(string content) => new() { Role = ChatRole.System, Content = content };
    public static ChatMessage User(string content) => new() { Role = ChatRole.User, Content = content };
    public static ChatMessage UserWithImage(string content, string imagePath) => new() { Role = ChatRole.User, Content = content, AttachedImagePath = imagePath };
    public static ChatMessage Assistant(string content) => new() { Role = ChatRole.Assistant, Content = content };
}

public enum ChatRole
{
    System,
    User,
    Assistant
}
