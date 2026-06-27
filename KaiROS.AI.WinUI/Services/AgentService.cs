using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using KaiROS.AI.WinUI.Models;
using KaiROS.AI.WinUI.Services.Tools;
using Microsoft.UI.Dispatching;

namespace KaiROS.AI.WinUI.Services;

public class AgentService : IAgentService
{
    private readonly IChatService _chatService;
    private readonly List<ITool> _allTools = [];

    public bool IsFileReaderEnabled { get; set; } = true;
    public bool IsFileWriterEnabled { get; set; } = true;
    public bool IsWebFetchEnabled { get; set; } = true;
    public bool IsCalculatorEnabled { get; set; } = true;
    public bool IsSystemInfoEnabled { get; set; } = true;
    public bool IsDateTimeEnabled { get; set; } = true;
    public bool IsClipboardEnabled { get; set; } = true;

    public Func<string, string, Task<bool>>? ConfirmationCallback { get; set; }

    private static readonly Regex ToolCallRegex = new(@"```tool\s*\n\s*ToolName:\s*(?<name>\w+)\s*\n\s*Arguments:\s*(?<args>\{.*?\})\s*\n\s*```", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    public AgentService(IChatService chatService)
    {
        _chatService = chatService;

        // Instantiate tools
        var uiDispatcher = DispatcherQueue.GetForCurrentThread();
        _allTools.Add(new FileReaderTool());
        _allTools.Add(new FileWriterTool());
        _allTools.Add(new WebFetchTool());
        _allTools.Add(new CalculatorTool());
        _allTools.Add(new SystemInfoTool());
        _allTools.Add(new DateTimeTool());
        _allTools.Add(new ClipboardTool(uiDispatcher));
    }

    public List<ITool> GetEnabledTools()
    {
        var list = new List<ITool>();
        if (IsFileReaderEnabled) list.Add(_allTools.First(t => t.Name == "FileReader"));
        if (IsFileWriterEnabled) list.Add(_allTools.First(t => t.Name == "FileWriter"));
        if (IsWebFetchEnabled) list.Add(_allTools.First(t => t.Name == "WebFetch"));
        if (IsCalculatorEnabled) list.Add(_allTools.First(t => t.Name == "Calculator"));
        if (IsSystemInfoEnabled) list.Add(_allTools.First(t => t.Name == "SystemInfo"));
        if (IsDateTimeEnabled) list.Add(_allTools.First(t => t.Name == "DateTime"));
        if (IsClipboardEnabled) list.Add(_allTools.First(t => t.Name == "Clipboard"));
        return list;
    }

    public async IAsyncEnumerable<AgentStep> RunAgentAsync(
        string userRequest, 
        List<ITool> enabledTools, 
        [EnumeratorCancellation] CancellationToken ct)
    {
        var messages = new List<ChatMessage>();
        var systemPrompt = BuildAgentSystemPrompt(enabledTools);
        messages.Add(ChatMessage.System(systemPrompt));
        messages.Add(ChatMessage.User(userRequest));

        int iteration = 0;
        const int maxIterations = 10;
        bool conversationFinished = false;

        while (iteration < maxIterations && !conversationFinished)
        {
            iteration++;

            var thinkingStep = new AgentStep 
            { 
                Type = AgentStepType.Thinking, 
                Content = "Thinking..." 
            };
            yield return thinkingStep;

            var responseBuilder = new StringBuilder();
            
            await foreach (var token in _chatService.GenerateResponseStreamAsync(messages, cancellationToken: ct))
            {
                responseBuilder.Append(token);
                thinkingStep.Content = responseBuilder.ToString();
                yield return thinkingStep;
            }

            var fullResponse = responseBuilder.ToString();
            var toolCallMatch = ToolCallRegex.Match(fullResponse);

            if (toolCallMatch.Success)
            {
                var toolName = toolCallMatch.Groups["name"].Value.Trim();
                var toolArgsJson = toolCallMatch.Groups["args"].Value.Trim();

                var thinkingText = fullResponse.Substring(0, toolCallMatch.Index).Trim();
                thinkingStep.Content = thinkingText;
                yield return thinkingStep;

                var argsDict = ParseArgs(toolArgsJson);
                var toolCallStep = new AgentStep
                {
                    Type = AgentStepType.ToolCall,
                    ToolName = toolName,
                    ToolArgs = argsDict,
                    Content = $"Tool Call: {toolName}"
                };
                yield return toolCallStep;

                var tool = enabledTools.FirstOrDefault(t => t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));
                ToolResult result;

                if (tool == null)
                {
                    result = new ToolResult { Success = false, Error = $"Tool '{toolName}' is not enabled or not found." };
                }
                else
                {
                    if (tool.RequiresConfirmation)
                    {
                        var confirmed = await PromptConfirmationAsync(tool.Name, tool.GetConfirmationDetails(argsDict));
                        if (!confirmed)
                        {
                            result = new ToolResult { Success = false, Error = "User denied execution of this tool." };
                        }
                        else
                        {
                            try { result = await tool.ExecuteAsync(argsDict, ct); }
                            catch (Exception ex) { result = new ToolResult { Success = false, Error = ex.Message }; }
                        }
                    }
                    else
                    {
                        try { result = await tool.ExecuteAsync(argsDict, ct); }
                        catch (Exception ex) { result = new ToolResult { Success = false, Error = ex.Message }; }
                    }
                }

                var toolResultStep = new AgentStep
                {
                    Type = AgentStepType.ToolResult,
                    ToolName = toolName,
                    Result = result
                };
                yield return toolResultStep;

                messages.Add(ChatMessage.Assistant(fullResponse));
                var resultText = result.Success 
                    ? $"[Tool Result] Success. Output:\n{result.Output}" 
                    : $"[Tool Result] Error. Details:\n{result.Error}";
                messages.Add(ChatMessage.User(resultText));
            }
            else
            {
                var finalStep = new AgentStep
                {
                    Type = AgentStepType.FinalResponse,
                    Content = fullResponse
                };
                yield return finalStep;
                conversationFinished = true;
            }
        }

        if (iteration >= maxIterations && !conversationFinished)
        {
            yield return new AgentStep
            {
                Type = AgentStepType.FinalResponse,
                Content = "Agent stopped: Reached maximum tool invocation limit of 10."
            };
        }
    }

