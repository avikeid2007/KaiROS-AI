using System.Collections.Generic;
using KaiROS.AI.WinUI.Services.Tools;

namespace KaiROS.AI.WinUI.Models;

public enum AgentStepType
{
    Thinking,
    ToolCall,
    ToolResult,
    FinalResponse
}

public class AgentStep
{
    public AgentStepType Type { get; set; }
    public string Content { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public Dictionary<string, object> ToolArgs { get; set; } = [];
    public ToolResult? Result { get; set; }
}
