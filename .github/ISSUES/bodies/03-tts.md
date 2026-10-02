## Summary
Add Text-to-Speech (TTS) so the AI can read its responses aloud. Use Windows built-in speech synthesis for zero-dependency implementation.

## Motivation
TTS completes the voice conversation experience, enables accessibility for visually impaired users, and allows hands-free consumption of AI responses.

## Detailed Requirements

### New Service: `TextToSpeechService.cs`
```csharp
public interface ITextToSpeechService
{
    bool IsSpeaking { get; }
    bool IsAvailable { get; }
    List<VoiceInfo> GetAvailableVoices();
    Task SpeakAsync(string text, CancellationToken ct = default);
    Task StopAsync();
    Task SetVoiceAsync(string voiceId);
    Task SetRateAsync(double rate);   // 0.5 to 2.0
    Task SetVolumeAsync(double volume); // 0.0 to 1.0
}
```

Implementation:
- Use `Windows.Media.SpeechSynthesis.SpeechSynthesizer` - built into Windows, no downloads needed
- Stream audio via `MediaPlayer` for smooth playback
- Parse markdown from AI responses to extract plain text before speaking (strip code blocks, links, etc.)

### UI Changes
- **Per-message "Read Aloud" button** on each AI response bubble in `ChatView.xaml`
- **Global TTS toggle** in the chat toolbar - when enabled, auto-reads each new AI response
- **Speaking indicator**: Show a sound wave animation on the message being read
- **Stop button**: Replace play with stop while speaking

### Settings (`SettingsView.xaml`)
- "Voice Output" section:
  - Toggle: Enable TTS
  - Dropdown: Voice selection (list system voices)
  - Slider: Speech rate (0.5x - 2.0x)
  - Slider: Volume
  - Toggle: Auto-read new responses

### ChatView.xaml Message Template Changes
Add to the AI message DataTemplate:
```xml
<Button Command="{Binding ReadAloudCommand}" CommandParameter="{Binding Content}"
        Style="{StaticResource IconButtonStyle}" ToolTipService.ToolTip="Read aloud">
    <FontIcon Glyph="&#xE767;" FontSize="14"/>
</Button>
```

## Files to Create/Modify
- **[NEW]** `Services/TextToSpeechService.cs` + `ITextToSpeechService.cs`
- **[MODIFY]** `Views/ChatView.xaml` - add Read Aloud buttons per message + global toggle
- **[MODIFY]** `ViewModels/ChatViewModel.cs` - add TTS commands
- **[MODIFY]** `Views/SettingsView.xaml` - add TTS settings
- **[MODIFY]** `ViewModels/SettingsViewModel.cs` - add TTS settings

## Acceptance Criteria
- [ ] Read Aloud button on each AI message
- [ ] Windows voices listed and selectable in Settings
- [ ] Rate and volume adjustable
- [ ] Auto-read mode works for streaming responses
- [ ] Stop button cancels speech mid-read
- [ ] Markdown is stripped before speaking (no reading headings or code fence syntax)
- [ ] Works immediately with no downloads (uses Windows built-in voices)
