## Summary
Add email integration - connect IMAP/SMTP accounts for AI-powered email summarization, drafting, and smart replies directly within KaiROS.

## Motivation
Email is a massive productivity pain point. AI-assisted email handling (summarize inbox, draft replies, extract action items) is a killer feature for a personal AI assistant.

## Detailed Requirements

### Service: `EmailService.cs`
```csharp
public interface IEmailService
{
    bool IsConfigured { get; }
    Task ConnectAsync();
    Task DisconnectAsync();
    Task<List<EmailMessage>> GetInboxAsync(int count = 50, int offset = 0);
    Task<EmailMessage> GetMessageAsync(string messageId);
    Task<List<string>> GetFoldersAsync();
    Task SendAsync(EmailMessage message);
    Task<string> SummarizeInboxAsync(int count = 10);  // AI-powered
}

public class EmailMessage
{
    public string Id { get; set; }
    public string Subject { get; set; }
    public string From { get; set; }
    public List<string> To { get; set; }
    public List<string> Cc { get; set; }
    public DateTime Date { get; set; }
    public string BodyText { get; set; }
    public string BodyHtml { get; set; }
    public bool IsRead { get; set; }
    public bool HasAttachments { get; set; }
    public string Folder { get; set; }
}

public class EmailAccountConfig
{
    public string DisplayName { get; set; }
    public string EmailAddress { get; set; }
    public string ImapServer { get; set; }
    public int ImapPort { get; set; } = 993;
    public string SmtpServer { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string Username { get; set; }
    public string Password { get; set; }  // Stored securely
    public bool UseSsl { get; set; } = true;
}
```

- Use `MailKit` NuGet for IMAP/SMTP (industry standard for .NET)
- `MimeKit` for MIME message parsing
- Credentials stored securely using Windows Credential Store (`Windows.Security.Credentials.PasswordVault`)

### UI: `EmailView.xaml`
- **Inbox list** (left panel): Subject, sender, date, read/unread indicator
- **Email detail** (right panel): Full email content rendered
- **AI toolbar** per email: "Summarize", "Draft Reply", "Extract Action Items", "Translate"
- **Compose view**: New email form with AI-assisted writing ("Help me write...", "Make more formal", "Shorten")
- **Folder navigation**: Inbox, Sent, Drafts, custom folders

### Navigation
Add "Email" item to `MainWindow.xaml` NavigationView (Tag="6", Icon: `&#xE715;`).

### AI Integration
- **Inbox summary**: "Summarize my last 10 emails" - AI reads subjects and bodies, produces digest
- **Smart reply**: AI suggests 3 reply options (brief, detailed, decline)
- **Email drafting**: Natural language -> formatted email ("Write a polite follow-up email to John about the project deadline")
- **Action item extraction**: Extract to-dos from email content

### NuGet Dependencies
- `MailKit` - robust .NET IMAP/SMTP client
- `MimeKit` - MIME message parsing (dependency of MailKit)

### Settings (`SettingsView.xaml`)
- "Email" section:
  - IMAP server, port, SSL toggle
  - SMTP server, port, SSL toggle
  - Username and password (stored in PasswordVault)
  - "Test Connection" button
  - Sync settings: check frequency, max emails to fetch

## Files to Create/Modify
- **[NEW]** `Services/EmailService.cs` + `IEmailService.cs`
- **[NEW]** `Models/EmailModels.cs` (EmailMessage, EmailAccountConfig)
- **[NEW]** `Views/EmailView.xaml` + `EmailView.xaml.cs`
- **[NEW]** `ViewModels/EmailViewModel.cs`
- **[MODIFY]** `MainWindow.xaml` - add Email nav item
- **[MODIFY]** `MainWindow.xaml.cs` - handle Email navigation
- **[MODIFY]** `KaiROS.AI.WinUI.csproj` - add MailKit + MimeKit NuGet
- **[MODIFY]** `Views/SettingsView.xaml` - email account configuration

## Acceptance Criteria
- [ ] Connect to IMAP email account
- [ ] Display inbox with email list (subject, sender, date)
- [ ] View individual email content
- [ ] AI summarizes individual emails
- [ ] AI summarizes inbox digest
- [ ] AI drafts reply suggestions
- [ ] Compose and send emails via SMTP
- [ ] Email account configured in Settings with test connection
- [ ] Credentials stored securely (PasswordVault)
