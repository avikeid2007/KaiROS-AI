$env:GITHUB_TOKEN = ''
$env:GH_TOKEN = ''
$repo = "avikeid2007/KaiROS-AI"

# Issue 1: AI Memory
$body1 = @"
## Summary
Add persistent AI memory that allows KaiROS to remember user preferences, facts, and context across chat sessions. The AI should automatically extract and store key facts from conversations and inject relevant memories into future chats.

## Motivation
Currently each chat session starts fresh with no memory of past interactions. Users must re-explain preferences and context every time. Persistent memory makes the AI feel personal and intelligent over time.

## Detailed Requirements

### Data Model
Create a new ``MemoryEntry`` model in ``KaiROS.AI.WinUI/Models/``:
```csharp
public class MemoryEntry
{
    public int Id { get; set; }
    public string Key { get; set; }         // e.g., "user_name", "coding_language_preference"
    public string Value { get; set; }       // e.g., "Avnish", "C#"
    public string Category { get; set; }    // "personal", "preference", "fact", "instruction"
    public DateTime CreatedAt { get; set; }
    public DateTime LastUsedAt { get; set; }
    public int UsageCount { get; set; }
    public bool IsActive { get; set; } = true;
}
```

### Database Changes (``DatabaseService.cs``)
Add a new ``Memories`` table to the SQLite database in ``InitializeAsync()``:
```sql
CREATE TABLE IF NOT EXISTS Memories (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Key TEXT NOT NULL,
    Value TEXT NOT NULL,
    Category TEXT DEFAULT 'fact',
    CreatedAt TEXT NOT NULL,
    LastUsedAt TEXT NOT NULL,
    UsageCount INTEGER DEFAULT 1,
    IsActive INTEGER DEFAULT 1
);
```
Add CRUD methods to ``IDatabaseService``:
- ``Task<List<MemoryEntry>> GetMemoriesAsync()``
- ``Task AddMemoryAsync(MemoryEntry memory)``
- ``Task UpdateMemoryAsync(MemoryEntry memory)``
- ``Task DeleteMemoryAsync(int id)``
- ``Task<List<MemoryEntry>> SearchMemoriesAsync(string query)``

### New Service: ``MemoryService.cs`` in ``Services/``
- **Auto-extraction**: After each AI response, send a follow-up prompt to the model asking it to extract any user facts/preferences from the conversation (name, preferences, instructions, facts about projects, etc.)
- **Memory injection**: Before generating responses, query relevant memories and prepend them to the system prompt as ``[Memory] User's name is Avnish. User prefers C#.``
- **Deduplication**: Check for existing similar memories before adding new ones
- **Relevance scoring**: Prioritize frequently used and recently used memories

### UI Changes
- **Settings tab** (``SettingsView.xaml``): Add a "Memory" section with:
  - Toggle to enable/disable AI memory
  - Button to "View & Manage Memories" → opens a dialog/page listing all memories
  - Button to "Clear All Memories"
- **Chat input area** (``ChatView.xaml``): Small indicator icon showing memory is active
- **Memory management dialog**: List view showing all memories with edit/delete per entry

### Integration with ``ChatViewModel.cs``
- Modify ``SendMessageAsync()`` to:
  1. Query ``MemoryService`` for relevant memories before building the prompt
  2. After receiving the AI response, call ``MemoryService.ExtractMemoriesAsync()`` in background
  3. Pass memory context via the existing ``sessionContext`` parameter in ``IChatService.GenerateResponseStreamAsync()``

## Files to Create/Modify
- **[NEW]** ``Models/MemoryEntry.cs``
- **[NEW]** ``Services/MemoryService.cs`` + ``IMemoryService.cs``
- **[MODIFY]** ``Services/DatabaseService.cs`` — add Memories table + CRUD
- **[MODIFY]** ``ViewModels/ChatViewModel.cs`` — integrate memory injection/extraction
- **[MODIFY]** ``ViewModels/SettingsViewModel.cs`` — add memory settings
- **[MODIFY]** ``Views/SettingsView.xaml`` — add Memory section UI
- **[MODIFY]** ``Views/ChatView.xaml`` — add memory indicator

## Acceptance Criteria
- [ ] Memories persist across app restarts (stored in SQLite)
- [ ] AI automatically extracts facts from conversations
- [ ] Relevant memories are injected into prompts
- [ ] Users can view, edit, and delete individual memories
- [ ] Memory can be toggled on/off in Settings
- [ ] No noticeable performance impact on chat speed
"@

gh issue create --repo $repo --title "Feature: AI Memory — Persistent Context Across Sessions" --body $body1 --label "feature,tier-1-high-impact,WinUI"

# Issue 2: Voice Input (STT)
$body2 = @"
## Summary
Add Speech-to-Text (STT) voice input capability so users can speak to the AI instead of typing. Use local Whisper-based transcription for privacy.

## Motivation
Voice input is essential for accessibility, hands-free usage, and a modern conversational AI experience. Many users prefer speaking over typing for longer prompts.

## Detailed Requirements

