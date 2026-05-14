using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using KaiROS.AI.WinUI.Models;

namespace KaiROS.AI.WinUI.Services;

public class ApiServer : IDisposable
{
    private readonly RaasConfiguration _config;
    private readonly IChatService _chatService;
    private readonly RagEngine _ragEngine;
    private readonly HttpListener _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;

    private sealed class ParsedChatRequest
    {
        public List<ChatMessage> Messages { get; set; } = [];
        public bool Stream { get; set; }
        public bool UseOpenAiFormat { get; set; }
        public string RequestedModel { get; set; } = string.Empty;
        public ResponseFormatInfo? ResponseFormat { get; set; }
        public bool IncludeUsageInStream { get; set; }
    }
    
    public bool IsRunning { get; private set; }
    public RagEngine RagEngine => _ragEngine;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ApiServer(RaasConfiguration config, IChatService chatService, RagEngine ragEngine)
    {
        _config = config;
        _chatService = chatService;
        _ragEngine = ragEngine;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{config.Port}/");
    }

    public async Task StartAsync()
    {
        if (IsRunning) return;

        try
        {
            // Load sources first? 
            // In a real app we might want to lazy load or load on start.
            // For now, let's assume RagEngine is pre-populated or populated here.
            
            _listener.Start();
            IsRunning = true;
            _cts = new CancellationTokenSource();
            _listenerTask = ListenAsync(_cts.Token);
            
            System.Diagnostics.Debug.WriteLine($"[RaaS] Server '{_config.Name}' started on port {_config.Port}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RaaS] Failed to start server {_config.Name}: {ex.Message}");
            IsRunning = false;
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (!IsRunning) return;

        _cts?.Cancel();
        _listener.Stop();
        _listener.Close();
        
        if (_listenerTask != null)
        {
            try { await _listenerTask; } catch { }
        }
        
        IsRunning = false;
        System.Diagnostics.Debug.WriteLine($"[RaaS] Server '{_config.Name}' stopped");
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = HandleRequestAsync(context, ct);
            }
            catch (Exception ex) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RaaS] Listener error: {ex.Message}");
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken ct)
    {
        var request = context.Request;
        var response = context.Response;
        
        // Count request (atomic — multiple handlers may run concurrently)
        Interlocked.Increment(ref _config._requestCount);

        try
        {
             // Enable CORS — restrict to localhost to prevent cross-origin exfiltration
            response.Headers.Add("Access-Control-Allow-Origin", "http://localhost");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 200;
                response.Close();
                return;
            }

            var path = request.Url?.AbsolutePath.ToLowerInvariant() ?? "/";
            
            if (path == "/" && request.HttpMethod == "GET")
            {
                await HandleHomeAsync(response);
            }
            else if ((path == "/chat" || path == "/api/chat") && request.HttpMethod == "POST")
            {
                await HandleChatAsync(request, response, forceStreaming: false, preferOpenAiFormat: false, ct);
            }
            else if ((path == "/chat/stream" || path == "/api/chat/stream") && request.HttpMethod == "POST")
            {
                await HandleChatAsync(request, response, forceStreaming: true, preferOpenAiFormat: false, ct);
            }
            else if (path == "/v1/chat/completions" && request.HttpMethod == "POST")
            {
                await HandleChatAsync(request, response, forceStreaming: false, preferOpenAiFormat: true, ct);
            }
            else if (path == "/health" || path == "/api/health")
            {
                 var health = new { status = "ok", service = _config.Name };
                 await SendJsonAsync(response, health);
            }
            else if (path == "/v1/models" || path == "/models" || path == "/api/models")
            {
                await HandleModelsAsync(response);
            }
            else
            {
                response.StatusCode = 404;
                response.Close();
            }
        }
        catch (Exception ex)
        {
             System.Diagnostics.Debug.WriteLine($"[RaaS] Error handling request: {ex.Message}");
             response.StatusCode = 500;
             response.Close();
        }
    }

    private async Task HandleChatAsync(HttpListenerRequest request, HttpListenerResponse response, bool forceStreaming, bool preferOpenAiFormat, CancellationToken ct)
    {
        using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();

        ParsedChatRequest? parsedRequest;
        try
        {
            parsedRequest = ParseChatRequest(body, preferOpenAiFormat, forceStreaming);
        }
        catch (JsonException)
        {
            response.StatusCode = 400;
            response.Close();
            return;
        }

        if (parsedRequest == null || parsedRequest.Messages.Count == 0)
        {
            response.StatusCode = 400;
            response.Close();
            return;
        }

        if (!string.IsNullOrWhiteSpace(parsedRequest.RequestedModel) &&
            !string.Equals(parsedRequest.RequestedModel, "kairos-raas", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(parsedRequest.RequestedModel, _config.Name, StringComparison.OrdinalIgnoreCase))
        {
            await SendJsonAsync(response, new ApiErrorResponse
            {
                Error = new ApiError { Message = $"Unknown model '{parsedRequest.RequestedModel}'. Use 'kairos-raas' for this endpoint." }
            });
            return;
        }

        var messages = parsedRequest.Messages;
        var lastUserMsg = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Content ?? string.Empty;
        var ragContext = _ragEngine.GetContext(lastUserMsg);

        if (!messages.Any(m => m.Role == ChatRole.System))
        {
            messages.Insert(0, new ChatMessage { Role = ChatRole.System, Content = _config.SystemPrompt });
        }

        if (parsedRequest.Stream)
        {
            await HandleStreamingResponseAsync(response, messages, ragContext, ct, parsedRequest.UseOpenAiFormat, parsedRequest.IncludeUsageInStream);
            return;
        }

        var fullResponse = new StringBuilder();

        await foreach (var token in _chatService.GenerateResponseStreamAsync(messages, false, null, ragContext, null, ct))
        {
            fullResponse.Append(token);
        }

        if (parsedRequest.UseOpenAiFormat)
        {
            var text = ApplyResponseFormat(fullResponse.ToString(), parsedRequest.ResponseFormat);
            var promptChars = messages.Sum(m => m.Content?.Length ?? 0);
            var completionChars = text.Length;

            var openAiResponse = new ChatCompletionResponse
            {
                Model = "kairos-raas",
                Choices =
                [
                    new ChatCompletionChoice
                    {
                        Index = 0,
                        Message = new ChatCompletionMessage
                        {
                            Role = "assistant",
                            Content = text
                        },
                        FinishReason = "stop"
                    }
                ],
                Usage = new UsageInfo
                {
                    PromptTokens = Math.Max(1, promptChars / 4),
                    CompletionTokens = Math.Max(1, completionChars / 4),
                    TotalTokens = Math.Max(1, (promptChars + completionChars) / 4)
                }
            };

            await SendJsonAsync(response, openAiResponse);
            return;
        }

        var result = new SimpleChatResponse
        {
            Model = "kairos-raas",
            Content = fullResponse.ToString(),
            TokenCount = fullResponse.Length / 4
        };

        await SendJsonAsync(response, result);
    }

    private static ParsedChatRequest? ParseChatRequest(string body, bool preferOpenAiFormat, bool forceStreaming)
    {
        var openAiRequest = JsonSerializer.Deserialize<ChatCompletionRequest>(body, JsonOptions);
        if (openAiRequest?.Messages?.Count > 0 && (preferOpenAiFormat || !string.IsNullOrWhiteSpace(openAiRequest.Model)))
        {
            return new ParsedChatRequest
            {
                Messages = openAiRequest.Messages.Select(ToInternalMessage).ToList(),
                Stream = forceStreaming || openAiRequest.Stream,
                UseOpenAiFormat = true,
                RequestedModel = openAiRequest.Model,
                ResponseFormat = openAiRequest.ResponseFormat,
                IncludeUsageInStream = openAiRequest.StreamOptions?.IncludeUsage == true
            };
        }

        var simpleRequest = JsonSerializer.Deserialize<SimpleChatRequest>(body, JsonOptions);
        if (simpleRequest?.Messages?.Count > 0)
        {
            return new ParsedChatRequest
            {
                Messages = simpleRequest.Messages.Select(ToInternalMessage).ToList(),
                Stream = forceStreaming,
                UseOpenAiFormat = preferOpenAiFormat
            };
        }

        if (openAiRequest?.Messages?.Count > 0)
        {
            return new ParsedChatRequest
            {
                Messages = openAiRequest.Messages.Select(ToInternalMessage).ToList(),
                Stream = forceStreaming || openAiRequest.Stream,
                UseOpenAiFormat = true,
                RequestedModel = openAiRequest.Model,
                ResponseFormat = openAiRequest.ResponseFormat,
                IncludeUsageInStream = openAiRequest.StreamOptions?.IncludeUsage == true
            };
        }

        return null;
    }

    private static ChatMessage ToInternalMessage(ChatCompletionMessage message)
    {
        return new ChatMessage
        {
            Role = message.Role switch
            {
                "user" => ChatRole.User,
                "system" => ChatRole.System,
                _ => ChatRole.Assistant
            },
            Content = message.Content
        };
    }

    private async Task HandleStreamingResponseAsync(HttpListenerResponse response, List<ChatMessage> messages, string ragContext, CancellationToken ct, bool openAiFormat, bool includeUsageInStream)
    {
        response.ContentType = "text/event-stream";
        response.Headers.Add("Cache-Control", "no-cache");
        response.Headers.Add("Connection", "keep-alive");

        using var writer = new StreamWriter(response.OutputStream, Encoding.UTF8);

        if (openAiFormat)
        {
            var completionId = $"chatcmpl-{Guid.NewGuid():N}";
            var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var completionChars = 0;

            var startChunk = new ChatCompletionChunk
            {
                Id = completionId,
                Created = created,
                Model = "kairos-raas",
                Choices =
                [
                    new ChatCompletionChunkChoice
                    {
                        Index = 0,
                        Delta = new ChatCompletionDelta { Role = "assistant" }
                    }
                ]
            };

            await writer.WriteAsync($"data: {JsonSerializer.Serialize(startChunk, JsonOptions)}\n\n");
            await writer.FlushAsync();

            await foreach (var token in _chatService.GenerateResponseStreamAsync(messages, false, null, ragContext, null, ct))
            {
                completionChars += token.Length;
                var chunk = new ChatCompletionChunk
                {
                    Id = completionId,
                    Created = created,
                    Model = "kairos-raas",
                    Choices =
                    [
                        new ChatCompletionChunkChoice
                        {
                            Index = 0,
                            Delta = new ChatCompletionDelta { Content = token }
                        }
                    ]
                };

                await writer.WriteAsync($"data: {JsonSerializer.Serialize(chunk, JsonOptions)}\n\n");
                await writer.FlushAsync();
            }

            var endChunk = new ChatCompletionChunk
            {
                Id = completionId,
                Created = created,
                Model = "kairos-raas",
                Choices =
                [
                    new ChatCompletionChunkChoice
                    {
                        Index = 0,
                        Delta = new ChatCompletionDelta(),
                        FinishReason = "stop"
                    }
                ]
            };

            await writer.WriteAsync($"data: {JsonSerializer.Serialize(endChunk, JsonOptions)}\n\n");

            if (includeUsageInStream)
            {
                var promptChars = messages.Sum(m => m.Content?.Length ?? 0);
                var usageChunk = new
                {
                    id = completionId,
                    @object = "chat.completion.chunk",
                    created,
                    model = "kairos-raas",
                    choices = Array.Empty<object>(),
                    usage = new UsageInfo
                    {
                        PromptTokens = Math.Max(1, promptChars / 4),
                        CompletionTokens = Math.Max(1, completionChars / 4),
                        TotalTokens = Math.Max(1, (promptChars + completionChars) / 4)
                    }
                };

                await writer.WriteAsync($"data: {JsonSerializer.Serialize(usageChunk, JsonOptions)}\n\n");
            }

            await writer.WriteAsync("data: [DONE]\n\n");
            await writer.FlushAsync();
            response.Close();
            return;
        }

        await foreach (var token in _chatService.GenerateResponseStreamAsync(messages, false, null, ragContext, null, ct))
        {
            var chunk = new { content = token };
            var json = JsonSerializer.Serialize(chunk, JsonOptions);
            await writer.WriteAsync($"data: {json}\n\n");
            await writer.FlushAsync();
        }

        await writer.WriteAsync("data: [DONE]\n\n");
        await writer.FlushAsync();
        response.Close();
    }

    private static string ApplyResponseFormat(string content, ResponseFormatInfo? responseFormat)
    {
        if (!string.Equals(responseFormat?.Type, "json_object", StringComparison.OrdinalIgnoreCase))
        {
            return content;
        }

        var trimmed = content.Trim();
        if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
        {
            try
            {
                using var _ = JsonDocument.Parse(trimmed);
                return trimmed;
            }
            catch
            {
                // Fall through and wrap in JSON.
            }
        }

        return JsonSerializer.Serialize(new { response = content });
    }

    private async Task HandleModelsAsync(HttpListenerResponse response)
    {
        var models = new ModelsListResponse
        {
            Data =
            [
                new ModelInfo
                {
                    Id = "kairos-raas",
                    OwnedBy = "kairos-local"
                }
            ]
        };

        await SendJsonAsync(response, models);
    }

    private async Task HandleHomeAsync(HttpListenerResponse response)
    {
        var sourcesList = _config.Sources.Any() 
            ? string.Join("", _config.Sources.Select(s => $"<li><span class=\"source-icon\">📄</span> {s.Name}</li>")) 
            : "<li class=\"empty\">No sources loaded</li>";

        var html = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{_config.Name} - KaiROS RaaS</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{
            font-family: 'Segoe UI', system-ui, sans-serif;
            background: linear-gradient(135deg, #0f0f23 0%, #1a1a3e 50%, #0f0f23 100%);
            color: #e0e0e0;
            min-height: 100vh;
            padding: 40px 20px;
        }}
        .container {{ max-width: 900px; margin: 0 auto; }}
        .header {{ text-align: center; margin-bottom: 40px; }}
        .badge {{ 
            background: linear-gradient(90deg, #10B981 0%, #059669 100%); 
            color: white; padding: 4px 12px; border-radius: 20px; 
            font-size: 0.8rem; font-weight: bold; vertical-align: middle;
            margin-left: 10px;
        }}
        h1 {{
            font-size: 2.5rem;
            background: linear-gradient(90deg, #667eea 0%, #764ba2 100%);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            margin-bottom: 10px;
        }}
        .grid {{ display: grid; grid-template-columns: 1fr 1fr; gap: 24px; margin-bottom: 24px; }}
        @media (max-width: 768px) {{ .grid {{ grid-template-columns: 1fr; }} }}
        
        .card {{
            background: rgba(255,255,255,0.05);
            border: 1px solid rgba(255,255,255,0.1);
            border-radius: 12px;
            padding: 24px;
        }}
        .card-title {{ font-size: 1.2rem; font-weight: 600; margin-bottom: 16px; color: #fff; border-bottom: 1px solid rgba(255,255,255,0.1); padding-bottom: 8px; }}
        
        .info-row {{ display: flex; justify-content: space-between; margin-bottom: 12px; font-size: 0.95rem; }}
        .label {{ color: #888; }}
        .value {{ font-weight: 600; color: #eee; }}
        
        .sources-list {{ list-style: none; }}
        .sources-list li {{ 
            padding: 8px 12px; background: rgba(0,0,0,0.2); 
            border-radius: 6px; margin-bottom: 8px; font-size: 0.9rem;
            display: flex; align-items: center;
        }}
        .source-icon {{ margin-right: 8px; }}
        .empty {{ color: #666; font-style: italic; background: none !important; }}

        pre {{
            background: rgba(0,0,0,0.4);
            padding: 16px;
            border-radius: 8px;
            overflow-x: auto;
            font-size: 0.85rem;
            color: #a5b3ce;
        }}
        code {{ font-family: 'Consolas', monospace; color: #a5b3ce; }}
        
        .api-url {{ 
            background: rgba(16, 185, 129, 0.1); border: 1px solid rgba(16, 185, 129, 0.3);
            color: #10B981; padding: 12px; border-radius: 8px; text-align: center;
            font-family: 'Consolas', monospace; margin-bottom: 24px; font-size: 1.1rem;
        }}

        .footer {{ text-align: center; color: #666; margin-top: 40px; font-size: 0.85rem; border-top: 1px solid rgba(255,255,255,0.05); padding-top: 20px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>{_config.Name} <span class=""badge"">Running</span></h1>
            <p style=""color: #888;"">KaiROS RaaS Instance</p>
        </div>

        <div class=""api-url"">
            http://localhost:{_config.Port}
        </div>
        
        <div class=""grid"">
            <!-- Info Card -->
            <div class=""card"">
                <div class=""card-title"">⚙️ Configuration</div>

                <div class=""info-row""><span class=""label"">Port</span><span class=""value"">{_config.Port}</span></div>
                <div class=""info-row""><span class=""label"">Requests Served</span><span class=""value"">{_config.RequestCount}</span></div>
                <div class=""info-row""><span class=""label"">System Prompt</span></div>
                <div style=""background: rgba(0,0,0,0.3); padding: 10px; border-radius: 6px; font-size: 0.9rem; color: #ccc; max-height: 100px; overflow-y: auto;"">
                    {_config.SystemPrompt}
                </div>
            </div>

            <!-- Sources Card -->
            <div class=""card"">
                <div class=""card-title"">📚 Knowledge Base</div>
                <ul class=""sources-list"">
                    {sourcesList}
                </ul>
            </div>
        </div>

        <div class=""card"">
            <div class=""card-title"">🚀 API Usage</div>
            
            <p style=""margin-bottom: 8px; color: #eee; font-weight: 600;"">Standard Response</p>
            <pre style=""margin-bottom: 24px;"">curl -X POST http://localhost:{_config.Port}/chat \
  -H ""Content-Type: application/json"" \
  -d '{{
    ""messages"": [
      {{ ""role"": ""user"", ""content"": ""Hello!"" }}
    ]
  }}'</pre>

            <p style=""margin-bottom: 8px; color: #eee; font-weight: 600;"">Streaming Response (SSE)</p>
            <pre>curl -N -X POST http://localhost:{_config.Port}/chat/stream \
  -H ""Content-Type: application/json"" \
  -d '{{
    ""messages"": [
      {{ ""role"": ""user"", ""content"": ""Tell me a story."" }}
    ]
  }}'</pre>
        </div>
        
        <div class=""footer"">
            Powered by KaiROS Local AI • <a href=""http://localhost:5000/"" style=""color: #666; text-decoration: none;"">Main Dashboard</a>
        </div>
    </div>
</body>
</html>";

        response.ContentType = "text/html";
        var bytes = Encoding.UTF8.GetBytes(html);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }

    private async Task SendJsonAsync<T>(HttpListenerResponse response, T data)
    {
        response.ContentType = "application/json";
        var json = JsonSerializer.Serialize(data, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }

    public void Dispose()
    {
        StopAsync().Wait();
    }
}

