using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KaiROS.AI.WinUI.Services.Tools;

public class DateTimeTool : ITool
{
    public string Name => "DateTime";
    public string Description => "Gets the current date, time, and timezone information.";
    public string ParametersJsonSchema => """
    {
      "type": "object",
      "properties": {
        "timeZoneId": { "type": "string", "description": "Optional: Timezone ID (e.g. 'Eastern Standard Time', 'India Standard Time')" }
      }
    }
    """;

    public bool RequiresConfirmation => false;
    public string GetConfirmationDetails(Dictionary<string, object> parameters) => "";

    public Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken ct)
    {
        var localTime = DateTime.Now;
        var utcTime = DateTime.UtcNow;

        string? tzId = parameters.GetStringParameter("timeZoneId");
        if (!string.IsNullOrEmpty(tzId))
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
                var tzTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, tz);
                return Task.FromResult(new ToolResult
                {
                    Success = true,
                    Output = $"Time in timezone '{tzId}': {tzTime:yyyy-MM-dd HH:mm:ss} (Offset: {tz.BaseUtcOffset})"
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new ToolResult { Success = false, Error = $"Timezone error: {ex.Message}" });
            }
        }

        return Task.FromResult(new ToolResult
        {
            Success = true,
            Output = $"Local Time: {localTime:yyyy-MM-dd HH:mm:ss} (Timezone: {TimeZoneInfo.Local.DisplayName})\nUTC Time: {utcTime:yyyy-MM-dd HH:mm:ss}"
        });
    }
}
