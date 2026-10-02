## Summary
Add sandboxed shell/terminal access - the AI can execute PowerShell commands on the user's machine with explicit user approval for each command.

## Motivation
Developer productivity feature. Users can ask KaiROS to check disk space, list running processes, run build commands, or inspect files without leaving the app.

## Detailed Requirements

### Service: `ShellService.cs`
```csharp
public interface IShellService
{
    bool IsEnabled { get; }
    Task<ShellResult> ExecuteAsync(string command, string workingDirectory = null, int timeoutMs = 30000, CancellationToken ct = default);
}

public class ShellResult
{
    public string Command { get; set; }
    public string StandardOutput { get; set; }
    public string StandardError { get; set; }
    public int ExitCode { get; set; }
    public TimeSpan Duration { get; set; }
    public bool WasApproved { get; set; }
}
```

- Execute PowerShell commands via `System.Diagnostics.Process`
- Capture stdout, stderr, exit code
- Configurable timeout (default 30s)
- Output size limit (truncate outputs > 10KB with "[truncated]" indicator)

### Security (Critical)
- **All commands require explicit user approval** via a confirmation ContentDialog
- Dialog shows: command text, working directory, and "Allow" / "Deny" buttons
- Optional: "Always allow" for specific command prefixes (configurable in Settings)
- Configurable allowed working directories
- Block dangerous commands by default (format, del /, rm -rf, etc.)

### Chat Integration
- User types `/run <command>` or AI in Agent Mode requests shell execution
- Command output displayed in chat with monospace formatting (use existing code block rendering)
- Error output shown in red/warning color

### Settings (`SettingsView.xaml`)
- "Shell Access" section:
  - Toggle: Enable/Disable shell access (disabled by default)
  - Default working directory path
  - Timeout setting (seconds)
  - Blocked command patterns list

## Files to Create/Modify
- **[NEW]** `Services/ShellService.cs` + `IShellService.cs`
- **[NEW]** `Models/ShellResult.cs`
- **[MODIFY]** `ViewModels/ChatViewModel.cs` - /run command handling + approval dialog
- **[MODIFY]** `Views/ChatView.xaml` - command output display styling
- **[MODIFY]** `Views/SettingsView.xaml` - shell access settings

## Acceptance Criteria
- [ ] Execute PowerShell commands from chat via /run prefix
- [ ] All commands require explicit user approval dialog
- [ ] Output displayed in monospace/code block format
- [ ] Timeout and output size limits enforced
- [ ] Enable/disable toggle in Settings (disabled by default)
- [ ] Dangerous commands blocked by default
