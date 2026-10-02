## Summary
Add AI image generation capability - generate images from text prompts using Stable Diffusion (local) or API-based providers.

## Motivation
Visual content creation is a popular AI use case. Integrating it keeps users in the KaiROS ecosystem for both text and image generation.

## Detailed Requirements

### Service: `ImageGenerationService.cs`
```csharp
public interface IImageGenerationService
{
    bool IsConfigured { get; }
    Task<GeneratedImage> GenerateAsync(ImageGenRequest request, CancellationToken ct);
    Task<List<string>> GetAvailableModelsAsync();
}

public class ImageGenRequest
{
    public string Prompt { get; set; }
    public string NegativePrompt { get; set; }
    public int Width { get; set; } = 512;
    public int Height { get; set; } = 512;
    public int Steps { get; set; } = 20;
    public string Model { get; set; }
}

public class GeneratedImage
{
    public string FilePath { get; set; }  // Saved to local gallery
    public byte[] ImageBytes { get; set; }
    public string Prompt { get; set; }
    public DateTime GeneratedAt { get; set; }
}
```

Support multiple backends:
1. **API-based**: OpenAI DALL-E API, Stability AI API (via REST)
2. **Local**: Stable Diffusion via API (user runs ComfyUI or Automatic1111 locally, KaiROS connects to their API)

### Chat Integration
- Detect image generation intent (user types "generate an image of..." or "/imagine ...")
- Display generated image inline in chat message bubble
- Save generated images to `%LocalAppData%/KaiROS.AI/gallery/`
- Right-click context menu: Save As, Copy, Regenerate

### Settings (`SettingsView.xaml`)
- "Image Generation" section:
  - Backend selector: None / OpenAI / Stability AI / Local (ComfyUI/A1111)
  - API URL field (for local backends)
  - API key field (for cloud backends, stored securely)
  - Default image size, steps, model

## Files to Create/Modify
- **[NEW]** `Services/ImageGenerationService.cs` + `IImageGenerationService.cs`
- **[NEW]** `Models/ImageGenModels.cs`
- **[MODIFY]** `ViewModels/ChatViewModel.cs` - detect image requests, display images
- **[MODIFY]** `Views/ChatView.xaml` - image display in chat bubbles
- **[MODIFY]** `Views/SettingsView.xaml` - image gen settings section
- **[MODIFY]** `ViewModels/SettingsViewModel.cs` - image gen settings

## Acceptance Criteria
- [ ] Generate images from text prompts via configured API
- [ ] Images displayed inline in chat message bubbles
- [ ] Support local Stable Diffusion API (ComfyUI/A1111 endpoints)
- [ ] Support at least one cloud API (OpenAI DALL-E or Stability AI)
- [ ] Images saveable to disk via right-click or save button
- [ ] Backend configurable in Settings
