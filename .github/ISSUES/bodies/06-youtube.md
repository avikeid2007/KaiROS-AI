## Summary
Add YouTube video summarization - paste a YouTube URL and get an AI-generated summary of the video transcript.

## Motivation
One of the most requested features in AI tools. Low effort to implement but provides high user value.

## Detailed Requirements

### New Service: `YouTubeService.cs`
```csharp
public interface IYouTubeService
{
    Task<VideoInfo> GetVideoInfoAsync(string url);
    Task<string> GetTranscriptAsync(string videoId);
    bool IsYouTubeUrl(string text);
}

public class VideoInfo
{
    public string VideoId { get; set; }
    public string Title { get; set; }
    public string ThumbnailUrl { get; set; }
    public string Duration { get; set; }
    public string ChannelName { get; set; }
}
```

- Extract video ID from URL (support youtube.com/watch?v= and youtu.be/ formats)
- Fetch transcript using YouTube's timedtext API (no API key needed for auto-generated captions)
- Fallback: Use yt-dlp CLI if available for more reliable transcript extraction
- Clean up transcript (remove timestamps, merge segments)

### Chat Integration
- Detect YouTube URLs pasted in chat input (in `ChatViewModel.cs`)
- Show a preview card: video title, thumbnail, duration
- Auto-suggest: "Would you like me to summarize this video?"
- Send transcript as context to LLM with summarization prompt
- Display summary as structured response with sections: Overview, Key Points, Main Arguments, Conclusion

### UI
- YouTube URL detection with link preview card in chat
- Summary displayed as structured AI response
- Option to view raw transcript

## Files to Create/Modify
- **[NEW]** `Services/YouTubeService.cs` + `IYouTubeService.cs`
- **[NEW]** `Models/VideoInfo.cs`
- **[MODIFY]** `ViewModels/ChatViewModel.cs` - URL detection + summary command
- **[MODIFY]** `Views/ChatView.xaml` - URL preview card (optional)

## Acceptance Criteria
- [ ] Paste YouTube URL and transcript is fetched automatically
- [ ] AI generates structured summary with key points
- [ ] Works with auto-generated captions
- [ ] Handles videos without captions gracefully (shows error message)
- [ ] Support both youtube.com and youtu.be URL formats
