## Summary
Add Speech-to-Text (STT) voice input capability so users can speak to the AI instead of typing. Use local Whisper-based transcription for privacy.

## Motivation
Voice input is essential for accessibility, hands-free usage, and a modern conversational AI experience. Many users prefer speaking over typing for longer prompts.

## Detailed Requirements

### NuGet Dependencies
Add to `KaiROS.AI.WinUI.csproj`:
- `Whisper.net` (C# bindings for whisper.cpp) OR use Windows built-in `Windows.Media.SpeechRecognition`
- `NAudio` for audio capture

### New Service: `SpeechToTextService.cs`
```csharp
public interface ISpeechToTextService
{
    bool IsListening { get; }
    bool IsAvailable { get; }
    event EventHandler<string> TranscriptionCompleted;
    event EventHandler<string> PartialTranscription;
    event EventHandler<float> AudioLevelChanged;
    Task StartListeningAsync(CancellationToken ct = default);
    Task StopListeningAsync();
    Task<string> TranscribeFileAsync(string audioFilePath);
}
```

Implementation approach:
1. **Primary**: Use `Whisper.net` with a small model (`ggml-base.en.bin`, ~142MB) for local transcription
2. **Fallback**: Use Windows `SpeechRecognizer` API (`Windows.Media.SpeechRecognition`) which requires no model download
3. **Audio capture**: Use `NAudio` or `Windows.Media.Capture.MediaCapture` to record from microphone

### Model Management
- Auto-download Whisper model on first use (similar to how LLM models are downloaded)
- Store in `%LocalAppData%/KaiROS.AI/whisper-models/`
- Settings option to choose model size: tiny (75MB), base (142MB), small (466MB)

### UI Changes in `ChatView.xaml`
- Add a **microphone button** next to the Send button in the chat input area
- **States**:
  - Idle: Gray microphone icon
  - Listening: Pulsing red microphone with audio level visualization
  - Transcribing: Spinning indicator
- **Behavior**:
  - Click to start recording, click again (or press Enter/Escape) to stop
  - Real-time partial transcription shown in the input TextBox as user speaks
  - Final transcription fills the TextBox; user can edit before sending
  - Optional: Auto-send after silence detection (configurable in Settings)

### Settings (`SettingsView.xaml`)
- "Voice Input" section:
  - Toggle: Enable/Disable voice input
  - Dropdown: Whisper model size (tiny/base/small)
  - Dropdown: Audio input device selection
  - Toggle: Auto-send after silence (with configurable silence duration slider)
  - Button: Download/Update Whisper model

### Integration with `ChatViewModel.cs`
- Add `StartVoiceInputCommand` and `StopVoiceInputCommand`
- Add `IsListening` observable property
- Wire transcription result to `UserInput` property
- Handle microphone permissions via `Package.appxmanifest` capability

### Package Manifest
Add to `Package.appxmanifest`:
```xml
<DeviceCapability Name="microphone"/>
```

## Files to Create/Modify
- **[NEW]** `Services/SpeechToTextService.cs` + `ISpeechToTextService.cs`
- **[MODIFY]** `Views/ChatView.xaml` - add mic button + recording UI
- **[MODIFY]** `ViewModels/ChatViewModel.cs` - add voice input commands
- **[MODIFY]** `Views/SettingsView.xaml` - add voice settings section
- **[MODIFY]** `ViewModels/SettingsViewModel.cs` - add voice settings
- **[MODIFY]** `Package.appxmanifest` - add microphone capability
- **[MODIFY]** `KaiROS.AI.WinUI.csproj` - add NuGet packages

## Acceptance Criteria
- [ ] Microphone button visible in chat input area
- [ ] Click starts recording with visual feedback (pulsing, audio level)
- [ ] Transcription appears in input TextBox
- [ ] Works offline using local Whisper model
- [ ] Whisper model downloads automatically on first use
- [ ] Settings allow model size selection and input device choice
- [ ] Graceful error handling if no microphone is available
