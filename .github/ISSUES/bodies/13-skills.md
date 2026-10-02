## Summary
Add a Skills/Prompt Template system - pre-configured prompt templates with variables that users can create, share, and apply to chat sessions.

## Motivation
Power users can create reusable AI behaviors (e.g., "Code Reviewer", "Email Writer", "Meeting Summarizer") without understanding prompt engineering.

## Detailed Requirements

### Data Model: `Skill.cs`
```csharp
public class Skill
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; }
    public string Description { get; set; }
    public string Icon { get; set; }           // Segoe Fluent Icons glyph code
    public string SystemPrompt { get; set; }    // The actual prompt template
    public string Category { get; set; }        // "Writing", "Code", "Analysis", "Custom"
    public List<SkillVariable> Variables { get; set; } = new();
    public bool IsBuiltIn { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SkillVariable
{
    public string Name { get; set; }        // e.g., "language", "tone"
    public string Description { get; set; }
    public string DefaultValue { get; set; }
}
```

### Built-in Skills (ship with app)
1. **Code Reviewer**: "Review this code for bugs, performance issues, and best practices..."
2. **Email Writer**: "Write a professional email with the following tone: {{tone}}..."
3. **Meeting Summarizer**: "Summarize this meeting transcript into key decisions and action items..."
4. **Translator**: "Translate the following text to {{target_language}}..."
5. **ELI5**: "Explain the following concept as if I'm 5 years old..."
6. **Blog Post Writer**: "Write a blog post about {{topic}} in {{style}} style..."
7. **SQL Generator**: "Generate SQL queries for the following request..."
8. **Regex Helper**: "Create a regex pattern that matches {{pattern_description}}..."
9. **Grammar Fixer**: "Fix grammar and improve clarity of the following text..."
10. **Debate Partner**: "Take the opposing position and debate: {{topic}}..."

### Database (`DatabaseService.cs`)
Add `Skills` table and seed with built-in skills.

### UI
- **Skills panel**: Flyout or dropdown accessible from chat toolbar button
- **Skill cards**: Show icon, name, description for each skill
- **Variable input**: When a skill has variables (e.g., {{language}}), show input fields before applying
- **Skill editor dialog**: Create/edit custom skills with system prompt editor and variable definition
- **Category tabs**: Filter skills by category
- **Active skill indicator**: Show which skill is active in chat toolbar

### Integration
- Activating a skill sets the system prompt for the current chat session
- Deactivating restores the default system prompt
- Skills persist in database for custom ones

## Files to Create/Modify
- **[NEW]** `Models/Skill.cs`
- **[NEW]** `Services/SkillService.cs` + `ISkillService.cs`
- **[NEW]** `Views/SkillsPanel.xaml` + `SkillsPanel.xaml.cs` (flyout or UserControl)
- **[MODIFY]** `Services/DatabaseService.cs` - add Skills table + seed data
- **[MODIFY]** `Views/ChatView.xaml` - add skills button in toolbar
- **[MODIFY]** `ViewModels/ChatViewModel.cs` - skill activation logic

## Acceptance Criteria
- [ ] 10+ built-in skills shipped with the app
- [ ] Users can create custom skills with system prompts and variables
- [ ] Skills apply system prompt to current chat context
- [ ] Skills accessible from chat toolbar flyout
- [ ] Custom skills persist in SQLite database
- [ ] Variable substitution works (fill in {{language}}, {{tone}} etc.)
