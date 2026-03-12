using KaiROS.AI.Uno.Models;
using System.Diagnostics;

namespace KaiROS.AI.Uno.Services;

public class ChatService : IChatService
{
    private readonly IModelManagerService _modelManager;
    private readonly IWebSearchService _webSearchService;
    private readonly IDocumentService _documentService;

    private InferenceStats _lastStats = new();

    public bool IsModelLoaded => _modelManager.ActiveModel != null && _modelManager.IsNativeBackendAvailable;
    public InferenceStats LastStats => _lastStats;

    public event EventHandler<string>? TokenGenerated;
    public event EventHandler<InferenceStats>? StatsUpdated;

    public ChatService(
        IModelManagerService modelManager,
        IWebSearchService webSearchService,
        IDocumentService documentService)
    {
        _modelManager = modelManager;
        _webSearchService = webSearchService;
        _documentService = documentService;
        
        Debug.WriteLine($"ChatService created. IsNativeBackendAvailable: {_modelManager.IsNativeBackendAvailable}");
    }

    public async Task<string> GenerateResponseAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        var result = new System.Text.StringBuilder();
        await foreach (var token in GenerateResponseStreamAsync(messages, cancellationToken: cancellationToken))
        {
            result.Append(token);
        }
        return result.ToString();
    }

    public async Task<string> GenerateResponseAsync(IEnumerable<ChatMessage> messages, bool useWebSearch, CancellationToken cancellationToken = default)
    {
        var result = new System.Text.StringBuilder();
        await foreach (var token in GenerateResponseStreamAsync(messages, useWebSearch, cancellationToken: cancellationToken))
        {
            result.Append(token);
        }
        return result.ToString();
    }

    public async IAsyncEnumerable<string> GenerateResponseStreamAsync(
        IEnumerable<ChatMessage> messages,
        string? imagePath = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var token in GenerateResponseStreamAsync(messages, false, null, null, imagePath, cancellationToken))
        {
            yield return token;
        }
    }

    public async IAsyncEnumerable<string> GenerateResponseStreamAsync(
        IEnumerable<ChatMessage> messages,
        bool useWebSearch,
        string? imagePath = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var token in GenerateResponseStreamAsync(messages, useWebSearch, null, null, imagePath, cancellationToken))
        {
            yield return token;
        }
    }

    public async IAsyncEnumerable<string> GenerateResponseStreamAsync(
        IEnumerable<ChatMessage> messages,
        bool useWebSearch,
        string? sessionContext,
        string? ragContext,
        string? imagePath = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Debug.WriteLine($"GenerateResponseStreamAsync: IsNativeBackendAvailable={_modelManager.IsNativeBackendAvailable}, ActiveModel={_modelManager.ActiveModel?.Name ?? "null"}");

        if (!_modelManager.IsNativeBackendAvailable)
        {
            yield return "AI inference is only available on desktop platforms. This web version demonstrates the UI but cannot run local LLM models.";
            yield break;
        }

        if (_modelManager.ActiveModel == null)
        {
            yield return "No model loaded. Please go to Models and load a downloaded model first.";
            yield break;
        }

        if (!_modelManager.ActiveModel.IsDownloaded)
        {
            yield return $"Model '{_modelManager.ActiveModel.DisplayName}' is not downloaded. Please download it first from the Models page.";
            yield break;
        }

        var messageList = messages.ToList();
        var prompt = BuildPrompt(messageList, useWebSearch, sessionContext, ragContext);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var tokenCount = 0;

#if DESKTOP
        // Desktop implementation would use LLamaSharp here
        // This is a placeholder for the actual implementation
        Debug.WriteLine($"Desktop mode: Would load model from {_modelManager.ActiveModel.LocalPath}");
#endif

        // Placeholder response for demonstration - in real implementation this would use LLamaSharp
        var response = $"Hello! I'm running on {_modelManager.ActiveModel.DisplayName}. This is a demonstration response. " +
                       $"In the full implementation with LLamaSharp, this would generate actual AI responses from your locally running model. " +
                       $"The model file would be loaded from: {_modelManager.ActiveModel.LocalPath}";

        foreach (var word in response.Split(' '))
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            tokenCount++;
            TokenGenerated?.Invoke(this, word + " ");

            _lastStats = new InferenceStats
            {
                TokensPerSecond = tokenCount / stopwatch.Elapsed.TotalSeconds,
                TotalTokens = tokenCount,
                GeneratedTokens = tokenCount,
                ElapsedTime = stopwatch.Elapsed
            };
            StatsUpdated?.Invoke(this, _lastStats);

            yield return word + " ";

            await Task.Delay(50, cancellationToken);
        }

        stopwatch.Stop();
    }

    public void ClearContext()
    {
        // Would clear the model context on desktop
    }

    private string BuildPrompt(IEnumerable<ChatMessage> messages, bool useWebSearch, string? sessionContext, string? ragContext)
    {
        var sb = new System.Text.StringBuilder();

        foreach (var message in messages)
        {
            sb.AppendLine($"{message.Role}: {message.Content}");
        }

        sb.Append("Assistant:");

        return sb.ToString();
    }
}