### NuGet Dependencies
Add to ``KaiROS.AI.WinUI.csproj``:
- ``Whisper.net`` (C# bindings for whisper.cpp) OR
- ``NAudio`` (for audio capture) + Windows built-in speech recognition as fallback
- Consider ``Microsoft.CognitiveServices.Speech`` as optional cloud fallback

### New Service: ``SpeechToTextService.cs``
```csharp
public interface ISpeechToTextService
{
    bool IsListening { get; }
    bool IsAvailable { get; }
    event EventHandler<string> TranscriptionCompleted;
    event EventHandler<string> PartialTranscription; // Real-time partial results
    event EventHandler<float> AudioLevelChanged;     // For visual feedback
    Task StartListeningAsync(CancellationToken ct = default);
    Task StopListeningAsync();
    Task<string> TranscribeFileAsync(string audioFilePath);
}
```

Implementation approach:
1. **Primary**: Use ``Whisper.net`` with a small model (``ggml-base.en.bin``, ~142MB) for local transcription
2. **Fallback**: Use Windows ``SpeechRecognizer`` API (``Windows.Media.SpeechRecognition``) which requires no model download
3. **Audio capture**: Use ``NAudio`` or ``Windows.Media.Capture.MediaCapture`` to record from microphone

### Model Management
- Auto-download Whisper model on first use (similar to how LLM models are downloaded)
- Store in ``%LocalAppData%/KaiROS.AI/whisper-models/``
- Settings option to choose model size: tiny (75MB), base (142MB), small (466MB)

### UI Changes in ``ChatView.xaml``
- Add a **microphone button** (🎤) next to the Send button in the chat input area
- **States**:
  - Idle: Gray microphone icon
  - Listening: Pulsing red microphone with audio level visualization
  - Transcribing: Spinning indicator
- **Behavior**:
  - Click to start recording, click again (or press Enter/Escape) to stop
  - Real-time partial transcription shown in the input TextBox as user speaks
  - Final transcription fills the TextBox; user can edit before sending
  - Optional: Auto-send after silence detection (configurable in Settings)

### Settings (``SettingsView.xaml``)
- "Voice Input" section:
  - Toggle: Enable/Disable voice input
  - Dropdown: Whisper model size (tiny/base/small)
  - Dropdown: Audio input device selection
  - Toggle: Auto-send after silence (with configurable silence duration slider)
  - Button: Download/Update Whisper model

### Integration with ``ChatViewModel.cs``
- Add ``StartVoiceInputCommand`` and ``StopVoiceInputCommand``
- Add ``IsListening`` observable property
- Wire transcription result to ``UserInput`` property
- Handle microphone permissions via ``Package.appxmanifest`` capability

### Package Manifest
Add to ``Package.appxmanifest``:
```xml
<DeviceCapability Name="microphone"/>
```

## Files to Create/Modify
- **[NEW]** ``Services/SpeechToTextService.cs`` + ``ISpeechToTextService.cs``
- **[MODIFY]** ``Views/ChatView.xaml`` — add mic button + recording UI
- **[MODIFY]** ``ViewModels/ChatViewModel.cs`` — add voice input commands
- **[MODIFY]** ``Views/SettingsView.xaml`` — add voice settings section
- **[MODIFY]** ``ViewModels/SettingsViewModel.cs`` — add voice settings
- **[MODIFY]** ``Package.appxmanifest`` — add microphone capability
- **[MODIFY]** ``KaiROS.AI.WinUI.csproj`` — add NuGet packages

## Acceptance Criteria
- [ ] Microphone button visible in chat input area
- [ ] Click starts recording with visual feedback (pulsing, audio level)
- [ ] Transcription appears in input TextBox
- [ ] Works offline using local Whisper model
- [ ] Whisper model downloads automatically on first use
- [ ] Settings allow model size selection and input device choice
- [ ] Graceful error handling if no microphone is available
"@

gh issue create --repo $repo --title "Feature: Voice Input (Speech-to-Text) with Local Whisper" --body $body2 --label "feature,tier-1-high-impact,WinUI"

# Issue 3: Text-to-Speech
$body3 = @"
## Summary
Add Text-to-Speech (TTS) so the AI can read its responses aloud. Use Windows built-in speech synthesis for zero-dependency implementation.

## Motivation
TTS completes the voice conversation experience, enables accessibility for visually impaired users, and allows hands-free consumption of AI responses.

## Detailed Requirements

### New Service: ``TextToSpeechService.cs``
```csharp
public interface ITextToSpeechService
{
    bool IsSpeaking { get; }
    bool IsAvailable { get; }
    List<VoiceInfo> GetAvailableVoices();
    Task SpeakAsync(string text, CancellationToken ct = default);
    Task StopAsync();
    Task SetVoiceAsync(string voiceId);
    Task SetRateAsync(double rate);  // 0.5 to 2.0
    Task SetVolumeAsync(double volume); // 0.0 to 1.0
}
```

Implementation:
- Use ``Windows.Media.SpeechSynthesis.SpeechSynthesizer`` — built into Windows, no downloads needed
- Stream audio via ``MediaPlayer`` for smooth playback
- Parse markdown from AI responses to extract plain text before speaking (strip code blocks, links, etc.)

### UI Changes
- **Per-message "Read Aloud" button** (🔊) on each AI response bubble in ``ChatView.xaml``
- **Global TTS toggle** in the chat toolbar — when enabled, auto-reads each new AI response
- **Speaking indicator**: Show a sound wave animation on the message being read
- **Stop button**: Replace play with stop while speaking

### Settings (``SettingsView.xaml``)
- "Voice Output" section:
  - Toggle: Enable TTS
  - Dropdown: Voice selection (list system voices)
  - Slider: Speech rate (0.5x - 2.0x)
  - Slider: Volume
  - Toggle: Auto-read new responses

### ChatView.xaml Message Template Changes
Add to the AI message ``DataTemplate``:
```xml
<Button Command="{Binding ReadAloudCommand}" CommandParameter="{Binding Content}"
        Style="{StaticResource IconButtonStyle}" ToolTipService.ToolTip="Read aloud">
    <FontIcon Glyph="&#xE767;" FontSize="14"/>
</Button>
```

## Files to Create/Modify
- **[NEW]** ``Services/TextToSpeechService.cs`` + ``ITextToSpeechService.cs``
- **[MODIFY]** ``Views/ChatView.xaml`` — add Read Aloud buttons per message + global toggle
- **[MODIFY]** ``ViewModels/ChatViewModel.cs`` — add TTS commands
- **[MODIFY]** ``Views/SettingsView.xaml`` — add TTS settings
- **[MODIFY]** ``ViewModels/SettingsViewModel.cs`` — add TTS settings

## Acceptance Criteria
- [ ] Read Aloud button on each AI message
- [ ] Windows voices listed and selectable in Settings
- [ ] Rate and volume adjustable
- [ ] Auto-read mode works for streaming responses
- [ ] Stop button cancels speech mid-read
- [ ] Markdown is stripped before speaking (no reading ``### heading`` or ``\`\`\`code\`\`\```)
- [ ] Works immediately with no downloads (uses Windows built-in voices)
"@

gh issue create --repo $repo --title "Feature: Text-to-Speech (TTS) Voice Output" --body $body3 --label "feature,tier-1-high-impact,WinUI"

# Issue 4: Deep Research
$body4 = @"
## Summary
Add a Deep Research mode that performs multi-step web research — generating search queries, reading multiple sources, and synthesizing findings into a structured report with citations.

## Motivation
The existing web search feature returns snippets from a single query. Deep Research performs iterative, multi-step research like a human researcher — significantly more valuable for knowledge work.

## Detailed Requirements

### New Service: ``DeepResearchService.cs``
```csharp
public interface IDeepResearchService
{
    IAsyncEnumerable<ResearchProgress> RunResearchAsync(string query, ResearchOptions options, CancellationToken ct);
}

public class ResearchProgress
{
    public ResearchPhase Phase { get; set; }
    public string StatusMessage { get; set; }
    public int CurrentStep { get; set; }
    public int TotalSteps { get; set; }
    public List<ResearchSource> SourcesFound { get; set; }
    public string PartialReport { get; set; }
}

public enum ResearchPhase
{
    PlanningQueries,
    Searching,
    ReadingSources,
    Analyzing,
    WritingReport,
    Complete
}

public class ResearchSource
{
    public string Title { get; set; }
    public string Url { get; set; }
    public string Snippet { get; set; }
    public string FullContent { get; set; }
    public bool IsRead { get; set; }
}
```

### Research Pipeline (in ``DeepResearchService.cs``)
1. **Query Planning**: Send user's question to LLM with prompt: "Generate 3-5 specific search queries to thoroughly research: {question}"
2. **Web Search**: Use existing ``IWebSearchService`` to search each query
3. **Source Reading**: Fetch full page content for top 3-5 unique URLs using ``HttpClient`` + HTML-to-text extraction (use ``HtmlAgilityPack`` NuGet)
4. **Analysis**: Send all source content to LLM: "Based on these sources, identify key findings, contradictions, and gaps"
5. **Report Generation**: Generate final structured report with: Executive Summary, Key Findings, Detailed Analysis, Sources (with inline citations)
6. **Yield progress** at each phase for real-time UI updates

### UI: Research Panel
Option A: Add as a mode in existing ``ChatView.xaml`` (toggle button: Chat | Research)
Option B: New navigation item "Research" with dedicated view

**Recommended: Option A** — Add a "Deep Research" toggle/button in the chat toolbar.

When Deep Research is active:
- Show a multi-step progress panel with phases and status
- Display sources being read (URL list with checkmarks)
- Stream the final report in the chat area
- Report includes clickable citation links

### Dependencies
Add to ``KaiROS.AI.WinUI.csproj``:
- ``HtmlAgilityPack`` — HTML parsing for reading web pages

## Files to Create/Modify
- **[NEW]** ``Services/DeepResearchService.cs`` + ``IDeepResearchService.cs``
- **[NEW]** ``Models/ResearchModels.cs`` (ResearchProgress, ResearchSource, ResearchOptions)
- **[MODIFY]** ``Views/ChatView.xaml`` — add research mode toggle + progress UI
- **[MODIFY]** ``ViewModels/ChatViewModel.cs`` — add research command + progress binding
- **[MODIFY]** ``KaiROS.AI.WinUI.csproj`` — add HtmlAgilityPack NuGet

## Acceptance Criteria
- [ ] User can trigger Deep Research from chat with a toggle/button
- [ ] Research runs through all 5 phases with real-time progress
- [ ] Sources are displayed with titles and URLs
- [ ] Final report is well-structured with citations
- [ ] Report is streamable (token-by-token display)
- [ ] User can cancel mid-research
- [ ] Research conversations are saved to chat history
"@

gh issue create --repo $repo --title "Feature: Deep Research — Multi-Step Web Research with Reports" --body $body4 --label "feature,tier-1-high-impact,WinUI"

# Issue 5: Agent Mode (Tool Use)
$body5 = @"
## Summary
Add an Agent Mode where the AI can use tools (file system, web fetch, calculator, code execution, system info) to autonomously complete multi-step tasks.

## Motivation
Agent mode transforms KaiROS from a chatbot into a true AI assistant that can *act*, not just *talk*. This is the most requested feature in local AI applications.

## Detailed Requirements

### Tool Framework
Create a plugin-style tool system:

```csharp
public interface ITool
{
    string Name { get; }
    string Description { get; }
    string ParametersJsonSchema { get; }  // JSON Schema for function calling
    Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken ct);
}

public class ToolResult
{
    public bool Success { get; set; }
    public string Output { get; set; }
    public string Error { get; set; }
}
```

### Built-in Tools (``Services/Tools/``)
1. **FileReaderTool**: Read file contents (with path validation/sandboxing)
2. **FileWriterTool**: Write/create files (with user confirmation)
3. **WebFetchTool**: Fetch and extract text from a URL
4. **CalculatorTool**: Evaluate math expressions
5. **SystemInfoTool**: Get system info (CPU, RAM, disk, running processes)
6. **DateTimeTool**: Get current date/time, timezone conversions
7. **ClipboardTool**: Read/write clipboard

### Agent Service: ``AgentService.cs``
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
2. If LLM returns a tool call → execute tool → feed result back to LLM
3. Repeat until LLM gives final text response (max 10 iterations)
4. Display each step in the UI (thinking → tool call → result → thinking → response)

### UI Changes (``ChatView.xaml``)
- **Agent Mode toggle** in chat toolbar (distinct from regular chat)
- **Step-by-step display**: Show each agent step in the chat:
  - 🧠 Thinking: "I need to read the file..."
  - 🔧 Tool Call: ``read_file(path="/Users/...")`` 
  - 📋 Result: File contents (collapsible)
  - 💬 Final Response
- **Tool approval**: For destructive tools (file write, clipboard), show confirmation dialog before execution
- **Settings**: Configure which tools are enabled/disabled

### Security
- File operations sandboxed to user-specified directories
- All destructive actions require user approval
- Maximum iteration limit (10) to prevent infinite loops
- Token budget per agent run

## Files to Create/Modify
- **[NEW]** ``Services/AgentService.cs`` + ``IAgentService.cs``
- **[NEW]** ``Services/Tools/ITool.cs``
- **[NEW]** ``Services/Tools/FileReaderTool.cs``
- **[NEW]** ``Services/Tools/FileWriterTool.cs``
- **[NEW]** ``Services/Tools/WebFetchTool.cs``
- **[NEW]** ``Services/Tools/CalculatorTool.cs``
- **[NEW]** ``Services/Tools/SystemInfoTool.cs``
- **[NEW]** ``Services/Tools/DateTimeTool.cs``
- **[NEW]** ``Services/Tools/ClipboardTool.cs``
- **[NEW]** ``Models/AgentModels.cs``
- **[MODIFY]** ``Views/ChatView.xaml`` — agent mode UI + step display
- **[MODIFY]** ``ViewModels/ChatViewModel.cs`` — agent commands
- **[MODIFY]** ``Views/SettingsView.xaml`` — tool configuration

## Acceptance Criteria
- [ ] Agent mode toggle in chat interface
- [ ] At least 5 built-in tools working
- [ ] Multi-step agent execution with visible steps
- [ ] Tool calls displayed with expandable input/output
- [ ] Destructive actions require user confirmation
- [ ] Agent respects iteration limit
- [ ] Agent conversations saved to chat history
"@

gh issue create --repo $repo --title "Feature: Agent Mode — Tool-Using AI for Multi-Step Tasks" --body $body5 --label "feature,tier-1-high-impact,WinUI"

Write-Host "Tier 1 issues created (1-5)"

# Issue 6: YouTube Summarization
$body6 = @"
## Summary
Add YouTube video summarization — paste a YouTube URL and get an AI-generated summary of the video transcript.

## Motivation
One of the most requested features in AI tools. Very low effort to implement but provides high user value.

## Detailed Requirements

### New Service: ``YouTubeService.cs``
- Extract video ID from URL (support youtube.com/watch?v= and youtu.be/ formats)
- Fetch transcript using YouTube's timedtext API (no API key needed for auto-generated captions)
- Fallback: Use ``yt-dlp`` CLI if available for more reliable transcript extraction
- Clean up transcript (remove timestamps, merge segments)

### Chat Integration
- Detect YouTube URLs pasted in chat input
- Show a preview card: video title, thumbnail, duration
- Auto-suggest: "Would you like me to summarize this video?"
- Send transcript as context to LLM with prompt: "Summarize this YouTube video transcript. Provide key points, main arguments, and a brief summary."

### UI
- YouTube URL detection in ``ChatViewModel.cs`` with link preview
- Summary displayed as a structured response with sections

## Files to Create/Modify
- **[NEW]** ``Services/YouTubeService.cs`` + ``IYouTubeService.cs``
- **[MODIFY]** ``ViewModels/ChatViewModel.cs`` — URL detection + summary command
- **[MODIFY]** ``Views/ChatView.xaml`` — URL preview card (optional)

## Acceptance Criteria
- [ ] Paste YouTube URL → transcript fetched automatically
- [ ] AI generates structured summary
- [ ] Works with auto-generated captions
- [ ] Handles videos without captions gracefully
"@

gh issue create --repo $repo --title "Feature: YouTube Video Summarization" --body $body6 --label "feature,tier-2-productivity,WinUI"

# Issue 7: Model Comparison
$body7 = @"
## Summary
Add side-by-side model comparison — send the same prompt to two loaded models simultaneously and compare their outputs.

## Motivation
Helps users evaluate which model performs best for their use case. Great for benchmarking and testing.

## Detailed Requirements

### UI: Compare View
- Add "Compare" mode toggle or new NavigationView item (Tag="4")
- Split-pane layout: two chat columns side by side
- Model selector dropdown at top of each column
- Shared input field at bottom — sends to both models simultaneously
- Performance stats displayed per column (tokens/sec, total tokens)

### Implementation
- Reuse existing ``IChatService`` but create two instances or run two inference sessions
- Note: LLamaSharp may not support two models simultaneously in memory — handle this:
  - Option A: Sequential execution (run model A, then model B)
  - Option B: Swap models between runs
  - Display both results when complete

### ViewModel: ``CompareViewModel.cs``
- Two model selections, shared input, parallel/sequential execution
- Side-by-side results with timing stats

## Files to Create/Modify
- **[NEW]** ``Views/CompareView.xaml`` + ``CompareView.xaml.cs``
- **[NEW]** ``ViewModels/CompareViewModel.cs``
- **[MODIFY]** ``MainWindow.xaml`` — add Compare nav item
- **[MODIFY]** ``MainWindow.xaml.cs`` — handle Compare navigation

## Acceptance Criteria
- [ ] Split-pane comparison view with model selectors
- [ ] Same prompt sent to both models
- [ ] Results and performance stats displayed side by side
- [ ] Clear visual indication of which model produced which output
"@

gh issue create --repo $repo --title "Feature: Side-by-Side Model Comparison" --body $body7 --label "feature,tier-2-productivity,WinUI"

# Issue 8: Backup/Restore
$body8 = @"
## Summary
Add backup and restore functionality — export all app data (chat history, settings, custom models, RaaS configs, memories) as a ZIP file and import on any device.

## Motivation
Data portability, disaster recovery, and migration between devices. Essential for user trust.

## Detailed Requirements

### Backup Service: ``BackupService.cs``
```csharp
public interface IBackupService
{
    Task<string> CreateBackupAsync(string outputPath, BackupOptions options);
    Task RestoreBackupAsync(string backupFilePath);
    Task<BackupInfo> InspectBackupAsync(string backupFilePath);
}

public class BackupOptions
{
    public bool IncludeChatHistory { get; set; } = true;
    public bool IncludeSettings { get; set; } = true;
    public bool IncludeCustomModels { get; set; } = true;
    public bool IncludeRaasConfigs { get; set; } = true;
    public bool IncludeMemories { get; set; } = true;
    // Note: Do NOT include downloaded model files (too large)
}
```

### Backup Format
ZIP file containing:
- ``kairos.db`` — SQLite database copy
- ``settings.json`` — app settings export
- ``metadata.json`` — backup timestamp, app version, machine info
- ``chat_history/`` — exported sessions as JSON (optional, for readability)

### UI in Settings
- "Backup & Restore" section in ``SettingsView.xaml``
- "Create Backup" button → file save dialog → creates ZIP
- "Restore Backup" button → file open dialog → confirmation → restore
- Checkboxes for what to include in backup
- Display last backup date

## Files to Create/Modify
- **[NEW]** ``Services/BackupService.cs`` + ``IBackupService.cs``
- **[NEW]** ``Models/BackupModels.cs``
- **[MODIFY]** ``Views/SettingsView.xaml`` — add Backup section
- **[MODIFY]** ``ViewModels/SettingsViewModel.cs`` — add backup commands

## Acceptance Criteria
- [ ] Backup creates a ZIP with all selected data
- [ ] Restore replaces current data with backup data
- [ ] Confirmation dialog before restore (warns about data replacement)
- [ ] Backup file includes metadata (version, date)
- [ ] Model binary files are NOT included (too large)
"@

gh issue create --repo $repo --title "Feature: Backup & Restore — Data Export/Import" --body $body8 --label "feature,tier-2-productivity,WinUI"

# Issue 9: Notes System
$body9 = @"
## Summary
Add a built-in Notes system with a markdown editor. Users can create, organize, and AI-assist their notes.

## Motivation
Makes KaiROS a productivity hub. Notes can be used as context for AI conversations and vice versa.

## Detailed Requirements

### Data Model: ``Note.cs``
```csharp
public class Note
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; }
    public string Content { get; set; }  // Markdown
    public string Category { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool IsPinned { get; set; }
}
```

### Database (``DatabaseService.cs``)
Add ``Notes`` table + CRUD methods.

### UI: ``NotesView.xaml``
- Left panel: note list (searchable, sortable by date/title, pin support)
- Right panel: markdown editor with live preview
- Toolbar: New, Delete, Pin, "Ask AI about this note", Export
- AI actions: "Summarize", "Expand", "Rewrite", "Generate action items"

### Navigation
Add "Notes" item to ``MainWindow.xaml`` NavigationView (Tag="4").

## Files to Create/Modify
- **[NEW]** ``Models/Note.cs``
- **[NEW]** ``Views/NotesView.xaml`` + ``NotesView.xaml.cs``
- **[NEW]** ``ViewModels/NotesViewModel.cs``
- **[NEW]** ``Services/NotesService.cs``
- **[MODIFY]** ``Services/DatabaseService.cs`` — add Notes table
- **[MODIFY]** ``MainWindow.xaml`` — add Notes nav item
- **[MODIFY]** ``MainWindow.xaml.cs`` — handle Notes navigation

## Acceptance Criteria
- [ ] Create, edit, delete notes with markdown support
- [ ] Notes persist in SQLite database
- [ ] Search notes by title/content
- [ ] Pin important notes
- [ ] AI can summarize/expand/rewrite notes
- [ ] Notes accessible from navigation sidebar
"@

gh issue create --repo $repo --title "Feature: Notes System with Markdown Editor & AI Assist" --body $body9 --label "feature,tier-2-productivity,WinUI"

# Issue 10: Task Management
$body10 = @"
## Summary
Add a task/to-do management system. AI can extract action items from conversations and users can manage tasks.

## Motivation
Keeps users inside KaiROS for daily productivity. AI-powered task extraction is a unique differentiator.

## Detailed Requirements

### Data Model: ``TaskItem.cs``
```csharp
public class TaskItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; }
    public string Description { get; set; }
    public TaskPriority Priority { get; set; }
    public TaskStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string SourceSessionId { get; set; }  // Chat session that created it
}
```

### AI Integration
- "Extract Tasks" button in chat — AI identifies action items from conversation
- Auto-suggest: After lengthy AI response, offer to extract tasks
- Natural language: "Add a task to review the code by Friday"

### UI: ``TasksView.xaml``
- Task list with filters (All, Active, Completed, overdue)
- Inline editing, priority badges, due dates
- "Extract from Chat" button

### Navigation
Add "Tasks" to ``MainWindow.xaml`` NavigationView.

## Files to Create/Modify
- **[NEW]** ``Models/TaskItem.cs``
- **[NEW]** ``Views/TasksView.xaml`` + ``TasksView.xaml.cs``
- **[NEW]** ``ViewModels/TasksViewModel.cs``
- **[NEW]** ``Services/TaskService.cs``
- **[MODIFY]** ``Services/DatabaseService.cs`` — add Tasks table
- **[MODIFY]** ``MainWindow.xaml`` — add Tasks nav item
- **[MODIFY]** ``ViewModels/ChatViewModel.cs`` — add "Extract Tasks" command

## Acceptance Criteria
- [ ] Create, edit, complete, delete tasks
- [ ] AI extracts action items from chat conversations
- [ ] Tasks have priority, status, and optional due dates
- [ ] Filter by status (active/completed)
- [ ] Tasks persist in SQLite
"@

gh issue create --repo $repo --title "Feature: Task Management with AI Extraction" --body $body10 --label "feature,tier-2-productivity,WinUI"

Write-Host "Tier 2 issues created (6-10)"

# Issue 11: Document Management Library
$body11 = @"
## Summary
Extend the existing RAG document support into a full Document Management Library — upload, organize, tag, and search documents. Documents can be used as context for any chat.

## Motivation
Currently documents are only used per-RAG-session. A persistent document library lets users build a personal knowledge base.

## Detailed Requirements

### Enhancements to existing ``DocumentView.xaml``
- Add folder/category organization
- Tag documents with custom labels
- Full-text search across all documents
- Document preview panel
- Bulk operations (select multiple, delete, re-index)
- "Chat with this document" quick action → opens chat with RAG context

### Database Changes
Add ``DocumentLibrary`` table with: Id, FileName, FilePath, FileType, FileSize, Tags, Category, IndexedAt, ChunkCount.

### Service Enhancements (``DocumentService.cs``)
- Persistent document indexing (currently seems per-session)
- Background re-indexing when documents change
- Cross-document search using keyword matching

## Files to Create/Modify
- **[MODIFY]** ``Views/DocumentView.xaml`` — add library features
- **[MODIFY]** ``ViewModels/DocumentViewModel.cs`` — add library management
- **[MODIFY]** ``Services/DocumentService.cs`` — persistent indexing
- **[MODIFY]** ``Services/DatabaseService.cs`` — add DocumentLibrary table

## Acceptance Criteria
- [ ] Documents organized in categories with tags
- [ ] Full-text search across all indexed documents
- [ ] Quick "Chat with document" action
- [ ] Documents persist across app restarts
- [ ] Bulk operations supported
"@

gh issue create --repo $repo --title "Feature: Document Management Library" --body $body11 --label "feature,tier-3-advanced,WinUI"

# Issue 12: Semantic Search with Embeddings
$body12 = @"
## Summary
Add embedding-based semantic search across chat history and documents using LLamaSharp's embedding support.

## Motivation
Current search is keyword-based. Semantic search finds related content by meaning, dramatically improving the search experience.

## Detailed Requirements

### Service: ``EmbeddingService.cs``
- Use ``LLamaEmbedder`` from LLamaSharp to generate embeddings
- Store embeddings in SQLite as BLOB (or use a simple vector store)
- Cosine similarity search for finding related content
- Index: chat messages, documents, notes (when those features exist)

### Use Cases
- Search chat history: "What did I ask about Python?" finds messages about Python even if the word isn't used
- Find related documents in RAG
- Memory retrieval (supports AI Memory feature)

### UI
- Enhanced search in chat history sidebar with "Semantic" toggle
- Search results ranked by relevance score

## Files to Create/Modify
- **[NEW]** ``Services/EmbeddingService.cs`` + ``IEmbeddingService.cs``
- **[MODIFY]** ``Services/DatabaseService.cs`` — add Embeddings table
- **[MODIFY]** ``ViewModels/ChatViewModel.cs`` — integrate semantic search
- **[MODIFY]** ``Views/ChatView.xaml`` — semantic search toggle

## Acceptance Criteria
- [ ] Embeddings generated for chat messages
- [ ] Semantic search returns relevant results by meaning
- [ ] Search results ranked by cosine similarity score
- [ ] Background indexing doesn't impact chat performance
"@

gh issue create --repo $repo --title "Feature: Semantic Search with Embeddings" --body $body12 --label "feature,tier-3-advanced,WinUI"

# Issue 13: Skills / Prompt Templates
$body13 = @"
## Summary
Add a Skills/Prompt Template system — pre-configured prompt templates with variables that users can create, share, and apply.

## Motivation
Power users can create reusable AI behaviors (e.g., "Code Reviewer", "Email Writer", "Meeting Summarizer") without understanding prompt engineering.

## Detailed Requirements

### Data Model: ``Skill.cs``
```csharp
public class Skill
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Icon { get; set; }          // Segoe Fluent Icons glyph
    public string SystemPrompt { get; set; }   // The actual prompt template
    public string Category { get; set; }       // "Writing", "Code", "Analysis"
    public List<SkillVariable> Variables { get; set; }  // {{input}}, {{language}}
    public bool IsBuiltIn { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### Built-in Skills
Ship with 10+ pre-built skills: Code Reviewer, Email Writer, Meeting Summarizer, Translator, Explain Like I'm 5, Blog Post Writer, SQL Generator, Regex Helper, Grammar Fixer, Debate Partner.

### UI
- Skills panel accessible from chat toolbar (dropdown or flyout)
- Skill editor for creating/editing custom skills
- Variable substitution UI (fill in {{language}}, {{tone}} etc.)
- Skill activates by setting system prompt + any variables

## Files to Create/Modify
- **[NEW]** ``Models/Skill.cs``
- **[NEW]** ``Services/SkillService.cs``
- **[NEW]** ``Views/SkillsPanel.xaml`` (flyout or dialog)
- **[MODIFY]** ``Services/DatabaseService.cs`` — add Skills table
- **[MODIFY]** ``Views/ChatView.xaml`` — add skills button
- **[MODIFY]** ``ViewModels/ChatViewModel.cs`` — skill activation

## Acceptance Criteria
- [ ] 10+ built-in skills shipped with the app
- [ ] Users can create custom skills with variables
- [ ] Skills apply system prompt to chat context
- [ ] Skills accessible from chat toolbar
- [ ] Custom skills persist in database
"@

gh issue create --repo $repo --title "Feature: Skills / Prompt Template System" --body $body13 --label "feature,tier-3-advanced,WinUI"

# Issue 14: Image Generation
$body14 = @"
## Summary
Add AI image generation capability — generate images from text prompts using Stable Diffusion (local) or API-based providers.

## Motivation
Visual content creation is a popular AI use case. Integrating it keeps users in the KaiROS ecosystem.

## Detailed Requirements

### Service: ``ImageGenerationService.cs``
- Support multiple backends:
  1. **API-based**: OpenAI DALL-E, Stability AI (via REST API)
  2. **Local**: Stable Diffusion via API (user runs ComfyUI/A1111 locally)
- User configures backend in Settings (API URL + key)

### Chat Integration
- Detect image generation intent in user messages (e.g., "generate an image of...")
- Display generated image inline in chat
- Save generated images to a gallery folder
- Support: prompt, negative prompt, size, steps configuration

### UI
- Generated images displayed in chat message bubbles
- Right-click → Save Image As...
- Settings: configure image gen backend (URL, API key)

## Files to Create/Modify
- **[NEW]** ``Services/ImageGenerationService.cs`` + ``IImageGenerationService.cs``
- **[MODIFY]** ``ViewModels/ChatViewModel.cs`` — detect image requests
- **[MODIFY]** ``Views/ChatView.xaml`` — image display in chat
- **[MODIFY]** ``Views/SettingsView.xaml`` — image gen settings

## Acceptance Criteria
- [ ] Generate images from text prompts via API
- [ ] Images displayed inline in chat
- [ ] Support local Stable Diffusion API (ComfyUI/A1111)
- [ ] Images saveable to disk
- [ ] Configurable backend in Settings
"@

gh issue create --repo $repo --title "Feature: AI Image Generation Integration" --body $body14 --label "feature,tier-3-advanced,WinUI"

# Issue 15: Shell/Terminal Access
$body15 = @"
## Summary
Add sandboxed shell/terminal access — the AI can execute PowerShell commands on the user's machine with approval.

## Motivation
Developer productivity feature. "Check disk space", "list running processes", "run this build command" — very useful for power users.

## Detailed Requirements

### Service: ``ShellService.cs``
- Execute PowerShell commands with timeout
- Capture stdout, stderr, exit code
- **Security**: All commands require user approval via confirmation dialog
- Configurable allowed/blocked command patterns
- Working directory configuration
- Output size limit (truncate large outputs)

### Agent Integration
- Works as a tool in Agent Mode (Issue #5)
- Also usable standalone: user types ``/run dir`` in chat → executes and shows output

### UI
- Command output displayed in chat with monospace formatting
- Approval dialog: "KaiROS wants to run: ``Get-Process | Select -First 10``. Allow?"
- Settings: enable/disable shell access, set allowed directories

## Files to Create/Modify
- **[NEW]** ``Services/ShellService.cs`` + ``IShellService.cs``
- **[MODIFY]** ``ViewModels/ChatViewModel.cs`` — /run command handling
- **[MODIFY]** ``Views/ChatView.xaml`` — command output display
- **[MODIFY]** ``Views/SettingsView.xaml`` — shell access settings

## Acceptance Criteria
- [ ] Execute PowerShell commands from chat
- [ ] All commands require user approval
- [ ] Output displayed in monospace format
- [ ] Timeout and output size limits enforced
- [ ] Enable/disable toggle in Settings
"@

gh issue create --repo $repo --title "Feature: Sandboxed Shell/Terminal Access" --body $body15 --label "feature,tier-3-advanced,WinUI"

Write-Host "Tier 3 issues created (11-15)"

# Issue 16: MCP Protocol Support
$body16 = @"
## Summary
Add Model Context Protocol (MCP) client support — connect to external MCP tool servers to extend KaiROS capabilities.

## Motivation
MCP is becoming the standard protocol for AI tool integration (created by Anthropic). Supporting it future-proofs KaiROS and allows connection to a growing ecosystem of MCP servers.

## Detailed Requirements

### MCP Client Service: ``McpClientService.cs``
- Implement MCP client protocol (JSON-RPC over stdio/SSE)
- Connect to user-configured MCP servers
- Discover available tools from connected servers
- Execute tool calls and return results to the LLM

### Configuration
- Settings UI to add/remove MCP server connections
- Each server: name, command (for stdio) or URL (for SSE), environment variables
- Connection status indicator

### Integration with Agent Mode
- MCP tools appear alongside built-in tools in Agent Mode
- Tool schemas auto-discovered from MCP server

### NuGet
- Consider ``ModelContextProtocol`` NuGet package if available, or implement minimal client

## Files to Create/Modify
- **[NEW]** ``Services/McpClientService.cs`` + ``IMcpClientService.cs``
- **[NEW]** ``Models/McpModels.cs``
- **[MODIFY]** ``Views/SettingsView.xaml`` — MCP server configuration
- **[MODIFY]** ``ViewModels/SettingsViewModel.cs`` — MCP settings
- **[MODIFY]** ``Services/AgentService.cs`` — integrate MCP tools (depends on Agent Mode issue)

## Acceptance Criteria
- [ ] Connect to MCP servers via stdio or SSE transport
- [ ] Discover and list available tools from connected servers
- [ ] Execute MCP tool calls from agent mode
- [ ] Add/remove servers in Settings
- [ ] Connection status indicator
"@

gh issue create --repo $repo --title "Feature: MCP (Model Context Protocol) Client Support" --body $body16 --label "feature,tier-3-advanced,WinUI"

# Issue 17: Calendar Integration
$body17 = @"
## Summary
Add calendar integration — view, create, and manage events via Google Calendar or CalDAV. AI can query and create calendar events.

## Motivation
A personal AI assistant that knows your schedule is significantly more useful.

## Detailed Requirements

### Service: ``CalendarService.cs``
- Support Google Calendar API (OAuth2) and/or CalDAV protocol
- CRUD operations for events
- Query: "What's on my schedule today?"
- Create: "Schedule a meeting with John tomorrow at 3 PM"

### UI
- Calendar widget in sidebar or dedicated view
- Today's events summary
- Natural language event creation via chat

### Settings
- Calendar account configuration (Google OAuth or CalDAV URL)
- Sync frequency settings

## Files to Create/Modify
- **[NEW]** ``Services/CalendarService.cs`` + ``ICalendarService.cs``
- **[NEW]** ``Models/CalendarEvent.cs``
- **[NEW]** ``Views/CalendarView.xaml`` (optional)
- **[MODIFY]** ``Views/SettingsView.xaml`` — calendar account config
- **[MODIFY]** ``ViewModels/ChatViewModel.cs`` — calendar queries

## Acceptance Criteria
- [ ] Connect to at least one calendar provider
- [ ] View today's events
- [ ] AI can query calendar ("what's on my schedule?")
- [ ] AI can create events from natural language
- [ ] Events synced from external calendar
"@

gh issue create --repo $repo --title "Feature: Calendar Integration (Google Calendar / CalDAV)" --body $body17 --label "feature,tier-3-advanced,WinUI"

# Issue 18: Webhooks
$body18 = @"
## Summary
Add webhook support to the REST API server — send notifications and allow external automation triggers.

## Motivation
Connects KaiROS to external workflows (Zapier, IFTTT, n8n, Power Automate).

## Detailed Requirements

### Webhook Endpoints in ``ApiServer.cs``
- ``POST /api/webhooks`` — register a webhook (URL, events, secret)
- ``DELETE /api/webhooks/{id}`` — remove a webhook
- ``GET /api/webhooks`` — list registered webhooks

### Events
- ``chat.completed`` — fires when AI generates a response
- ``model.loaded`` — fires when a model is loaded
- ``model.unloaded`` — fires when a model is unloaded
- ``research.completed`` — fires when deep research finishes

### Implementation
- Store webhook registrations in SQLite
- On event trigger, POST JSON payload to registered URLs with HMAC signature
- Retry logic (3 attempts with exponential backoff)

## Files to Create/Modify
- **[NEW]** ``Services/WebhookService.cs``
- **[NEW]** ``Models/WebhookModels.cs``
- **[MODIFY]** ``Services/ApiServer.cs`` — add webhook endpoints
- **[MODIFY]** ``Services/DatabaseService.cs`` — add Webhooks table

## Acceptance Criteria
- [ ] Register/unregister webhooks via API
- [ ] Webhooks fire on supported events
- [ ] HMAC signature verification
- [ ] Retry logic for failed deliveries
"@

gh issue create --repo $repo --title "Feature: Webhook System for External Automation" --body $body18 --label "feature,tier-3-advanced,WinUI"

# Issue 19: Multi-User Auth
$body19 = @"
## Summary
Add optional multi-user authentication to the REST API server — JWT-based auth with user accounts and API tokens.

## Motivation
Enables sharing a KaiROS instance in enterprise or family settings. Protects the API from unauthorized access.

## Detailed Requirements

### Auth Service: ``AuthService.cs``
- User registration with username/password (hashed with bcrypt)
- JWT token generation and validation
- API token support (long-lived tokens for programmatic access)
- Role-based access: admin, user

### API Changes (``ApiServer.cs``)
- ``POST /api/auth/login`` — returns JWT
- ``POST /api/auth/register`` — create account (admin only after first user)
- ``POST /api/auth/token`` — generate API token
- All existing endpoints require Bearer token when auth is enabled

### Settings
- Toggle: Enable/Disable authentication
- Admin panel: manage users, revoke tokens
- First-run setup: create admin account

## Files to Create/Modify
- **[NEW]** ``Services/AuthService.cs`` + ``IAuthService.cs``
- **[NEW]** ``Models/UserAccount.cs``
- **[MODIFY]** ``Services/ApiServer.cs`` — add auth middleware + endpoints
- **[MODIFY]** ``Services/DatabaseService.cs`` — add Users/Tokens tables
- **[MODIFY]** ``Views/SettingsView.xaml`` — auth settings

## Acceptance Criteria
- [ ] Optional auth toggle in Settings
- [ ] User registration and login
- [ ] JWT and API token support
- [ ] Protected endpoints when auth is enabled
- [ ] Admin can manage users
"@

gh issue create --repo $repo --title "Feature: Multi-User Authentication for REST API" --body $body19 --label "feature,tier-3-advanced,WinUI"

# Issue 20: Email Integration
$body20 = @"
## Summary
Add email integration — connect IMAP/SMTP accounts for AI-powered email summarization, drafting, and smart replies.

## Motivation
Email is a massive productivity pain point. AI-assisted email handling is a killer feature.

## Detailed Requirements

### Service: ``EmailService.cs``
- IMAP client for reading emails (use ``MailKit`` NuGet)
- SMTP client for sending emails
- Email parsing: extract subject, sender, body, attachments
- AI operations: summarize inbox, draft replies, extract action items

### UI: ``EmailView.xaml``
- Inbox view with email list
- Email detail view
- AI actions per email: "Summarize", "Draft Reply", "Extract Action Items"
- Compose view with AI-assisted writing

### Settings
- Email account configuration (IMAP/SMTP server, port, credentials)
- Sync settings (frequency, folders)

### NuGet Dependencies
- ``MailKit`` — robust .NET IMAP/SMTP client
- ``MimeKit`` — MIME message parsing

## Files to Create/Modify
- **[NEW]** ``Services/EmailService.cs`` + ``IEmailService.cs``
- **[NEW]** ``Models/EmailModels.cs``
- **[NEW]** ``Views/EmailView.xaml`` + ``EmailView.xaml.cs``
- **[NEW]** ``ViewModels/EmailViewModel.cs``
- **[MODIFY]** ``MainWindow.xaml`` — add Email nav item
- **[MODIFY]** ``KaiROS.AI.WinUI.csproj`` — add MailKit NuGet
- **[MODIFY]** ``Views/SettingsView.xaml`` — email account config

## Acceptance Criteria
- [ ] Connect to IMAP email account
- [ ] Display inbox with email list
- [ ] AI summarizes individual emails or entire inbox
- [ ] AI drafts reply suggestions
- [ ] Send emails via SMTP
- [ ] Email account configured in Settings
"@

gh issue create --repo $repo --title "Feature: Email Integration with AI-Powered Inbox" --body $body20 --label "feature,tier-3-advanced,WinUI"

Write-Host "All 20 issues created successfully!"
