using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace KaiROS.AI.WinUI.Services.Tools;

public class WebFetchTool : ITool
{
    public string Name => "WebFetch";
    public string Description => "Fetches text from a web URL.";
    public string ParametersJsonSchema => """
    {
      "type": "object",
      "properties": {
        "url": { "type": "string", "description": "The URL to fetch content from" }
      },
      "required": ["url"]
    }
    """;

    public bool RequiresConfirmation => false;
    public string GetConfirmationDetails(Dictionary<string, object> parameters) => "";

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken ct)
    {
        string? url = parameters.GetStringParameter("url");
        if (string.IsNullOrEmpty(url))
        {
            return new ToolResult { Success = false, Error = "Missing or invalid 'url' parameter." };
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

            var html = await client.GetStringAsync(url, ct);
            
            // Basic HTML to text conversion
            // Strip scripts and styles
            html = Regex.Replace(html, @"<(script|style)[^>]*?>.*?</\1>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            // Strip tags
            html = Regex.Replace(html, @"<[^>]*?>", " ");
            // Collapse whitespaces
            html = Regex.Replace(html, @"\s+", " ").Trim();

            if (html.Length > 5000)
            {
                html = html.Substring(0, 5000) + "... [Truncated]";
            }

            return new ToolResult { Success = true, Output = html };
        }
        catch (Exception ex)
        {
            return new ToolResult { Success = false, Error = $"Failed to fetch URL: {ex.Message}" };
        }
    }
}
