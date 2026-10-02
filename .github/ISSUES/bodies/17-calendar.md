## Summary
Add calendar integration - view, create, and manage events via Google Calendar API or CalDAV protocol. AI can query your schedule and create events via natural language.

## Motivation
A personal AI assistant that knows your schedule is significantly more useful. "What do I have today?" and "Schedule a meeting tomorrow at 3 PM" are natural AI interactions.

## Detailed Requirements

### Service: `CalendarService.cs`
```csharp
public interface ICalendarService
{
    bool IsConfigured { get; }
    Task<List<CalendarEvent>> GetEventsAsync(DateTime start, DateTime end);
    Task<CalendarEvent> CreateEventAsync(CalendarEvent evt);
    Task UpdateEventAsync(CalendarEvent evt);
    Task DeleteEventAsync(string eventId);
    Task<string> GetTodaySummaryAsync();  // For AI context injection
}

public class CalendarEvent
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string Location { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public bool IsAllDay { get; set; }
    public string CalendarName { get; set; }
}
```

Support:
1. **Google Calendar API** (OAuth2 authentication flow)
2. **CalDAV protocol** (for self-hosted calendars like Radicale, Nextcloud)

### Chat Integration
- AI can answer: "What's on my schedule today/this week?"
- AI can create events: "Schedule a meeting with John tomorrow at 3 PM for 1 hour"
- Today's schedule summary optionally injected into system prompt

### UI Options
Option A: Dedicated `CalendarView.xaml` with month/week/day view
Option B: Calendar widget in Settings or sidebar showing today's events

**Recommended: Start with Option B** (simpler) and expand to Option A later.

### Settings (`SettingsView.xaml`)
- "Calendar" section:
  - Calendar provider selector: None / Google Calendar / CalDAV
  - Google OAuth flow ("Sign in with Google" button)
  - CalDAV URL, username, password fields
  - Sync frequency (every 5/15/30/60 minutes)
  - Toggle: Include today's events in AI context

### NuGet
- `Google.Apis.Calendar.v3` for Google Calendar
- Or use raw HTTP/CalDAV for self-hosted

## Files to Create/Modify
- **[NEW]** `Services/CalendarService.cs` + `ICalendarService.cs`
- **[NEW]** `Models/CalendarEvent.cs`
- **[NEW]** `Views/CalendarView.xaml` + `CalendarView.xaml.cs` (optional, can start with Settings widget)
- **[MODIFY]** `Views/SettingsView.xaml` - calendar account configuration
- **[MODIFY]** `ViewModels/SettingsViewModel.cs` - calendar settings
- **[MODIFY]** `ViewModels/ChatViewModel.cs` - calendar query handling

## Acceptance Criteria
- [ ] Connect to at least one calendar provider (Google or CalDAV)
- [ ] View today's and upcoming events
- [ ] AI can query calendar ("what's on my schedule?")
- [ ] AI can create events from natural language
- [ ] Calendar account configurable in Settings
