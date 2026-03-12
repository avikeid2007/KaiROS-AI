using KaiROS.AI.Uno.Models;

namespace KaiROS.AI.Uno.Services;

public interface IRagSourceProvider
{
    RagSourceType SupportedType { get; }
    Task<string> GetContentAsync(RagSource source);
}
