using KaiROS.AI.WinUI.Models;

namespace KaiROS.AI.WinUI.Services;

public interface IRagSourceProvider
{
    RagSourceType SupportedType { get; }
    Task<string> GetContentAsync(RagSource source);
}
