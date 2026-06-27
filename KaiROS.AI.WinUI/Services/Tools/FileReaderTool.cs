using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KaiROS.AI.WinUI.Services.Tools;

public class FileReaderTool : ITool
{
    public string Name => "FileReader";
    public string Description => "Reads the text contents of a local file safely.";
    public string ParametersJsonSchema => """
    {
      "type": "object",
      "properties": {
        "path": { "type": "string", "description": "Absolute path to the text file" }
      },
      "required": ["path"]
    }
    """;

    public bool RequiresConfirmation => false;
    public string GetConfirmationDetails(Dictionary<string, object> parameters) => "";

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken ct)
    {
        string? path = parameters.GetStringParameter("path");
        if (string.IsNullOrEmpty(path))
        {
            return new ToolResult { Success = false, Error = "Missing or invalid 'path' parameter." };
        }

        try
        {
            // Simple sandboxing: prevent reading system folders like C:\Windows
            string fullPath = Path.GetFullPath(path);
            string windowsDir = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            
            if (fullPath.StartsWith(windowsDir, StringComparison.OrdinalIgnoreCase))
            {
                return new ToolResult { Success = false, Error = "Access denied: Reading from Windows system directories is blocked." };
            }

            if (!File.Exists(fullPath))
            {
                return new ToolResult { Success = false, Error = $"File not found: {fullPath}" };
            }

            // Limit read size to 50KB to protect LLM context
            var fileInfo = new FileInfo(fullPath);
            if (fileInfo.Length > 50 * 1024)
            {
                var partialText = await ReadPartialFileAsync(fullPath, 50 * 1024, ct);
                return new ToolResult 
                { 
                    Success = true, 
                    Output = $"[Truncated - file is too large ({fileInfo.Length} bytes). Showing first 50KB:]\n{partialText}" 
                };
            }

            var text = await File.ReadAllTextAsync(fullPath, ct);
            return new ToolResult { Success = true, Output = text };
        }
        catch (Exception ex)
        {
            return new ToolResult { Success = false, Error = $"Failed to read file: {ex.Message}" };
        }
    }

    private async Task<string> ReadPartialFileAsync(string path, int bytesCount, CancellationToken ct)
    {
        byte[] buffer = new byte[bytesCount];
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        int readBytes = await fs.ReadAsync(buffer, 0, bytesCount, ct);
        return System.Text.Encoding.UTF8.GetString(buffer, 0, readBytes);
    }
}
