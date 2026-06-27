using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KaiROS.AI.WinUI.Models;

namespace KaiROS.AI.WinUI.Services;

public class DeepResearchService : IDeepResearchService
{
    private readonly IChatService _chatService;
    private readonly IWebSearchService _webSearchService;

    // Use a large context window for all deep research inference phases
    private const uint DeepResearchContextSize = 32768;
    // Max chars per source page to avoid overflowing context with raw HTML
    private const int MaxCharsPerSource = 3000;

    public DeepResearchService(IChatService chatService, IWebSearchService webSearchService)
    {
        _chatService = chatService;
        _webSearchService = webSearchService;
    }

    public async IAsyncEnumerable<ResearchProgress> RunResearchAsync(
        string query, 
        ResearchOptions options, 
        [EnumeratorCancellation] CancellationToken ct)
    {
        var progress = new ResearchProgress
        {
            Phase = ResearchPhase.PlanningQueries,
            StatusMessage = "Planning search queries...",
            CurrentStep = 0,
            TotalSteps = 5,
            SourcesFound = []
        };
        yield return progress;

        // 1. Planning Queries
        _chatService.ClearContext(DeepResearchContextSize);
        var planningPrompt = $"Generate 3-5 specific, distinct search queries to thoroughly research: \"{query}\". Output ONLY the queries, one per line, with no other text, numbers, or bullet points.";
        var planningResponse = await _chatService.GenerateResponseAsync(
            new[] { ChatMessage.User(planningPrompt) }, maxTokens: -1, ct);
        
        var searchQueries = planningResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(q => q.Trim().TrimStart('-', '*', '1', '2', '3', '4', '5', '.', ' '))
            .Where(q => !string.IsNullOrEmpty(q))
            .Take(options.MaxQueries)
            .ToList();

        if (!searchQueries.Any())
        {
            searchQueries.Add(query);
        }

        // 2. Searching
        progress.Phase = ResearchPhase.Searching;
        progress.StatusMessage = $"Searching Web for planned queries...";
        progress.CurrentStep = 1;
        yield return progress;

        var allSearchResults = new List<SearchResult>();
        for (int i = 0; i < searchQueries.Count; i++)
        {
            var searchQuery = searchQueries[i];
            
            // Add a delay between queries to avoid DuckDuckGo rate limiting
            if (i > 0)
            {
                await Task.Delay(1500, ct);
            }

            progress.StatusMessage = $"Searching for: \"{searchQuery}\"... ({i + 1}/{searchQueries.Count})";
            yield return progress;

            try
            {
                var results = await _webSearchService.SearchAsync(searchQuery, 3, ct);
                allSearchResults.AddRange(results);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeepResearch] Search error for query '{searchQuery}': {ex.Message}");
            }
        }

        // De-duplicate search results by URL
        var uniqueResults = allSearchResults
            .GroupBy(r => r.Link)
            .Select(g => g.First())
            .Take(options.MaxSources)
            .ToList();

