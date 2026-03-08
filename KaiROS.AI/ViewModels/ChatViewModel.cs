using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KaiROS.AI;
using KaiROS.AI.Models;
using KaiROS.AI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Collections.ObjectModel;
using System.IO;

namespace KaiROS.AI.ViewModels;

public partial class ChatViewModel : ViewModelBase
{
    private readonly IChatService _chatService;
    private readonly IModelManagerService _modelManager;
    private readonly ISessionService _sessionService;
    private readonly IExportService _exportService;
    private readonly IDocumentService _documentService;
    private readonly IRaasService _raasService;
    private readonly DispatcherQueue _dispatcherQueue;
    private CancellationTokenSource? _currentInferenceCts;

    [ObservableProperty]
    private ObservableCollection<ChatMessageViewModel> _messages = new();

    [ObservableProperty]
    private ObservableCollection<ChatSession> _sessions = new();

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
    private bool _isSearchVisible;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isEnterToSendEnabled;

    [ObservableProperty]
    private string _currentDocumentName = string.Empty;

    [ObservableProperty]
    private string? _attachedImagePath;

    [ObservableProperty]
    private bool _hasAttachedImage;
    
    // --- RAG Selection ---
    
    [ObservableProperty]
    private ObservableCollection<string> _availableKnowledgeBases = new() 
    { 
        "None" 
    };

    [ObservableProperty]
    private string _selectedKnowledgeBase = "None"; // Default to None

    [ObservableProperty]
    private int _globalRagDocumentCount;

    private string _currentDocumentContext = string.Empty;

    public IModelManagerService ModelManager => _modelManager;

    public ChatViewModel(IChatService chatService, IModelManagerService modelManager, ISessionService sessionService, IExportService exportService, IDocumentService documentService, IRaasService raasService)
    {
        _chatService = chatService;
        _modelManager = modelManager;
        _sessionService = sessionService;
        _exportService = exportService;
        _documentService = documentService;
        _raasService = raasService;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        IsWebSearchEnabled = false;
        IsEnterToSendEnabled = true;

        _chatService.StatsUpdated += OnStatsUpdated;
        _modelManager.ModelLoaded += OnModelLoaded;
        _modelManager.ModelUnloaded += OnModelUnloaded;

        // Always dispatch to UI thread — CollectionChanged may fire from any thread
        _raasService.Configurations.CollectionChanged += (s, e) =>
            _dispatcherQueue.TryEnqueue(UpdateKnowledgeBaseList);
    }

    public override async Task InitializeAsync()
    {
        await _sessionService.InitializeAsync().ConfigureAwait(false);

        var sessions = await _sessionService.GetAllSessionsAsync().ConfigureAwait(false);
        var docCount = _documentService.LoadedDocuments.Count;

        // Populate sessions on the UI thread to avoid cross-thread ObservableCollection writes
        _dispatcherQueue.TryEnqueue(() =>
        {
            Sessions.Clear();
            foreach (var s in sessions)
                Sessions.Add(s);
            GlobalRagDocumentCount = docCount;
        });

        await _raasService.InitializeAsync().ConfigureAwait(false);

        // UpdateKnowledgeBaseList touches ObservableCollection — must be on UI thread
        _dispatcherQueue.TryEnqueue(UpdateKnowledgeBaseList);
    }
    
    private void UpdateKnowledgeBaseList()
    {
        // specific logic to preserve selection if possible
        var current = SelectedKnowledgeBase;
        
        AvailableKnowledgeBases.Clear();
        AvailableKnowledgeBases.Add("None");
        // User removed Global Knowledge tab, so we remove it here too
        
        foreach (var config in _raasService.Configurations)
        {
            // Only add running services? User said "saved RAG configuration", implies any? 
            // But if not running, we can't get context unless we load it on demand. 
            // For now, let's list all, but if not running, we might WARN or try to start it.
            // Requirement said "Use saved... as global RAG". Should probably work even if REST API is off?
            // If I implemented ApiServer to own the RagEngine, then I need the ApiServer to be Alive (Running) to use it.
            // So listing only Running services makes sense, OR start on demand.
            // Let's filter by Running for simplicity, or show all and check IsRunning.
            
            // Add all configurations
            // User can select them, and we handle the "Not Running" case in SendMessage
            AvailableKnowledgeBases.Add($"Service: {config.Name}");
        }
        
        if (AvailableKnowledgeBases.Contains(current))
        {
            SelectedKnowledgeBase = current;
        }
        else
        {
            SelectedKnowledgeBase = "None";
        }
    }

    private void OnModelLoaded(object? sender, LLMModelInfo model)
    {
        // ModelLoaded fires from background inference thread — must dispatch to UI
        _dispatcherQueue.TryEnqueue(() =>
        {
            HasActiveModel = true;
            ActiveModelInfo = $"{model.DisplayName} ({model.SizeText})";
        });
    }

