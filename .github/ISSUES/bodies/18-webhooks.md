## Summary
Add webhook support to the existing REST API server - send notifications to external URLs when events occur in KaiROS, and allow external systems to trigger actions.

## Motivation
Connects KaiROS to external automation workflows (Zapier, IFTTT, n8n, Power Automate, custom scripts).

## Detailed Requirements

### Webhook Endpoints in `ApiServer.cs`
Add to the existing REST API:
- `POST /api/webhooks` - register a webhook (URL, events to subscribe, optional secret)
- `DELETE /api/webhooks/{id}` - remove a webhook
- `GET /api/webhooks` - list registered webhooks
- `POST /api/webhooks/{id}/test` - send a test payload

### Supported Events
- `chat.completed` - fires when AI generates a complete response
- `model.loaded` - fires when a model is loaded
- `model.unloaded` - fires when a model is unloaded
- `research.completed` - fires when deep research finishes (if that feature exists)
- `task.created` - fires when a task is extracted from chat (if that feature exists)

### Webhook Payload Format
```json
{
    "event": "chat.completed",
    "timestamp": "2026-06-26T09:00:00Z",
    "data": {
        "session_id": "abc-123",
        "model": "qwen-3.5-9b",
        "user_message": "What is recursion?",
        "assistant_message": "Recursion is...",
        "token_count": 150
    },
    "signature": "sha256=..."
}
```

### Implementation: `WebhookService.cs`
```csharp
public interface IWebhookService
{
    Task RegisterAsync(WebhookRegistration registration);
    Task UnregisterAsync(string id);
    Task<List<WebhookRegistration>> GetAllAsync();
    Task FireEventAsync(string eventName, object data);
}
```

- Store webhook registrations in SQLite
- On event trigger, POST JSON payload to registered URLs
- Include HMAC-SHA256 signature header for verification
- Retry logic: 3 attempts with exponential backoff (1s, 5s, 25s)
- Log delivery status (success/failure) for debugging

### Database (`DatabaseService.cs`)
Add `Webhooks` table: Id, Url, Events (comma-separated), Secret, CreatedAt, IsActive.

## Files to Create/Modify
- **[NEW]** `Services/WebhookService.cs` + `IWebhookService.cs`
- **[NEW]** `Models/WebhookModels.cs`
- **[MODIFY]** `Services/ApiServer.cs` - add webhook CRUD endpoints
- **[MODIFY]** `Services/DatabaseService.cs` - add Webhooks table
- **[MODIFY]** `ViewModels/ChatViewModel.cs` - fire chat.completed event

## Acceptance Criteria
- [ ] Register/unregister webhooks via REST API
- [ ] Webhooks fire on supported events with correct payload
- [ ] HMAC-SHA256 signature included in headers
- [ ] Retry logic for failed deliveries (3 attempts)
- [ ] Test endpoint sends a sample payload
