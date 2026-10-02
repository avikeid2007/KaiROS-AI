## Summary
Add a Deep Research mode that performs multi-step web research - generating search queries, reading multiple sources, and synthesizing findings into a structured report with citations.

## Motivation
The existing web search feature returns snippets from a single query. Deep Research performs iterative, multi-step research like a human researcher - significantly more valuable for knowledge work.

## Detailed Requirements

### New Service: `DeepResearchService.cs`
```csharp
public interface IDeepResearchService
{
    IAsyncEnumerable<ResearchProgress> RunResearchAsync(string query, ResearchOptions options, CancellationToken ct);
}

public class ResearchProgress
{
    public ResearchPhase Phase { get; set; }
    public string StatusMessage { get; set; }
    public int CurrentStep { get; set; }
    public int TotalSteps { get; set; }
    public List<ResearchSource> SourcesFound { get; set; }
    public string PartialReport { get; set; }
}

public enum ResearchPhase { PlanningQueries, Searching, ReadingSources, Analyzing, WritingReport, Complete }

public class ResearchSource
{
    public string Title { get; set; }
    public string Url { get; set; }
    public string Snippet { get; set; }
    public string FullContent { get; set; }
    public bool IsRead { get; set; }
}
```

### Research Pipeline
1. **Query Planning**: Send user question to LLM: "Generate 3-5 specific search queries to thoroughly research: {question}"
2. **Web Search**: Use existing `IWebSearchService` to search each query
3. **Source Reading**: Fetch full page content for top 3-5 unique URLs using `HttpClient` + HTML-to-text extraction (use `HtmlAgilityPack` NuGet)
4. **Analysis**: Send all source content to LLM: "Based on these sources, identify key findings, contradictions, and gaps"
5. **Report Generation**: Generate final structured report with: Executive Summary, Key Findings, Detailed Analysis, Sources (with inline citations)
6. **Yield progress** at each phase for real-time UI updates

### UI: Research Mode in ChatView
Add a "Deep Research" toggle/button in the chat toolbar. When active:
- Show a multi-step progress panel with phases and status
- Display sources being read (URL list with checkmarks)
- Stream the final report in the chat area
- Report includes clickable citation links

### NuGet Dependencies
Add `HtmlAgilityPack` to `KaiROS.AI.WinUI.csproj` for HTML parsing.

## Files to Create/Modify
- **[NEW]** `Services/DeepResearchService.cs` + `IDeepResearchService.cs`
- **[NEW]** `Models/ResearchModels.cs` (ResearchProgress, ResearchSource, ResearchOptions)
- **[MODIFY]** `Views/ChatView.xaml` - add research mode toggle + progress UI
- **[MODIFY]** `ViewModels/ChatViewModel.cs` - add research command + progress binding
- **[MODIFY]** `KaiROS.AI.WinUI.csproj` - add HtmlAgilityPack NuGet

## Acceptance Criteria
- [ ] User can trigger Deep Research from chat with a toggle/button
- [ ] Research runs through all 5 phases with real-time progress
- [ ] Sources are displayed with titles and URLs
- [ ] Final report is well-structured with citations
- [ ] Report is streamable (token-by-token display)
- [ ] User can cancel mid-research
- [ ] Research conversations are saved to chat history
