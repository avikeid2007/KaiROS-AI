## Summary
Add a task/to-do management system. AI can extract action items from conversations and users can manage tasks within the app.

## Motivation
Keeps users inside KaiROS for daily productivity. AI-powered task extraction from chat is a unique differentiator.

## Detailed Requirements

### Data Model: `TaskItem.cs`
```csharp
public class TaskItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; }
    public string Description { get; set; }
    public TaskPriority Priority { get; set; }  // Low, Medium, High, Critical
    public TaskItemStatus Status { get; set; }  // Todo, InProgress, Done
    public DateTime CreatedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string SourceSessionId { get; set; }  // Chat session that created it
}

public enum TaskPriority { Low, Medium, High, Critical }
public enum TaskItemStatus { Todo, InProgress, Done }
```

### Database (`DatabaseService.cs`)
Add `Tasks` table with CRUD methods.

### AI Integration
- "Extract Tasks" button in chat toolbar sends conversation to LLM with prompt: "Extract all action items, to-dos, and tasks from this conversation. Return as JSON array."
- Parse LLM response and create `TaskItem` entries
- Auto-suggest: After lengthy AI response, offer to extract tasks
- Natural language creation: "Add a task to review the code by Friday" detected in chat input

### UI: `TasksView.xaml`
- Task list with filters (All, Active, Completed, Overdue)
- Inline editing of title, priority, due date
- Priority badges (color-coded: Low=gray, Medium=blue, High=orange, Critical=red)
- Checkbox to mark complete
- "Extract from Chat" button opens session picker
- Sort by: date created, due date, priority

### Navigation
Add "Tasks" to `MainWindow.xaml` NavigationView (Tag="5", Icon: `&#xE73A;`).

## Files to Create/Modify
- **[NEW]** `Models/TaskItem.cs`
- **[NEW]** `Views/TasksView.xaml` + `TasksView.xaml.cs`
- **[NEW]** `ViewModels/TasksViewModel.cs`
- **[NEW]** `Services/TaskService.cs` + `ITaskService.cs`
- **[MODIFY]** `Services/DatabaseService.cs` - add Tasks table
- **[MODIFY]** `MainWindow.xaml` - add Tasks nav item
- **[MODIFY]** `MainWindow.xaml.cs` - handle Tasks navigation
- **[MODIFY]** `ViewModels/ChatViewModel.cs` - add "Extract Tasks" command

## Acceptance Criteria
- [ ] Create, edit, complete, delete tasks
- [ ] AI extracts action items from chat conversations
- [ ] Tasks have priority, status, and optional due dates
- [ ] Filter by status (active/completed/overdue)
- [ ] Tasks persist in SQLite
- [ ] Color-coded priority badges
