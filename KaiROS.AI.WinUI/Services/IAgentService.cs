using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KaiROS.AI.WinUI.Models;
using KaiROS.AI.WinUI.Services.Tools;

namespace KaiROS.AI.WinUI.Services;

public interface IAgentService
{
    bool IsFileReaderEnabled { get; set; }
    bool IsFileWriterEnabled { get; set; }
    bool IsWebFetchEnabled { get; set; }
    bool IsCalculatorEnabled { get; set; }
    bool IsSystemInfoEnabled { get; set; }
    bool IsDateTimeEnabled { get; set; }
    bool IsClipboardEnabled { get; set; }

    Func<string, string, Task<bool>>? ConfirmationCallback { get; set; }

    List<ITool> GetEnabledTools();
    IAsyncEnumerable<AgentStep> RunAgentAsync(string userRequest, List<ITool> enabledTools, CancellationToken ct);
}
