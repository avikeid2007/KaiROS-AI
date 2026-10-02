## Summary
Add persistent AI memory that allows KaiROS to remember user preferences, facts, and context across chat sessions. The AI should automatically extract and store key facts from conversations and inject relevant memories into future chats.

## Motivation
Currently each chat session starts fresh with no memory of past interactions. Users must re-explain preferences and context every time. Persistent memory makes the AI feel personal and intelligent over time.

## Detailed Requirements

### Data Model
Create a new `MemoryEntry` model in `KaiROS.AI.WinUI/Models/`:
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

### Database Changes (`DatabaseService.cs`)
Add a new `Memories` table to the SQLite database in `InitializeAsync()`:
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
Add CRUD methods to `IDatabaseService`:
- `Task<List<MemoryEntry>> GetMemoriesAsync()`
- `Task AddMemoryAsync(MemoryEntry memory)`
- `Task UpdateMemoryAsync(MemoryEntry memory)`
- `Task DeleteMemoryAsync(int id)`
- `Task<List<MemoryEntry>> SearchMemoriesAsync(string query)`

### New Service: `MemoryService.cs` in `Services/`
- **Auto-extraction**: After each AI response, send a follow-up prompt to the model asking it to extract any user facts/preferences from the conversation (name, preferences, instructions, facts about projects, etc.)
- **Memory injection**: Before generating responses, query relevant memories and prepend them to the system prompt as `[Memory] User's name is Avnish. User prefers C#.`
- **Deduplication**: Check for existing similar memories before adding new ones
- **Relevance scoring**: Prioritize frequently used and recently used memories

### UI Changes
- **Settings tab** (`SettingsView.xaml`): Add a "Memory" section with:
  - Toggle to enable/disable AI memory
  - Button to "View & Manage Memories" that opens a dialog/page listing all memories
  - Button to "Clear All Memories"
- **Chat input area** (`ChatView.xaml`): Small indicator icon showing memory is active
- **Memory management dialog**: List view showing all memories with edit/delete per entry

### Integration with `ChatViewModel.cs`
- Modify `SendMessageAsync()` to:
  1. Query `MemoryService` for relevant memories before building the prompt
  2. After receiving the AI response, call `MemoryService.ExtractMemoriesAsync()` in background
  3. Pass memory context via the existing `sessionContext` parameter in `IChatService.GenerateResponseStreamAsync()`

## Files to Create/Modify
- **[NEW]** `Models/MemoryEntry.cs`
- **[NEW]** `Services/MemoryService.cs` + `IMemoryService.cs`
- **[MODIFY]** `Services/DatabaseService.cs` - add Memories table + CRUD
- **[MODIFY]** `ViewModels/ChatViewModel.cs` - integrate memory injection/extraction
- **[MODIFY]** `ViewModels/SettingsViewModel.cs` - add memory settings
- **[MODIFY]** `Views/SettingsView.xaml` - add Memory section UI
- **[MODIFY]** `Views/ChatView.xaml` - add memory indicator

## Acceptance Criteria
- [ ] Memories persist across app restarts (stored in SQLite)
- [ ] AI automatically extracts facts from conversations
- [ ] Relevant memories are injected into prompts
- [ ] Users can view, edit, and delete individual memories
- [ ] Memory can be toggled on/off in Settings
- [ ] No noticeable performance impact on chat speed
