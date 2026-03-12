using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KaiROS.AI.Uno.Models;
using KaiROS.AI.Uno.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace KaiROS.AI.Uno.ViewModels;

public partial class ChatViewModel : ViewModelBase
{
    private readonly IChatService _chatService;
    private readonly IModelManagerService _modelManager;
    private readonly ISessionService _sessionService;
    private readonly IExportService _exportService;
    private readonly IDocumentService _documentService;
    private readonly IRaasService _raasService;
    private CancellationTokenSource? _currentInferenceCts;

    [ObservableProperty]
    private ObservableCollection<ChatMessageViewModel> _messages = [];

    [ObservableProperty]
    private ObservableCollection<ChatSession> _sessions = [];

    [ObservableProperty]
    private ChatSession? _currentSession;

    [ObservableProperty]
    private string _userInput = string.Empty;

    [ObservableProperty]
    private bool _isWebSearchEnabled;

    [ObservableProperty]
    private string _systemPrompt = "You are a helpful, friendly AI assistant. Be concise and clear.";

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private bool _isSystemPromptExpanded;

    [ObservableProperty]
    private double _tokensPerSecond;

    [ObservableProperty]
    private int _totalTokens;

    [ObservableProperty]
    private string _memoryUsage = "N/A";

    [ObservableProperty]
    private string _elapsedTime = "0s";

    [ObservableProperty]
    private string _contextWindow = "N/A";

    [ObservableProperty]
    private string _gpuLayers = "N/A";

    [ObservableProperty]
    private bool _hasActiveModel;

    [ObservableProperty]
    private string _activeModelInfo = "No model loaded";

    [ObservableProperty]
    private bool _isSessionListVisible = true;

    [ObservableProperty]
    private string _currentDocumentName = string.Empty;

    [ObservableProperty]
    private string? _attachedImagePath;

    [ObservableProperty]
    private bool _hasAttachedImage;

    [ObservableProperty]
    private ObservableCollection<string> _availableKnowledgeBases = ["None"];

    [ObservableProperty]
    private string _selectedKnowledgeBase = "None";

    [ObservableProperty]
    private int _globalRagDocumentCount;

    private string _currentDocumentContext = string.Empty;

    public IModelManagerService ModelManager => _modelManager;

    public ChatViewModel(
        IChatService chatService,
        IModelManagerService modelManager,
        ISessionService sessionService,
        IExportService exportService,
        IDocumentService documentService,
        IRaasService raasService)
    {
        _chatService = chatService;
        _modelManager = modelManager;
        _sessionService = sessionService;
        _exportService = exportService;
        _documentService = documentService;
        _raasService = raasService;

        IsWebSearchEnabled = false;

        _chatService.StatsUpdated += OnStatsUpdated;
        _modelManager.ModelLoaded += OnModelLoaded;
        _modelManager.ModelUnloaded += OnModelUnloaded;
    }

    public override async Task InitializeAsync()
    {
        await _sessionService.InitializeAsync();
        // Note: Session loading would be implemented here
        await _raasService.InitializeAsync();

        // Update model info based on platform
        if (!_modelManager.IsNativeBackendAvailable)
        {
            ActiveModelInfo = "AI inference requires desktop app";
        }
    }

    private void OnModelLoaded(object? sender, LLMModelInfo model)
    {
        HasActiveModel = true;
        ActiveModelInfo = $"{model.DisplayName} ({model.SizeText})";
    }

    private void OnModelUnloaded(object? sender, EventArgs e)
    {
        HasActiveModel = false;
        ActiveModelInfo = "No model loaded";
    }

    private void OnStatsUpdated(object? sender, InferenceStats stats)
    {
        TokensPerSecond = Math.Round(stats.TokensPerSecond, 1);
        TotalTokens = stats.TotalTokens;
        MemoryUsage = stats.MemoryUsageText;
        ElapsedTime = $"{stats.ElapsedTime.TotalSeconds:F1}s";
        ContextWindow = stats.ContextSize > 0 ? $"{stats.ContextSize:N0}" : "N/A";
        GpuLayers = stats.GpuLayers >= 0 ? stats.GpuLayers.ToString() : "N/A";
    }

