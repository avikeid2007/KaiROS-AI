using KaiROS.AI.WinUI.Models;
using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Transformers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace KaiROS.AI.WinUI.Services;

public class ChatService : IChatService
{
    private readonly ModelManagerService _modelManager;
    private readonly IDocumentService _documentService;
    private readonly IWebSearchService _webSearchService;
    private LLamaContext? _context;
    private InteractiveExecutor? _executor;
    private InferenceStats _lastStats = new();
    private uint _currentContextSize;
    private bool _isSystemPromptSent = false;
    private readonly SemaphoreSlim _inferenceLock = new(1, 1);

    // Chat template support
    private bool _supportsNativeTemplate = false;
    private LLamaWeights? _cachedWeights;

    public bool IsModelLoaded => _modelManager.ActiveModel != null && _context != null;
    public InferenceStats LastStats => _lastStats;
    public uint CurrentContextSize => _currentContextSize;

    public event EventHandler<string>? TokenGenerated;
    public event EventHandler<InferenceStats>? StatsUpdated;

    public ChatService(ModelManagerService modelManager, IDocumentService documentService, IWebSearchService webSearchService)
    {
        _modelManager = modelManager;
        _documentService = documentService;
        _webSearchService = webSearchService;
        _modelManager.ModelLoaded += OnModelLoaded;
        _modelManager.ModelUnloaded += OnModelUnloaded;
    }

    private void OnModelLoaded(object? sender, LLMModelInfo model)
    {
        InitializeContext();
    }

    private void OnModelUnloaded(object? sender, EventArgs e)
    {
        DisposeContext();
    }