    private async Task<bool> PromptConfirmationAsync(string toolName, string details)
    {
        if (ConfirmationCallback != null)
        {
            return await ConfirmationCallback(toolName, details);
        }
        return false;
    }

    private static Dictionary<string, object> ParseArgs(string json)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json, options) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string BuildAgentSystemPrompt(List<ITool> enabledTools)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are in Agent Mode. You have access to local system tools to autonomously complete multi-step tasks.");
        sb.AppendLine("Think step-by-step. If you need to perform an action using a tool, you MUST output a tool call using the following block syntax:");
        sb.AppendLine();
        sb.AppendLine("```tool");
        sb.AppendLine("ToolName: <Name>");
        sb.AppendLine("Arguments: { \"argName\": \"argValue\" }");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("CRITICAL RULE: When you output a tool call block, stop generating. Do NOT write anything after the closing ```. The system will run the tool and feed the output back to you.");
        sb.AppendLine("If you do not need to call any more tools, output your final answer directly without the ```tool block.");
        sb.AppendLine();
        sb.AppendLine("Available Tools:");
        foreach (var tool in enabledTools)
        {
            sb.AppendLine($"- ToolName: {tool.Name}");
            sb.AppendLine($"  Description: {tool.Description}");
            sb.AppendLine($"  Arguments JSON Schema: {tool.ParametersJsonSchema}");
        }
        sb.AppendLine();
        sb.AppendLine("Example Conversation:");
        sb.AppendLine("User: Find the current time in UTC and write it to C:\\temp\\time.txt.");
        sb.AppendLine("Assistant: <think>I need to get the current time first, so I will call the DateTime tool.</think>\n```tool\nToolName: DateTime\nArguments: {}\n```");
        return sb.ToString();
    }
}
