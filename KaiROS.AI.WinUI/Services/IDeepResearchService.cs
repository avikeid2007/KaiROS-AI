using System.Collections.Generic;
using System.Threading;
using KaiROS.AI.WinUI.Models;

namespace KaiROS.AI.WinUI.Services;

public interface IDeepResearchService
{
    IAsyncEnumerable<ResearchProgress> RunResearchAsync(string query, ResearchOptions options, CancellationToken ct);
}
