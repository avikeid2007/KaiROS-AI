using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KaiROS.AI.WinUI;
using KaiROS.AI.WinUI.Models;
using KaiROS.AI.WinUI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using KaiROS.AI.WinUI.Services.Tools;

namespace KaiROS.AI.WinUI.ViewModels;

public partial class ChatViewModel : ViewModelBase
{
    private readonly IChatService _chatService;
    private readonly IModelManagerService _modelManager;
    private readonly ISessionService _sessionService;
    private readonly IExportService _exportService;
    private readonly IDocumentService _documentService;
    private readonly IRaasService _raasService;
    private readonly IAgentService _agentService;
    private readonly DispatcherQueue _dispatcherQueue;
    private CancellationTokenSource? _currentInferenceCts;

    [ObservableProperty]
    public partial bool IsAgentModeEnabled { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<ChatMessageViewModel> Messages { get; set; } = [];

    [ObservableProperty]
    public partial ObservableCollection<ChatSession> Sessions { get; set; } = [];

    [ObservableProperty]
    public partial ChatSession? CurrentSession { get; set; }

    [ObservableProperty]
    public partial string UserInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsWebSearchEnabled { get; set; }

    [ObservableProperty]
    public partial string SystemPrompt { get; set; } = "You are a helpful, friendly AI assistant. Be concise and clear.";

    [ObservableProperty]
    public partial bool IsGenerating { get; set; }

    [ObservableProperty]
    public partial bool IsSystemPromptExpanded { get; set; }

    [ObservableProperty]
    public partial double TokensPerSecond { get; set; }

    [ObservableProperty]
    public partial int TotalTokens { get; set; }

    [ObservableProperty]
    public partial string MemoryUsage { get; set; } = "N/A";

    [ObservableProperty]
    public partial string ElapsedTime { get; set; } = "0s";

    [ObservableProperty]
    public partial string ContextWindow { get; set; } = "N/A";

    [ObservableProperty]
    public partial string GpuLayers { get; set; } = "N/A";

    [ObservableProperty]
    public partial bool HasActiveModel { get; set; }

    [ObservableProperty]
    public partial bool IsVisionModelReady { get; set; }

    [ObservableProperty]
    public partial string ActiveModelInfo { get; set; } = "No model loaded";

    [ObservableProperty]
    public partial bool IsSessionListVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsSearchVisible { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsEnterToSendEnabled { get; set; }

    [ObservableProperty]
    public partial string CurrentDocumentName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? AttachedImagePath { get; set; }

    [ObservableProperty]
    public partial bool HasAttachedImage { get; set; }
    
    // --- RAG Selection ---
    
    [ObservableProperty]
    public partial ObservableCollection<string> AvailableKnowledgeBases { get; set; } =
    [
        "None" 
    ];

    [ObservableProperty]
    public partial string SelectedKnowledgeBase { get; set; } = "None";

    [ObservableProperty]
    public partial int GlobalRagDocumentCount { get; set; }

    private string _currentDocumentContext = string.Empty;

    public IModelManagerService ModelManager => _modelManager;

    public ChatViewModel(IChatService chatService, IModelManagerService modelManager, ISessionService sessionService, IExportService exportService, IDocumentService documentService, IRaasService raasService, IAgentService agentService)
    {
        _chatService = chatService;
        _modelManager = modelManager;
        _sessionService = sessionService;
        _exportService = exportService;
        _documentService = documentService;
        _raasService = raasService;
        _agentService = agentService;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _agentService.ConfirmationCallback = ConfirmToolApprovalAsync;

        IsWebSearchEnabled = false;
        IsEnterToSendEnabled = true;
        IsAgentModeEnabled = true;

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

        _dispatcherQueue.TryEnqueue(() =>
        {
            HasActiveModel = _chatService.IsModelLoaded;
            IsVisionModelReady = _modelManager.IsVisionModelLoaded;
        });

        // UpdateKnowledgeBaseList touches ObservableCollection — must be on UI thread
        _dispatcherQueue.TryEnqueue(UpdateKnowledgeBaseList);
    }
    
    private void UpdateKnowledgeBaseList()
    {
        // specific logic to preserve selection if possible
        var current = SelectedKnowledgeBase;
        
        AvailableKnowledgeBases.Clear();
        AvailableKnowledgeBases.Add("None");

        // Offer global document context when documents are loaded
        if (_documentService.LoadedDocuments.Any())
        {
            AvailableKnowledgeBases.Add("Global Knowledge Base");
        }
        
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

    private async Task<bool> ConfirmToolApprovalAsync(string toolName, string details)
    {
        var tcs = new TaskCompletionSource<bool>();
        _dispatcherQueue.TryEnqueue(async () =>
        {
            var mainWindow = App.Current.Services.GetRequiredService<MainWindow>();
            var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = "Tool Approval Required",
                Content = $"Agent wants to use '{toolName}'.\n\nDetails:\n{details}\n\nDo you want to allow this action?",
                PrimaryButtonText = "Allow",
                CloseButtonText = "Deny",
                DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Close,
                XamlRoot = mainWindow.Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            tcs.SetResult(result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary);
        });
        return await tcs.Task;
    }

    private void OnModelLoaded(object? sender, LLMModelInfo model)
    {
        // ModelLoaded fires from background inference thread — must dispatch to UI
        _dispatcherQueue.TryEnqueue(() =>
        {
            HasActiveModel = true;
            IsVisionModelReady = _modelManager.IsVisionModelLoaded;
            ActiveModelInfo = $"{model.DisplayName} ({model.SizeText})";
        });
    }

    private void OnModelUnloaded(object? sender, EventArgs e)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            HasActiveModel = false;
            IsVisionModelReady = false;
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
            var mainWindow = KaiROS.AI.WinUI.App.Current.Services.GetRequiredService<MainWindow>();
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
                            _currentDocumentContext = _currentDocumentContext[..50000];
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
            var mainWindow = KaiROS.AI.WinUI.App.Current.Services.GetRequiredService<MainWindow>();
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
            else if (SelectedKnowledgeBase != null && SelectedKnowledgeBase.StartsWith("Service: "))
            {
                var serviceName = SelectedKnowledgeBase[9..];
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

            if (IsAgentModeEnabled)
            {
                assistantVm.IsAgentMessage = true;
                AgentStepViewModel? currentThinkingVm = null;
                AgentStepViewModel? currentToolCallVm = null;

                try
                {
                    var enabledTools = _agentService.GetEnabledTools();
                    await foreach (var step in _agentService.RunAgentAsync(
                        userRequest: userMessage.Content,
                        enabledTools: enabledTools,
                        ct: _currentInferenceCts.Token))
                    {
                        if (step.Type == AgentStepType.Thinking)
                        {
                            if (currentThinkingVm == null)
                            {
                                currentThinkingVm = new AgentStepViewModel
                                {
                                    Type = AgentStepType.Thinking,
                                    Content = step.Content
                                };
                                var captured = currentThinkingVm;
                                _dispatcherQueue.TryEnqueue(() => assistantVm.AgentSteps.Add(captured));
                            }
                            else
                            {
                                var content = step.Content;
                                var capturedThinking = currentThinkingVm;
                                _dispatcherQueue.TryEnqueue(() => capturedThinking.Content = content);
                            }
                        }
                        else if (step.Type == AgentStepType.ToolCall)
                        {
                            currentThinkingVm = null;
                            var argsJson = JsonSerializer.Serialize(step.ToolArgs, new JsonSerializerOptions { WriteIndented = true });
                            currentToolCallVm = new AgentStepViewModel
                            {
                                Type = AgentStepType.ToolCall,
                                ToolName = step.ToolName,
                                ToolArgsJson = argsJson,
                                Content = $"Executing {step.ToolName}..."
                            };
                            var captured = currentToolCallVm;
                            _dispatcherQueue.TryEnqueue(() => assistantVm.AgentSteps.Add(captured));
                        }
                        else if (step.Type == AgentStepType.ToolResult)
                        {
                            if (currentToolCallVm != null)
                            {
                                var result = step.Result;
                                var capturedTool = currentToolCallVm;
                                _dispatcherQueue.TryEnqueue(() =>
                                {
                                    if (result != null)
                                    {
                                        capturedTool.IsSuccess = result.Success;
                                        capturedTool.ToolResultText = result.Success ? result.Output : result.Error;
                                        capturedTool.Content = result.Success 
                                            ? $"Completed {step.ToolName} successfully." 
                                            : $"Failed to execute {step.ToolName}.";
                                    }
                                });
                            }
                        }
                        else if (step.Type == AgentStepType.FinalResponse)
                        {
                            var content = step.Content;
                            _dispatcherQueue.TryEnqueue(() =>
                            {
                                assistantVm.Content = content;
                                assistantVm.Message.Content = content;
                            });
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _dispatcherQueue.TryEnqueue(() => assistantVm.AppendContent("\n[Generation stopped]"));
                }
                catch (Exception ex)
                {
                    _dispatcherQueue.TryEnqueue(() => assistantVm.Content = $"Error during generation: {ex.Message}");
                }
                finally
                {
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        assistantVm.CleanupContent();
                        assistantVm.Message.IsStreaming = false;
                        assistantVm.IsStreaming = false;
                    });
                    IsGenerating = false;
                    _currentInferenceCts = null;

                    if (CurrentSession != null && !string.IsNullOrEmpty(assistantVm.Content))
                    {
                        await _sessionService.AddMessageAsync(CurrentSession.Id, assistantVm.Message);
                        CurrentSession.MessageCount++;
                    }
                }
            }
            else
            {
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

public partial class ChatMessageViewModel(ChatMessage message, DispatcherQueue? dispatcherQueue = null) : ObservableObject
{
    public ChatMessage Message { get; } = message;

    [ObservableProperty]
    public partial string Content { get; set; } = message.Content;

    [ObservableProperty]
    public partial bool IsStreaming { get; set; } = message.IsStreaming;

    [ObservableProperty]
    public partial ObservableCollection<AgentStepViewModel> AgentSteps { get; set; } = [];

    [ObservableProperty]
    public partial bool IsAgentMessage { get; set; }

    public bool IsUser => Message.Role == ChatRole.User;
    public bool IsAssistant => Message.Role == ChatRole.Assistant;
    public bool IsSystem => Message.Role == ChatRole.System;
    public string Timestamp => Message.Timestamp.ToString("HH:mm");

    public bool HasImage => !string.IsNullOrEmpty(Message.AttachedImagePath);
    public string? AttachedImagePath => Message.AttachedImagePath;

    private readonly System.Text.StringBuilder _tokenBuffer = new();
    private readonly Lock _bufferLock = new();
    private Microsoft.UI.Xaml.DispatcherTimer? _flushTimer;
    private int _pendingTokenCount;
    private const int BATCH_TOKEN_COUNT = 15;
    private const int FLUSH_INTERVAL_MS = 50;

    private readonly DispatcherQueue? _dispatcherQueue = dispatcherQueue ?? DispatcherQueue.GetForCurrentThread();

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

public partial class AgentStepViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsThinking))]
    [NotifyPropertyChangedFor(nameof(IsToolCall))]
    public partial AgentStepType Type { get; set; }

    [ObservableProperty]
    public partial string Content { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ToolName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ToolArgsJson { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ToolResultText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSuccess { get; set; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;

    public bool IsThinking => Type == AgentStepType.Thinking;
    public bool IsToolCall => Type == AgentStepType.ToolCall;
}