        // If no results, try fallback with original query
        if (!uniqueResults.Any())
        {
            progress.StatusMessage = $"No results. Attempting fallback search for original query...";
            yield return progress;

            await Task.Delay(1500, ct);
            try
            {
                var results = await _webSearchService.SearchAsync(query, 3, ct);
                uniqueResults = results
                    .GroupBy(r => r.Link)
                    .Select(g => g.First())
                    .Take(options.MaxSources)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeepResearch] Fallback search error: {ex.Message}");
            }
        }

        progress.SourcesFound = uniqueResults.Select(r => new ResearchSource
        {
            Title = r.Title,
            Url = r.Link,
            Snippet = r.Snippet,
            IsRead = false
        }).ToList();
        
        yield return progress;

        // 3. Reading Sources
        progress.Phase = ResearchPhase.ReadingSources;
        progress.StatusMessage = "Reading source page contents...";
        progress.CurrentStep = 2;
        yield return progress;

        for (int i = 0; i < progress.SourcesFound.Count; i++)
        {
            var source = progress.SourcesFound[i];
            progress.StatusMessage = $"Fetching page {i + 1}/{progress.SourcesFound.Count}: {source.Title}";
            yield return progress;

            try
            {
                var content = await _webSearchService.GetPageContentAsync(source.Url, ct);
                // Truncate to avoid overflowing the 32K context with raw page text
                source.FullContent = content.Length > MaxCharsPerSource
                    ? content[..MaxCharsPerSource] + "\n... [content truncated]"
                    : content;
                source.IsRead = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeepResearch] Content fetch error for {source.Url}: {ex.Message}");
                source.FullContent = $"[Failed to fetch content: {ex.Message}]";
                source.IsRead = false;
            }
            yield return progress;
        }

        // 4. Analyzing
        progress.Phase = ResearchPhase.Analyzing;
        progress.StatusMessage = "Analyzing collected information...";
        progress.CurrentStep = 3;
        yield return progress;

        var analysisContextBuilder = new StringBuilder();
        bool hasReadSources = progress.SourcesFound.Any(s => s.IsRead);
        
        string analysisPrompt;
        if (hasReadSources)
        {
            analysisContextBuilder.AppendLine("Below is the collected information from web sources:");
            for (int i = 0; i < progress.SourcesFound.Count; i++)
            {
                var source = progress.SourcesFound[i];
                if (source.IsRead)
                {
                    analysisContextBuilder.AppendLine($"\nSource [{i + 1}]: {source.Title} ({source.Url})");
                    analysisContextBuilder.AppendLine(source.FullContent);
                }
            }
            analysisPrompt = $"{analysisContextBuilder}\n\nBased on the above sources, identify key findings, contradictions, and gaps to address the original question: \"{query}\".";
        }
        else
        {
            progress.StatusMessage = "No web sources found. Analyzing based on internal knowledge...";
            yield return progress;
            analysisPrompt = $"Identify key findings and analysis points to address the question: \"{query}\" based on your pre-trained internal knowledge.";
        }
        
        _chatService.ClearContext(DeepResearchContextSize);
        var analysisResponse = await _chatService.GenerateResponseAsync(
            new[] { ChatMessage.User(analysisPrompt) }, maxTokens: -1, ct);

        // 5. Writing Report
        progress.Phase = ResearchPhase.WritingReport;
        progress.StatusMessage = "Synthesizing and writing final report...";
        progress.CurrentStep = 4;
        yield return progress;

        string reportPrompt;
        if (hasReadSources)
        {
            reportPrompt = $"Synthesize a final comprehensive report for the query: \"{query}\" based on this analysis:\n\n{analysisResponse}\n\n" +
                "The report MUST include:\n" +
                "1. Executive Summary\n" +
                "2. Key Findings\n" +
                "3. Detailed Analysis\n" +
                "4. Sources (with clickable inline markdown citations matching the source URLs, e.g. [1](url), [2](url))\n\n" +
                "Format the output beautifully with markdown.";
        }
        else
        {
            reportPrompt = $"Synthesize a final comprehensive report for the query: \"{query}\" based on this analysis:\n\n{analysisResponse}\n\n" +
                "The report MUST include:\n" +
                "1. Executive Summary\n" +
                "2. Key Findings\n" +
                "3. Detailed Analysis\n\n" +
                "Format the output beautifully with markdown. Mention that no real-time web sources were accessible at this time.";
        }

        var reportBuilder = new StringBuilder();
        _chatService.ClearContext(DeepResearchContextSize);
        await foreach (var token in _chatService.GenerateResponseStreamAsync(
            new[] { ChatMessage.User(reportPrompt) }, false, null, null, null, maxTokens: -1, ct))
        {
            reportBuilder.Append(token);
            progress.PartialReport = reportBuilder.ToString();
            yield return progress;
        }

        // 6. Complete — restore normal context size
        _chatService.ClearContext(8192);
        progress.Phase = ResearchPhase.Complete;
        progress.StatusMessage = "Research completed successfully.";
        progress.CurrentStep = 5;
        yield return progress;
    }
}