    private void InitializeContext()
    {
        var weights = _modelManager.GetLoadedWeights();
        if (weights == null) return;

        _cachedWeights = weights;
        _currentContextSize = 8192; // Default context size
        _context = weights.CreateContext(new ModelParams(_modelManager.ActiveModel?.LocalPath ?? "")
        {
            ContextSize = _currentContextSize
        });

        // Use vision weights if available
        var visionWeights = _modelManager.GetLoadedLlavaWeights();
        if (visionWeights != null)
        {
            _executor = new InteractiveExecutor(_context, visionWeights);
        }
        else
        {
            _executor = new InteractiveExecutor(_context);
        }
        
        _isSystemPromptSent = false;

        // Detect whether this model has an embedded chat template
        _supportsNativeTemplate = DetectNativeTemplate(weights);

        Debug.WriteLine($"[KaiROS] Chat template mode: {(_supportsNativeTemplate ? "Native (LLamaTemplate)" : "Fallback (### System/User)")}");
    }

    /// <summary>
    /// Tries to construct an LLamaTemplate in non-strict mode. Returns true if the model
    /// has an embedded Jinja template we can use.
    /// </summary>
    private static bool DetectNativeTemplate(LLamaWeights weights)
    {
        try
        {
            // strict: false means no exception is thrown if the template is missing —
            // it will fall back to a built-in default. We validate by checking that
            // the template produces non-empty output for a dummy message.
            var template = new LLamaTemplate(weights, strict: false);
            template.Add("user", "test");
            template.AddAssistant = true;
            var result = PromptTemplateTransformer.ToModelPrompt(template);

            // If the result is meaningful (not just an empty string), we have a real template
            return !string.IsNullOrWhiteSpace(result);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[KaiROS] No native template detected: {ex.Message}");
            return false;
        }
    }

    private void DisposeContext()
    {
        _executor = null;
        _context?.Dispose();
        _context = null;
        _cachedWeights = null;
        _supportsNativeTemplate = false;
        _isSystemPromptSent = false;
    }

    public void ClearContext()
    {
        if (_context != null)
        {
            DisposeContext();
            InitializeContext();
        }
    }

    // Interface Implementations
    public Task<string> GenerateResponseAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
        => GenerateResponseAsync(messages, false, cancellationToken);

    public async Task<string> GenerateResponseAsync(IEnumerable<ChatMessage> messages, bool useWebSearch, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        await foreach (var token in GenerateResponseStreamAsync(messages, useWebSearch, null, null, null, cancellationToken))
        {
            sb.Append(token);
        }
        return sb.ToString();
    }

    public IAsyncEnumerable<string> GenerateResponseStreamAsync(IEnumerable<ChatMessage> messages, string? imagePath = null, CancellationToken cancellationToken = default)
        => GenerateResponseStreamAsync(messages, false, null, null, imagePath, cancellationToken);

    public IAsyncEnumerable<string> GenerateResponseStreamAsync(IEnumerable<ChatMessage> messages, bool useWebSearch, string? imagePath = null, CancellationToken cancellationToken = default)
        => GenerateResponseStreamAsync(messages, useWebSearch, null, null, imagePath, cancellationToken);

    public async IAsyncEnumerable<string> GenerateResponseStreamAsync(
        IEnumerable<ChatMessage> messages,
        bool useWebSearch,
        string? sessionContext,
        string? ragContext,
        string? imagePath = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_executor == null || _context == null)
        {
            yield return "Error: No model loaded. Please select and load a model first.";
            yield break;
        }

        string webContext = "";

        // Handle Web Search
        if (useWebSearch)
        {
            var lastUserMessage = messages.LastOrDefault(m => m.Role == ChatRole.User);
            if (lastUserMessage != null)
            {
                yield return "[Searching web...]";
                var searchResult = await PerformWebSearchAsync(lastUserMessage.Content, cancellationToken);
                yield return searchResult.Status;
                webContext = searchResult.Context;
            }
        }

        string prompt;
        var messagesList = messages.ToList();

        // Inject image path for vision models
        if (!string.IsNullOrEmpty(imagePath) && _modelManager.IsVisionModelLoaded)
        {
            var lastUser = messagesList.LastOrDefault(m => m.Role == ChatRole.User);
            if (lastUser != null)
            {
                var visionWeights = _modelManager.GetLoadedLlavaWeights();
                if (visionWeights != null && _executor is InteractiveExecutor interactiveExecutor)
                {
                    try 
                    {
                        var embed = visionWeights.LoadMedia(imagePath);
                        // Give it an explicit ID just in case
                        embed.Id = imagePath;
                        interactiveExecutor.Embeds.Add(embed);
                        Debug.WriteLine($"[KaiROS] Multimodal: Created image embedding for {imagePath}");
                        
                        // Intercept visual grounding triggers globally for all vision models
                        var userText = lastUser.Content.Trim();
                        string lowerText = userText.ToLower();
                        if (lowerText.Contains("what are object") || 
                            lowerText.Contains("what is object") ||
                            lowerText.Contains("what object") ||
                            lowerText.Contains("explain")) // Added 'explain'
                        {
                            userText = "Describe this image in detail. Do not output coordinates.";
                        }

                        // Qwen-VL and similar models handle image tags better when they are inline 
                        // and not separated by newlines, which can break the chat template formatting.
                        lastUser.Content = $"[Image: {{{embed.Id}}}] {userText}";
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[KaiROS] Multimodal Error: Failed to load media. {ex.Message}");
                        lastUser.Content = $"{{{imagePath}}}\n{lastUser.Content}"; // fallback
                    }
                }
                else
                {
                    lastUser.Content = $"{{{imagePath}}}\n{lastUser.Content}"; // fallback
                }
            }
        }
        else if (!string.IsNullOrEmpty(imagePath))
        {
            Debug.WriteLine($"[KaiROS] Warning: Image attached but vision model is not loaded or ready. Vision loaded: {_modelManager.IsVisionModelLoaded}");
        }

        if (!_isSystemPromptSent || _context == null)
        {
            // First turn: include system prompt + context + first user message
            prompt = BuildPrompt(messagesList, isFirstTurn: true, webContext, sessionContext, ragContext);
            _isSystemPromptSent = true;
        }
        else
        {
            // Follow-up turn: only the new user message (the executor maintains running context)
            prompt = BuildPrompt(messagesList, isFirstTurn: false, webContext);
        }

        // Build anti-prompts based on active template mode
        var antiPrompts = BuildAntiPrompts();

        var inferenceParams = new InferenceParams
        {
            MaxTokens = 4096,
            AntiPrompts = antiPrompts
        };

        var stopwatch = Stopwatch.StartNew();
        int tokenCount = 0;
        var startMemory = GC.GetTotalMemory(false);

        // Acquire lock to ensure only one inference runs at a time
        await _inferenceLock.WaitAsync(cancellationToken);

        try
        {
            bool isThinking = false;
            string buffer = "";
            
            // Regexes for thinking blocks
            // `^analysis\b` catches when the model starts exactly with 'analysis' (e.g. stripped <|channel|>analysis)
            var thinkStartRegex = new Regex(@"<think>|<\|.*?\|>analysis|^\s*analysis\b", RegexOptions.Compiled);
            
            // `assistantfinal` catches when <|channel|> is stripped between assistant and final tags
            var thinkEndRegex = new Regex(@"</think>|<\|.*?\|>assistant|<\|.*?\|>final|assistantfinal", RegexOptions.Compiled);
            
            // Regex for inline tags we just want to delete
            var stripTagsRegex = new Regex(@"<\|.*?\|>assistant|<\|.*?\|>final|<\|.*?\|>system|<\|.*?\|>user|<\|.*?\|>|assistantfinal|## OUTPUT:|## Response:|\[/INST\]|</s>|<eos>|### |\n### ", RegexOptions.Compiled);

            await foreach (var token in _executor.InferAsync(prompt, inferenceParams, cancellationToken))
            {
                tokenCount++;
                buffer += token;

                // Handle thinking blocks entry
                if (!isThinking)
                {
                    var match = thinkStartRegex.Match(buffer);
                    if (match.Success)
                    {
                        isThinking = true;
                        string before = buffer.Substring(0, match.Index);
                        
                        // Clean up anything before yielding
                        before = stripTagsRegex.Replace(before, "");
                        foreach (var ap in antiPrompts) before = before.Replace(ap, "");
                        
                        if (!string.IsNullOrWhiteSpace(before))
                        {
                            TokenGenerated?.Invoke(this, before);
                            yield return before;
                        }
                        
                        buffer = buffer.Substring(match.Index + match.Length);
                    }
                }

                // Handle thinking blocks exit
                if (isThinking)
                {
                    var match = thinkEndRegex.Match(buffer);
                    if (match.Success)
                    {
                        isThinking = false;
                        
                        // We also want to strip the actual 'assistant' or 'final' tag,
                        // so we start the regular buffer exactly after this tag.
                        buffer = buffer.Substring(match.Index + match.Length);
                        buffer = buffer.TrimStart('\n', '\r', ' ');
                    }
                    else
                    {
                        // Keep buffering, but limit size in case end tag never comes.
                        if (buffer.Length > 100)
                            buffer = buffer.Substring(buffer.Length - 50);
                        continue;
                    }
                }

                // Handle normal output
                if (!isThinking && buffer.Length > 0)
                {
                    // Check if we might be middle of forming a tag like <|... or <think or </think
                    int lastOpenBracket = buffer.LastIndexOf('<');
                    bool holdsPotentialTag = false;

                    if (lastOpenBracket != -1)
                    {
                        // Holds potential tag if there's no matching closing bracket after it
                        int nextCloseBracket = buffer.IndexOf('>', lastOpenBracket);
                        
                        // Or if it looks like start of anti-prompt
                        string suffix = buffer.Substring(lastOpenBracket);
                        holdsPotentialTag = nextCloseBracket == -1 || antiPrompts.Any(ap => ap.StartsWith(suffix));
                    }

                    if (holdsPotentialTag)
                    {
                        // We have an unfinished tag at the end, yield only the part before it
                        if (lastOpenBracket > 0)
                        {
                            string safeToYield = buffer.Substring(0, lastOpenBracket);
                            safeToYield = stripTagsRegex.Replace(safeToYield, "");
                            foreach (var ap in antiPrompts) safeToYield = safeToYield.Replace(ap, "");
                            
                            if (!string.IsNullOrEmpty(safeToYield))
                            {
                                TokenGenerated?.Invoke(this, safeToYield);
                                yield return safeToYield;
                            }
                            buffer = buffer.Substring(lastOpenBracket);
                        }
                    }
                    else
                    {
                        // No tags forming, yield everything
                        string safeToYield = buffer;
                        safeToYield = stripTagsRegex.Replace(safeToYield, "");
                        foreach (var ap in antiPrompts) safeToYield = safeToYield.Replace(ap, "");
                        
                        if (!string.IsNullOrEmpty(safeToYield))
                        {
                            TokenGenerated?.Invoke(this, safeToYield);
                            yield return safeToYield;
                        }
                        buffer = "";
                    }
                }

                if (tokenCount % 10 == 0) UpdateStats(stopwatch.Elapsed, tokenCount, startMemory);
            }

            // Flush any remaining buffer piece
            if (!isThinking && buffer.Length > 0)
            {
                buffer = stripTagsRegex.Replace(buffer, "");
                foreach (var ap in antiPrompts) buffer = buffer.Replace(ap, "");
                if (!string.IsNullOrEmpty(buffer))
                {
                    TokenGenerated?.Invoke(this, buffer);
                    yield return buffer;
                }
            }
        }
        finally
        {
            _inferenceLock.Release();
        }

        stopwatch.Stop();
        UpdateStats(stopwatch.Elapsed, tokenCount, startMemory);
    }

    // ─── Prompt Building ──────────────────────────────────────────────────────

    /// <summary>
    /// Builds the prompt string for the model, automatically choosing between
    /// the native Jinja chat template (when the GGUF has one) and the legacy
    /// ### System/User/Assistant fallback.
    /// </summary>
    private string BuildPrompt(
        IEnumerable<ChatMessage> messages,
        bool isFirstTurn,
        string webContext = "",
        string? sessionContext = null,
        string? ragContext = null)
    {
        return _supportsNativeTemplate && _cachedWeights != null
            ? BuildNativeTemplatePrompt(messages, isFirstTurn, webContext, sessionContext, ragContext)
            : BuildLegacyPrompt(messages, isFirstTurn, webContext, sessionContext, ragContext);
    }

    /// <summary>
    /// Uses LLamaSharp's LLamaTemplate to produce a prompt that exactly matches
    /// what the model was fine-tuned on (ChatML, Llama-3, Mistral, Gemma, Phi-3, etc.).
    /// </summary>
    private string BuildNativeTemplatePrompt(
        IEnumerable<ChatMessage> messages,
        bool isFirstTurn,
        string webContext = "",
        string? sessionContext = null,
        string? ragContext = null)
    {
        var messageList = messages.ToList();

        var template = new LLamaTemplate(_cachedWeights!, strict: false)
        {
            AddAssistant = true
        };

        if (isFirstTurn)
        {
            // Build system content (with optional RAG / session / web context)
            var systemMsg = messageList.FirstOrDefault(m => m.Role == ChatRole.System);
            var systemContent = systemMsg?.Content ?? "You are a helpful assistant. Be concise and direct.";
            systemContent = AppendContexts(systemContent, webContext, sessionContext, ragContext);

            if (_modelManager.IsVisionModelLoaded)
            {
                systemContent += "\nWhen analyzing images, always provide descriptive text responses rather than raw bounding box coordinates unless the user explicitly asks for coordinates.";
            }

            template.Add("system", systemContent);

            var latestUser = messageList.LastOrDefault(m => m.Role == ChatRole.User);
            if (latestUser != null)
                template.Add("user", latestUser.Content);
        }
        else
        {
            // Follow-up: the executor already has the prior context in its KV-cache.
            // We only need the new turn appended.
            if (!string.IsNullOrEmpty(webContext))
            {
                // Embed any web context as a brief system injection
                template.Add("system", $"[Additional web information]\n{webContext}");
            }

            var latestUser = messageList.LastOrDefault(m => m.Role == ChatRole.User);
            if (latestUser != null)
                template.Add("user", latestUser.Content);
        }

        return PromptTemplateTransformer.ToModelPrompt(template);
    }

    /// <summary>
    /// Legacy ### System/User/Assistant format — used when the GGUF has no embedded template.
    /// </summary>
    private static string BuildLegacyPrompt(
        IEnumerable<ChatMessage> messages,
        bool isFirstTurn,
        string webContext = "",
        string? sessionContext = null,
        string? ragContext = null)
    {
        var sb = new StringBuilder();
        var messageList = messages.ToList();

        if (isFirstTurn)
        {
            var systemMsg = messageList.FirstOrDefault(m => m.Role == ChatRole.System);
            var systemContent = systemMsg?.Content ?? "You are a helpful assistant. Be concise and direct.";
            systemContent = AppendContexts(systemContent, webContext, sessionContext, ragContext);

            sb.AppendLine($"### System:\n{systemContent}\n");

            var latestUser = messageList.LastOrDefault(m => m.Role == ChatRole.User);
            if (latestUser != null)
                sb.AppendLine($"### User:\n{latestUser.Content}\n");
        }
        else
        {
            if (!string.IsNullOrEmpty(webContext))
                sb.AppendLine($"### System:\n[Additional Information]\n{webContext}\n");

            var latestUser = messageList.LastOrDefault(m => m.Role == ChatRole.User);
            if (latestUser != null)
                sb.AppendLine($"### User:\n{latestUser.Content}\n");
        }

        sb.AppendLine("### Assistant:");
        return sb.ToString();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>Returns anti-prompt strings appropriate for the current template mode.</summary>
    private IReadOnlyList<string> BuildAntiPrompts()
    {
        if (_supportsNativeTemplate)
        {
            // Common end-of-turn tokens across the main model families.
            // The executor will stop at whichever appears first.
            return new[]
            {
                "<|eot_id|>",       // Llama-3
                "<|im_end|>",       // ChatML / Qwen / Mistral-Nemo
                "<|end|>",          // Phi-3
                "<eos>",            // Gemma 2
                "<end_of_turn>",    // Gemma
                "[/INST]",          // Llama-2 instruction
                "</s>",             // Llama-2, Mistral v0.1, Gemma
            };
        }

        // Legacy fallback: stop on user role markers
        return new[]
        {
            "User:", "\nUser:", "###", "Human:", "\nHuman:", "### User", "### Human"
        };
    }

    /// <summary>
    /// Appends optional document, RAG, and web contexts to a system message string.
    /// </summary>
    private static string AppendContexts(
        string systemContent,
        string webContext,
        string? sessionContext,
        string? ragContext)
    {
        var sb = new StringBuilder(systemContent);
        string documentContext = string.Empty;

        if (!string.IsNullOrEmpty(sessionContext))
            documentContext += "Attached Document Content:\n" + sessionContext + "\n\n";

        if (!string.IsNullOrEmpty(ragContext))
            documentContext += "Knowledge Base Context:\n" + ragContext + "\n\n";

        string combinedContext = "";
        if (!string.IsNullOrEmpty(documentContext)) combinedContext += "Context:\n" + documentContext + "\n\n";
        if (!string.IsNullOrEmpty(webContext)) combinedContext += webContext + "\n\n";

        if (!string.IsNullOrEmpty(combinedContext))
        {
            sb.Append("\n\n");
            sb.Append(combinedContext.TrimEnd());
        }

        return sb.ToString();
    }

    private async Task<(string Context, string Status)> PerformWebSearchAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            var results = await _webSearchService.SearchAsync(query, 3, cancellationToken);
            if (results.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Web Search Results:");
                var topResults = results.Take(2).ToList();
                foreach (var result in topResults)
                {
                    var content = await _webSearchService.GetPageContentAsync(result.Link, cancellationToken);
                    sb.AppendLine($"--- Source: {result.Title} ({result.Link}) ---");
                    if (!string.IsNullOrEmpty(content)) sb.AppendLine(content);
                    else sb.AppendLine($"Snippet: {result.Snippet}");
                    sb.AppendLine("--- End Source ---\n");
                }
                return (sb.ToString(), "\r[Found info] ");
            }
            else
            {
                return ("", "\r[No results] ");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Web search failed: {ex}");
            return ("", "\r[Search failed] ");
        }
    }

    private void UpdateStats(TimeSpan elapsed, int tokenCount, long startMemory)
    {
        var usedMemory = GC.GetTotalMemory(false) - startMemory;
        _lastStats = new InferenceStats
        {
            ElapsedTime = elapsed,
            TokensPerSecond = tokenCount / (elapsed.TotalSeconds > 0 ? elapsed.TotalSeconds : 1),
            GeneratedTokens = tokenCount,
            MemoryUsageBytes = usedMemory > 0 ? usedMemory : 0
        };
        StatsUpdated?.Invoke(this, _lastStats);
    }
}