    private void OnModelUnloaded(object? sender, EventArgs e)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            HasActiveModel = false;
            ActiveModelInfo = "No model loaded";
        });
    }

    private void OnStatsUpdated(object? sender, InferenceStats stats)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            TokensPerSecond = Math.Round(stats.TokensPerSecond, 1);
            TotalTokens = stats.TotalTokens;
            MemoryUsage = stats.MemoryUsageText;
            ElapsedTime = $"{stats.ElapsedTime.TotalSeconds:F1}s";
            ContextWindow = stats.ContextSize > 0 ? $"{stats.ContextSize:N0}" : "N/A";
            GpuLayers = stats.GpuLayers >= 0 ? stats.GpuLayers.ToString() : "N/A";
        });
    }

    [RelayCommand]
    private async Task UploadDocument()
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".txt");
            picker.FileTypeFilter.Add(".md");
            picker.FileTypeFilter.Add(".json");
            picker.FileTypeFilter.Add(".cs");
            picker.FileTypeFilter.Add(".xml");
            picker.FileTypeFilter.Add(".html");
            picker.FileTypeFilter.Add(".pdf");
            picker.FileTypeFilter.Add(".docx");
            picker.FileTypeFilter.Add(".doc");
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            var mainWindow = KaiROS.AI.App.Current.Services.GetRequiredService<MainWindow>();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(mainWindow));

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                var filePath = file.Path;
                var fileName = Path.GetFileName(filePath);

                if (File.Exists(filePath))
                {
                    var extractedContent = await _documentService.GetDocumentContentAsync(filePath);

                    if (string.IsNullOrWhiteSpace(extractedContent))
                    {
                        System.Diagnostics.Debug.WriteLine($"[ChatViewModel] WARNING: No text extracted from {fileName}");
                        _currentDocumentContext = string.Empty;
                        CurrentDocumentName = string.Empty;
                    }
                    else
                    {
                        _currentDocumentContext = extractedContent;
                        CurrentDocumentName = fileName;
                        
                        if (_currentDocumentContext.Length > 50000)
                        {
                            _currentDocumentContext = _currentDocumentContext.Substring(0, 50000);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to upload: {ex.Message}";
        }
    }


    [RelayCommand]
    private void RemoveDocument()
    {
        _currentDocumentContext = string.Empty;
        CurrentDocumentName = string.Empty;
    }

    [RelayCommand]
    private async Task AttachImage()
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".bmp");
            picker.FileTypeFilter.Add(".gif");
            picker.FileTypeFilter.Add(".webp");
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            var mainWindow = KaiROS.AI.App.Current.Services.GetRequiredService<MainWindow>();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(mainWindow));

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                AttachedImagePath = file.Path;
                HasAttachedImage = true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to attach image: {ex.Message}");
        }
    }

    [RelayCommand]
    private void RemoveAttachedImage()
    {
        AttachedImagePath = null;
        HasAttachedImage = false;
    }

    [RelayCommand]
    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(UserInput) || IsGenerating)
            return;

        if (!_chatService.IsModelLoaded)
        {
            Messages.Add(new ChatMessageViewModel(
                ChatMessage.Assistant("⚠️ No model loaded. Please go to the Models tab and load a model first."),
                _dispatcherQueue));
            return;
        }

        string savedInput = UserInput;
        try
        {
            if (CurrentSession == null)
            {
                var modelName = _modelManager.ActiveModel?.DisplayName;
                var newSession = await _sessionService.CreateSessionAsync(modelName, SystemPrompt);
                CurrentSession = newSession;
                Sessions.Insert(0, CurrentSession);
            }

            ChatMessage userMessage;
            if (HasAttachedImage && !string.IsNullOrEmpty(AttachedImagePath))
                userMessage = ChatMessage.UserWithImage(UserInput, AttachedImagePath);
            else
                userMessage = ChatMessage.User(UserInput);

            Messages.Add(new ChatMessageViewModel(userMessage, _dispatcherQueue));
            await _sessionService.AddMessageAsync(CurrentSession.Id, userMessage);
            CurrentSession.MessageCount++;

            if (CurrentSession.MessageCount == 1)
            {
                CurrentSession.Title = ChatSession.GenerateTitle(UserInput);
                await _sessionService.UpdateSessionAsync(CurrentSession);
            }

            // --- Determine RAG Context ---
            string? ragContext = null;
            if (SelectedKnowledgeBase == "Global Knowledge Base")
            {
                ragContext = _documentService.GetContextForQuery(UserInput, 3);
            }
            else if (SelectedKnowledgeBase.StartsWith("Service: "))
            {
                var serviceName = SelectedKnowledgeBase.Substring(9);
                var config = _raasService.Configurations.FirstOrDefault(c => c.Name == serviceName);
                if (config != null)
                {
                    var server = _raasService.GetServer(config.Id);
                    ragContext = server != null && server.IsRunning
                        ? server.RagEngine.GetContext(UserInput, 3)
                        : "[System: The selected RAG service is not running. Answer based on general knowledge only.]";
                }
            }

            // Capture image path before clearing
            string? imagePathToSend = AttachedImagePath;
            UserInput = string.Empty;
            RemoveAttachedImage();

            var allMessages = new List<ChatMessage>();
            if (!string.IsNullOrWhiteSpace(SystemPrompt))
                allMessages.Add(ChatMessage.System(SystemPrompt));
            allMessages.AddRange(Messages.Select(m => m.Message));

            var assistantMessage = ChatMessage.Assistant(string.Empty);
            assistantMessage.IsStreaming = true;
            var assistantVm = new ChatMessageViewModel(assistantMessage, _dispatcherQueue);
            Messages.Add(assistantVm);

            IsGenerating = true;
            _currentInferenceCts = new CancellationTokenSource();

            try
            {
                await foreach (var token in _chatService.GenerateResponseStreamAsync(
                    messages: allMessages,
                    useWebSearch: IsWebSearchEnabled,
                    sessionContext: _currentDocumentContext,
                    ragContext: ragContext,
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

                if (CurrentSession != null && !string.IsNullOrEmpty(assistantVm.Content))
                {
                    await _sessionService.AddMessageAsync(CurrentSession.Id, assistantVm.Message);
                    CurrentSession.MessageCount++;
                }
            }
        }
        catch (Exception ex)
        {
            // Surface unexpected pre-inference errors as a visible chat message
            IsGenerating = false;
            _currentInferenceCts = null;
            Messages.Add(new ChatMessageViewModel(
                ChatMessage.Assistant($"⚠️ Error: {ex.Message}"),
                _dispatcherQueue));
            // Restore input so user can retry
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
        RemoveDocument();
    }

    [RelayCommand]
    private async Task NewSession()
    {
        CurrentSession = null;
        Messages.Clear();
        _chatService.ClearContext();
        TokensPerSecond = 0;
        TotalTokens = 0;
        MemoryUsage = "N/A";
        ElapsedTime = "0s";
        RemoveDocument();
    }

    [RelayCommand]
    private async Task LoadSession(ChatSession session)
    {
        if (session == null) return;
        var loadedSession = await _sessionService.GetSessionAsync(session.Id).ConfigureAwait(false);
        if (loadedSession == null) return;

        _dispatcherQueue.TryEnqueue(() =>
        {
            CurrentSession = loadedSession;
            Messages.Clear();
            _chatService.ClearContext();

            foreach (var msg in loadedSession.Messages)
                Messages.Add(new ChatMessageViewModel(msg, _dispatcherQueue));

            if (!string.IsNullOrEmpty(loadedSession.SystemPrompt))
                SystemPrompt = loadedSession.SystemPrompt;

            TokensPerSecond = 0;
            TotalTokens = 0;
            MemoryUsage = "N/A";
            ElapsedTime = "0s";
        });
    }

    [RelayCommand]
    private async Task DeleteSession(ChatSession session)
    {
        if (session == null) return;
        await _sessionService.DeleteSessionAsync(session.Id).ConfigureAwait(false);
        _dispatcherQueue.TryEnqueue(() =>
        {
            Sessions.Remove(session);
            if (CurrentSession?.Id == session.Id)
            {
                CurrentSession = null;
                Messages.Clear();
                _chatService.ClearContext();
            }
        });
    }

    [RelayCommand]
    private void ToggleSessionList()
    {
        IsSessionListVisible = !IsSessionListVisible;
    }

    [RelayCommand]
    private void ToggleSearch()
    {
        IsSearchVisible = !IsSearchVisible;
        if (!IsSearchVisible) SearchText = string.Empty;
    }

    [RelayCommand]
    private void CloseSearch()
    {
        IsSearchVisible = false;
        SearchText = string.Empty;
    }

    [RelayCommand]
    private async Task ExportChatAsMarkdown()
    {
        if (CurrentSession == null || Messages.Count == 0) return;
        var messages = Messages.Select(m => m.Message).ToList();
        await _exportService.ExportWithDialogAsync(CurrentSession, messages, ExportFormat.Markdown);
    }

    [RelayCommand]
    private async Task ExportChatAsJson()
    {
        if (CurrentSession == null || Messages.Count == 0) return;
        var messages = Messages.Select(m => m.Message).ToList();
        await _exportService.ExportWithDialogAsync(CurrentSession, messages, ExportFormat.Json);
    }

    [RelayCommand]
    private async Task ExportChatAsText()
    {
        if (CurrentSession == null || Messages.Count == 0) return;
        var messages = Messages.Select(m => m.Message).ToList();
        await _exportService.ExportWithDialogAsync(CurrentSession, messages, ExportFormat.Text);
    }

    [RelayCommand]
    private void ToggleSystemPrompt()
    {
        IsSystemPromptExpanded = !IsSystemPromptExpanded;
    }
    
    [RelayCommand]
    private void CopyContent() { /* ... handled in item view model or pass parameter ... */ }
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
    private Microsoft.UI.Xaml.DispatcherTimer? _flushTimer;
    private int _pendingTokenCount;
    private const int BATCH_TOKEN_COUNT = 15;
    private const int FLUSH_INTERVAL_MS = 50;

    private readonly DispatcherQueue? _dispatcherQueue;

    public ChatMessageViewModel(ChatMessage message, DispatcherQueue? dispatcherQueue = null)
    {
        Message = message;
        _content = message.Content;
        _isStreaming = message.IsStreaming;
        _dispatcherQueue = dispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
    }

    /// <summary>
    /// Thread-safe: may be called from background LLamaSharp inference threads.
    /// Buffers tokens and flushes to the UI thread via DispatcherQueue.
    /// </summary>
    public void AppendContent(string text)
    {
        string? toFlush = null;
        lock (_bufferLock)
        {
            _tokenBuffer.Append(text);
            _pendingTokenCount++;
            if (_pendingTokenCount >= BATCH_TOKEN_COUNT)
            {
                toFlush = _tokenBuffer.ToString();
                _tokenBuffer.Clear();
                _pendingTokenCount = 0;
            }
        }

        if (toFlush != null)
        {
            // Batch threshold hit — flush immediately on UI thread
            var captured = toFlush;
            EnqueueUI(() =>
            {
                _flushTimer?.Stop();
                Content += captured;
                Message.Content = Content;
            });
        }
        else
        {
            // Not yet at batch threshold — ensure the periodic flush timer is running
            EnqueueUI(EnsureFlushTimer);
        }
    }

    private void EnqueueUI(Action action)
    {
        if (_dispatcherQueue == null)
        {
            action();
            return;
        }
        // If already on the UI thread, run inline to avoid redundant marshalling
        if (_dispatcherQueue == DispatcherQueue.GetForCurrentThread())
            action();
        else
            _dispatcherQueue.TryEnqueue(() => action());
    }

    // Must be called on the UI thread (enforced via EnqueueUI).
    private void EnsureFlushTimer()
    {
        if (_flushTimer == null)
        {
            _flushTimer = new Microsoft.UI.Xaml.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(FLUSH_INTERVAL_MS)
            };
            _flushTimer.Tick += (s, e) =>
            {
                string? buffered;
                lock (_bufferLock)
                {
                    if (_tokenBuffer.Length == 0) return;
                    buffered = _tokenBuffer.ToString();
                    _tokenBuffer.Clear();
                    _pendingTokenCount = 0;
                }
                _flushTimer?.Stop();
                Content += buffered;
                Message.Content = Content;
            };
        }
        if (!_flushTimer.IsEnabled) _flushTimer.Start();
    }

    public void FinalizeStreaming()
    {
        string? remaining;
        lock (_bufferLock)
        {
            remaining = _tokenBuffer.Length > 0 ? _tokenBuffer.ToString() : null;
            _tokenBuffer.Clear();
            _pendingTokenCount = 0;
        }
        EnqueueUI(() =>
        {
            _flushTimer?.Stop();
            _flushTimer = null;
            if (!string.IsNullOrEmpty(remaining))
            {
                Content += remaining;
                Message.Content = Content;
            }
        });
    }

    public void CleanupContent()
    {
        string? remaining;
        lock (_bufferLock)
        {
            remaining = _tokenBuffer.Length > 0 ? _tokenBuffer.ToString() : null;
            _tokenBuffer.Clear();
            _pendingTokenCount = 0;
        }
        EnqueueUI(() =>
        {
            _flushTimer?.Stop();
            _flushTimer = null;
            if (!string.IsNullOrEmpty(remaining))
                Content += remaining;
            var unwantedPatterns = new[] { "###", "\n###", "User:", "\nUser:", "Human:", "\nHuman:", "<|im_end|>", "<|assistant|>" };
            var cleaned = Content;
            foreach (var pattern in unwantedPatterns) cleaned = cleaned.Replace(pattern, "");
            Content = cleaned.Trim();
            Message.Content = Content;
        });
    }

    [RelayCommand]
    private void CopyContent()
    {
        if (!string.IsNullOrEmpty(Content))
        {
            try
            {
                var pkg = new DataPackage();
                pkg.SetText(Content);
                Clipboard.SetContent(pkg);
            }
            catch { }
        }
    }
}
