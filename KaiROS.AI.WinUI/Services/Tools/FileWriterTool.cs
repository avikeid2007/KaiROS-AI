using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KaiROS.AI.WinUI.Services.Tools;

public class FileWriterTool : ITool
{
    public string Name => "FileWriter";
    public string Description => "Writes or overwrites text content to a local file (requires user confirmation).";
    public string ParametersJsonSchema => """
    {
      "type": "object",
      "properties": {
        "path": { "type": "string", "description": "Absolute path to the destination file" },
        "content": { "type": "string", "description": "The text content to write" }
      },
      "required": ["path", "content"]
    }
    """;

    public bool RequiresConfirmation => true;

    public string GetConfirmationDetails(Dictionary<string, object> parameters)
    {
        string path = parameters.GetStringParameter("path") ?? string.Empty;
        string content = parameters.GetStringParameter("content") ?? string.Empty;
        
        string contentPreview = content;
        if (contentPreview.Length > 200) contentPreview = contentPreview.Substring(0, 200) + "...";
        
        return $"Write to file:\nPath: {path}\n\nContent Preview:\n{contentPreview}";
    }

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken ct)
    {
        string? path = parameters.GetStringParameter("path");
        string? content = parameters.GetStringParameter("content");

        if (string.IsNullOrEmpty(path))
        {
            return new ToolResult { Success = false, Error = "Missing or invalid 'path' parameter." };
        }

        if (content == null)
        {
            return new ToolResult { Success = false, Error = "Missing or invalid 'content' parameter." };
        }

        try
        {
            string fullPath = Path.GetFullPath(path);
            string windowsDir = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            
            if (fullPath.StartsWith(windowsDir, StringComparison.OrdinalIgnoreCase))
            {
                return new ToolResult { Success = false, Error = "Access denied: Writing to Windows system directories is blocked." };
            }

            // Create directories if not existing
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await File.WriteAllTextAsync(fullPath, content, ct);
            return new ToolResult { Success = true, Output = $"File written successfully to {fullPath}" };
        }
        catch (Exception ex)
        {
            return new ToolResult { Success = false, Error = $"Failed to write file: {ex.Message}" };
        }
    }
}
