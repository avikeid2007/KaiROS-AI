## Summary
Add backup and restore functionality - export all app data (chat history, settings, custom models config, RaaS configs, memories) as a ZIP file and import on any device.

## Motivation
Data portability, disaster recovery, and migration between devices. Essential for user trust.

## Detailed Requirements

### Backup Service: `BackupService.cs`
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
}

public class BackupInfo
{
    public DateTime CreatedAt { get; set; }
    public string AppVersion { get; set; }
    public string MachineName { get; set; }
    public long FileSizeBytes { get; set; }
    public int ChatSessionCount { get; set; }
    public int MemoryCount { get; set; }
}
```

### Backup Format
ZIP file containing:
- `kairos.db` - SQLite database copy
- `settings.json` - app settings export
- `metadata.json` - backup timestamp, app version, machine info
- `chat_history/` - exported sessions as JSON (optional, for readability)

Note: Do NOT include downloaded model files (too large, can be re-downloaded).

### UI in Settings (`SettingsView.xaml`)
- "Backup & Restore" section:
  - "Create Backup" button -> file save dialog (FileSavePicker) -> creates ZIP
  - "Restore Backup" button -> file open dialog (FileOpenPicker) -> confirmation dialog -> restore
  - Checkboxes for what to include in backup
  - Display last backup date
  - Restore shows a confirmation: "This will replace all current data. Continue?"

## Files to Create/Modify
- **[NEW]** `Services/BackupService.cs` + `IBackupService.cs`
- **[NEW]** `Models/BackupModels.cs`
- **[MODIFY]** `Views/SettingsView.xaml` - add Backup & Restore section
- **[MODIFY]** `ViewModels/SettingsViewModel.cs` - add backup/restore commands

## Acceptance Criteria
- [ ] Backup creates a ZIP with all selected data
- [ ] Restore replaces current data with backup data
- [ ] Confirmation dialog before restore (warns about data replacement)
- [ ] Backup file includes metadata (version, date, machine name)
- [ ] Model binary files are NOT included (too large)
- [ ] Backup file can be created via FileSavePicker