    [RelayCommand]
    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(UserInput) || IsGenerating)
            return;

        Debug.WriteLine($"SendMessage: IsModelLoaded={_chatService.IsModelLoaded}, HasActiveModel={HasActiveModel}");
        Debug.WriteLine($"SendMessage: ActiveModel={_modelManager.ActiveModel?.Name ?? "null"}, IsNativeBackendAvailable={_modelManager.IsNativeBackendAvailable}");

        if (!_chatService.IsModelLoaded)
        {
            var errorMsg = !_modelManager.IsNativeBackendAvailable 
                ? "AI inference requires the desktop application. Please run KaiROS AI on Windows/Mac/Linux desktop."
                : "No model loaded. Please go to Models, download a model, and click Load.";
            
            Messages.Add(new ChatMessageViewModel(
                ChatMessage.Assistant(errorMsg)));
            return;
        }

        string savedInput = UserInput;
        try
        {
            var userMessage = HasAttachedImage && !string.IsNullOrEmpty(AttachedImagePath)
                ? ChatMessage.UserWithImage(UserInput, AttachedImagePath)
                : ChatMessage.User(UserInput);

            Messages.Add(new ChatMessageViewModel(userMessage));

            string? imagePathToSend = AttachedImagePath;
            UserInput = string.Empty;
            RemoveAttachedImageCommand.Execute(null);

            var allMessages = new List<ChatMessage>();
            if (!string.IsNullOrWhiteSpace(SystemPrompt))
                allMessages.Add(ChatMessage.System(SystemPrompt));
            allMessages.AddRange(Messages.Select(m => m.Message));

            var assistantMessage = ChatMessage.Assistant(string.Empty);
            assistantMessage.IsStreaming = true;
            var assistantVm = new ChatMessageViewModel(assistantMessage);
            Messages.Add(assistantVm);

            IsGenerating = true;
            _currentInferenceCts = new CancellationTokenSource();

            try
            {
                await foreach (var token in _chatService.GenerateResponseStreamAsync(
                    messages: allMessages,
                    useWebSearch: IsWebSearchEnabled,
                    sessionContext: _currentDocumentContext,
                    ragContext: null,
                    imagePath: imagePathToSend,
                    cancellationToken: _currentInferenceCts.Token))
                {
                    assistantVm.AppendContent(token);
                }
            }
            catch (OperationCanceledException)
            {
                assistantVm.AppendContent("\n[Generation stopped]");
            }
            catch (Exception ex)
            {
                assistantVm.Content = $"Error during generation: {ex.Message}";
            }
            finally
            {
                assistantVm.CleanupContent();
                assistantVm.Message.IsStreaming = false;
                assistantVm.IsStreaming = false;
                IsGenerating = false;
                _currentInferenceCts = null;
            }
        }
        catch (Exception ex)
        {
            IsGenerating = false;
            _currentInferenceCts = null;
            Messages.Add(new ChatMessageViewModel(
                ChatMessage.Assistant($"Error: {ex.Message}")));
            if (string.IsNullOrWhiteSpace(UserInput))
                UserInput = savedInput;
        }
    }

    [RelayCommand]
    private void StopGeneration()
    {
        _currentInferenceCts?.Cancel();
    }

    [RelayCommand]
    private void ClearChat()
    {
        Messages.Clear();
        _chatService.ClearContext();
        CurrentSession = null;
        TokensPerSecond = 0;
        TotalTokens = 0;
        MemoryUsage = "N/A";
        ElapsedTime = "0s";
        RemoveDocumentCommand.Execute(null);
    }

    [RelayCommand]
    private void NewSession()
    {
        CurrentSession = null;
        Messages.Clear();
        _chatService.ClearContext();
        TokensPerSecond = 0;
        TotalTokens = 0;
        MemoryUsage = "N/A";
        ElapsedTime = "0s";
        RemoveDocumentCommand.Execute(null);
    }

    [RelayCommand]
    private void RemoveDocument()
    {
        _currentDocumentContext = string.Empty;
        CurrentDocumentName = string.Empty;
    }

    [RelayCommand]
    private void RemoveAttachedImage()
    {
        AttachedImagePath = null;
        HasAttachedImage = false;
    }

    [RelayCommand]
    private void ToggleSystemPrompt()
    {
        IsSystemPromptExpanded = !IsSystemPromptExpanded;
    }
}

public partial class ChatMessageViewModel : ObservableObject
{
    public ChatMessage Message { get; }

    [ObservableProperty]
    private string _content;

    [ObservableProperty]
    private bool _isStreaming;

    public bool IsUser => Message.Role == ChatRole.User;
    public bool IsAssistant => Message.Role == ChatRole.Assistant;
    public bool IsSystem => Message.Role == ChatRole.System;
    public string Timestamp => Message.Timestamp.ToString("HH:mm");
    public bool HasImage => !string.IsNullOrEmpty(Message.AttachedImagePath);
    public string? AttachedImagePath => Message.AttachedImagePath;

    private readonly System.Text.StringBuilder _tokenBuffer = new();
    private readonly object _bufferLock = new();

    public ChatMessageViewModel(ChatMessage message)
    {
        Message = message;
        _content = message.Content;
        _isStreaming = message.IsStreaming;
    }

    public void AppendContent(string text)
    {
        lock (_bufferLock)
        {
            _tokenBuffer.Append(text);
        }
        Content += text;
        Message.Content = Content;
    }

    public void CleanupContent()
    {
        var unwantedPatterns = new[] { "###", "\n###", "User:", "\nUser:", "Human:", "\nHuman:", "<|im_end|>", "" };
        var cleaned = Content;
        foreach (var pattern in unwantedPatterns) cleaned = cleaned.Replace(pattern, "");
        Content = cleaned.Trim();
        Message.Content = Content;
    }

    [RelayCommand]
    private void CopyContent()
    {
        // Platform-specific clipboard implementation needed
    }
}
