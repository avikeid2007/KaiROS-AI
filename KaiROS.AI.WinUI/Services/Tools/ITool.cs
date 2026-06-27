using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KaiROS.AI.WinUI.Services.Tools;

public interface ITool
{
    string Name { get; }
    string Description { get; }
    string ParametersJsonSchema { get; }
    bool RequiresConfirmation { get; }
    string GetConfirmationDetails(Dictionary<string, object> parameters);
    Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken ct);
}

public class ToolResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}

public static class ToolParameterExtensions
{
    public static string? GetStringParameter(this Dictionary<string, object> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out var val))
            return null;

        if (val is string str)
            return str;

        if (val is JsonElement elem)
        {
            if (elem.ValueKind == JsonValueKind.String)
                return elem.GetString();
            return elem.GetRawText();
        }

        return val?.ToString();
    }
}
