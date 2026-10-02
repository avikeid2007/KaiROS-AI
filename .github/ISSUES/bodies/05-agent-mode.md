## Summary
Add an Agent Mode where the AI can use tools (file system, web fetch, calculator, code execution, system info) to autonomously complete multi-step tasks.

## Motivation
Agent mode transforms KaiROS from a chatbot into a true AI assistant that can act, not just talk. This is the most requested feature in local AI applications.

## Detailed Requirements

### Tool Framework
Create a plugin-style tool system in `Services/Tools/`:

```csharp
public interface ITool
{
    string Name { get; }
    string Description { get; }
    string ParametersJsonSchema { get; }
    Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken ct);
}

public class ToolResult
{
    public bool Success { get; set; }
    public string Output { get; set; }
    public string Error { get; set; }
}
```

### Built-in Tools
1. **FileReaderTool**: Read file contents (with path validation/sandboxing)
2. **FileWriterTool**: Write/create files (with user confirmation dialog)
3. **WebFetchTool**: Fetch and extract text from a URL
4. **CalculatorTool**: Evaluate math expressions safely
5. **SystemInfoTool**: Get system info (CPU, RAM, disk, running processes)
6. **DateTimeTool**: Get current date/time, timezone conversions
7. **ClipboardTool**: Read/write clipboard content

### Agent Service: `AgentService.cs`
```csharp
public interface IAgentService
{
    IAsyncEnumerable<AgentStep> RunAgentAsync(string userRequest, List<ITool> enabledTools, CancellationToken ct);
}

public class AgentStep
{
    public AgentStepType Type { get; set; }  // Thinking, ToolCall, ToolResult, FinalResponse
    public string Content { get; set; }
    public string ToolName { get; set; }
    public Dictionary<string, object> ToolArgs { get; set; }
    public ToolResult Result { get; set; }
}
```

The agent loop:
1. Send user request + tool schemas to LLM with function calling prompt
2. If LLM returns a tool call -> execute tool -> feed result back to LLM
3. Repeat until LLM gives final text response (max 10 iterations)
4. Display each step in the UI

### UI Changes (`ChatView.xaml`)
- **Agent Mode toggle** in chat toolbar (distinct from regular chat)
- **Step-by-step display**: Show each agent step in the chat:
  - Thinking: "I need to read the file..."
  - Tool Call: `read_file(path="/Users/...")`
  - Result: File contents (collapsible)
  - Final Response
- **Tool approval**: For destructive tools (file write, clipboard), show confirmation dialog
- **Settings**: Configure which tools are enabled/disabled

### Security
- File operations sandboxed to user-specified directories
- All destructive actions require user approval
- Maximum iteration limit (10) to prevent infinite loops
- Token budget per agent run

## Files to Create/Modify
- **[NEW]** `Services/AgentService.cs` + `IAgentService.cs`
- **[NEW]** `Services/Tools/ITool.cs`
- **[NEW]** `Services/Tools/FileReaderTool.cs`
- **[NEW]** `Services/Tools/FileWriterTool.cs`
- **[NEW]** `Services/Tools/WebFetchTool.cs`
- **[NEW]** `Services/Tools/CalculatorTool.cs`
- **[NEW]** `Services/Tools/SystemInfoTool.cs`
- **[NEW]** `Services/Tools/DateTimeTool.cs`
- **[NEW]** `Services/Tools/ClipboardTool.cs`
- **[NEW]** `Models/AgentModels.cs`
- **[MODIFY]** `Views/ChatView.xaml` - agent mode UI + step display
- **[MODIFY]** `ViewModels/ChatViewModel.cs` - agent commands
- **[MODIFY]** `Views/SettingsView.xaml` - tool configuration

## Acceptance Criteria
- [ ] Agent mode toggle in chat interface
- [ ] At least 5 built-in tools working
- [ ] Multi-step agent execution with visible steps
- [ ] Tool calls displayed with expandable input/output
- [ ] Destructive actions require user confirmation
- [ ] Agent respects iteration limit
- [ ] Agent conversations saved to chat history
