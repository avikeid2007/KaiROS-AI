using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace KaiROS.AI.WinUI.Services.Tools;

public class CalculatorTool : ITool
{
    public string Name => "Calculator";
    public string Description => "Evaluates a mathematical expression safely (supports basic arithmetic: +, -, *, /, parenthesis).";
    public string ParametersJsonSchema => """
    {
      "type": "object",
      "properties": {
        "expression": { "type": "string", "description": "The math expression to evaluate, e.g. '2 * (3.5 + 4)'" }
      },
      "required": ["expression"]
    }
    """;

    public bool RequiresConfirmation => false;
    public string GetConfirmationDetails(Dictionary<string, object> parameters) => "";

    public Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken ct)
    {
        string? expr = parameters.GetStringParameter("expression");
        if (string.IsNullOrEmpty(expr))
        {
            return Task.FromResult(new ToolResult { Success = false, Error = "Missing or invalid 'expression' parameter." });
        }

        try
        {
            // DataTable.Compute is highly safe and handles arithmetic expressions
            using var table = new DataTable();
            var resultObj = table.Compute(expr, string.Empty);
            return Task.FromResult(new ToolResult 
            { 
                Success = true, 
                Output = resultObj?.ToString() ?? "null" 
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult { Success = false, Error = $"Math evaluation error: {ex.Message}" });
        }
    }
}
