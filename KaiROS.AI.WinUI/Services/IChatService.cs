using KaiROS.AI.WinUI.Models;

namespace KaiROS.AI.WinUI.Services;

public interface IChatService
{
    bool IsModelLoaded { get; }
    InferenceStats LastStats { get; }

    Task<string> GenerateResponseAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default);
    Task<string> GenerateResponseAsync(IEnumerable<ChatMessage> messages, bool useWebSearch, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> GenerateResponseStreamAsync(IEnumerable<ChatMessage> messages, string? imagePath = null, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> GenerateResponseStreamAsync(IEnumerable<ChatMessage> messages, bool useWebSearch, string? imagePath = null, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> GenerateResponseStreamAsync(IEnumerable<ChatMessage> messages, bool useWebSearch, string? sessionContext, string? ragContext, string? imagePath = null, CancellationToken cancellationToken = default);
    void ClearContext();

    event EventHandler<string>? TokenGenerated;
    event EventHandler<InferenceStats>? StatsUpdated;
}
