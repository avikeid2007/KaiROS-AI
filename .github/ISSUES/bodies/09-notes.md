## Summary
Add a built-in Notes system with a markdown editor. Users can create, organize, and AI-assist their notes.

## Motivation
Makes KaiROS a productivity hub. Notes can be used as context for AI conversations and vice versa.

## Detailed Requirements

### Data Model: `Note.cs`
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

### Database (`DatabaseService.cs`)
Add `Notes` table:
```sql
CREATE TABLE IF NOT EXISTS Notes (
    Id TEXT PRIMARY KEY,
    Title TEXT NOT NULL,
    Content TEXT,
    Category TEXT DEFAULT 'General',
    CreatedAt TEXT NOT NULL,
    ModifiedAt TEXT NOT NULL,
    IsPinned INTEGER DEFAULT 0
);
```
Add CRUD methods to `IDatabaseService`.

### UI: `NotesView.xaml`
- **Left panel**: note list (searchable, sortable by date/title, pin support)
- **Right panel**: markdown editor with live preview (reuse existing `MarkdownParser.cs`)
- **Toolbar**: New, Delete, Pin, "Ask AI about this note", Export to file
- **AI actions flyout**: "Summarize", "Expand", "Rewrite", "Generate action items", "Fix grammar"
- **Search bar**: Filter notes by title and content

### Navigation
Add "Notes" item to `MainWindow.xaml` NavigationView MenuItems (Tag="4", Icon: `&#xE70B;`).

### AI Integration
- "Ask AI" sends note content as context to the loaded model
- AI response can be appended to the note or shown in a side panel
- "Generate from Chat" - create a note from a chat conversation summary

## Files to Create/Modify
- **[NEW]** `Models/Note.cs`
- **[NEW]** `Views/NotesView.xaml` + `NotesView.xaml.cs`
- **[NEW]** `ViewModels/NotesViewModel.cs`
- **[NEW]** `Services/NotesService.cs` + `INotesService.cs`
- **[MODIFY]** `Services/DatabaseService.cs` - add Notes table + CRUD
- **[MODIFY]** `MainWindow.xaml` - add Notes nav item
- **[MODIFY]** `MainWindow.xaml.cs` - handle Notes navigation

## Acceptance Criteria
- [ ] Create, edit, delete notes with markdown support
- [ ] Notes persist in SQLite database
- [ ] Search notes by title/content
- [ ] Pin important notes to top
- [ ] AI can summarize/expand/rewrite notes
- [ ] Notes accessible from navigation sidebar
