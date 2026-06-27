using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Windows.ApplicationModel.DataTransfer;

namespace KaiROS.AI.WinUI.Services.Tools;

public class ClipboardTool : ITool
{
    private readonly DispatcherQueue _dispatcherQueue;

    public string Name => "Clipboard";
    public string Description => "Reads text from or writes text to the system clipboard.";
    public string ParametersJsonSchema => """
    {
      "type": "object",
      "properties": {
        "action": { "type": "string", "enum": ["read", "write"], "description": "Action to perform: 'read' to get clipboard, 'write' to set clipboard" },
        "content": { "type": "string", "description": "Text content to write (required if action is 'write')" }
      },
      "required": ["action"]
    }
    """;

    public bool RequiresConfirmation => true;

    public string GetConfirmationDetails(Dictionary<string, object> parameters)
    {
        string action = parameters.GetStringParameter("action") ?? "read";
        
        if (action.Equals("write", StringComparison.OrdinalIgnoreCase))
        {
            string content = parameters.GetStringParameter("content") ?? string.Empty;
            string contentPreview = content;
            if (contentPreview.Length > 200) contentPreview = contentPreview.Substring(0, 200) + "...";
            return $"Write content to system clipboard:\n\"{contentPreview}\"";
        }
        else
        {
            return "Read content from system clipboard";
        }
    }

    public ClipboardTool(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken ct)
    {
        string? action = parameters.GetStringParameter("action");
        if (string.IsNullOrEmpty(action))
        {
            return new ToolResult { Success = false, Error = "Missing or invalid 'action' parameter." };
        }

        bool isRead = action.Equals("read", StringComparison.OrdinalIgnoreCase);
        bool isWrite = action.Equals("write", StringComparison.OrdinalIgnoreCase);

        if (!isRead && !isWrite)
        {
            return new ToolResult { Success = false, Error = "Action must be 'read' or 'write'." };
        }

        string? content = parameters.GetStringParameter("content");
        if (isWrite && content == null)
        {
            return new ToolResult { Success = false, Error = "Missing 'content' parameter for write action." };
        }

        if (isRead)
        {
            var tcs = new TaskCompletionSource<ToolResult>();
            _dispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    var dataPackageView = Clipboard.GetContent();
                    if (dataPackageView.Contains(StandardDataFormats.Text))
                    {
                        var text = await dataPackageView.GetTextAsync();
                        tcs.SetResult(new ToolResult { Success = true, Output = text });
                    }
                    else
                    {
                        tcs.SetResult(new ToolResult { Success = true, Output = "[Clipboard is empty or does not contain text]" });
                    }
                }
                catch (Exception ex)
                {
                    tcs.SetResult(new ToolResult { Success = false, Error = $"Clipboard read failed: {ex.Message}" });
                }
            });
            return await tcs.Task;
        }
        else // write
        {
            var tcs = new TaskCompletionSource<ToolResult>();
            _dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    var pkg = new DataPackage();
                    pkg.SetText(content);
                    Clipboard.SetContent(pkg);
                    Clipboard.Flush(); // Flush is helpful to keep it after app exit
                    tcs.SetResult(new ToolResult { Success = true, Output = "Text written to clipboard successfully." });
                }
                catch (Exception ex)
                {
                    tcs.SetResult(new ToolResult { Success = false, Error = $"Clipboard write failed: {ex.Message}" });
                }
            });
            return await tcs.Task;
        }
    }
}
