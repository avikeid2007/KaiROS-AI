using System.Collections.Generic;

namespace KaiROS.AI.WinUI.Models;

public enum ResearchPhase
{
    PlanningQueries,
    Searching,
    ReadingSources,
    Analyzing,
    WritingReport,
    Complete
}

public class ResearchProgress
{
    public ResearchPhase Phase { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public int CurrentStep { get; set; }
    public int TotalSteps { get; set; }
    public List<ResearchSource> SourcesFound { get; set; } = [];
    public string PartialReport { get; set; } = string.Empty;
}

public class ResearchSource
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public string FullContent { get; set; } = string.Empty;
    public bool IsRead { get; set; }
}

public class ResearchOptions
{
    public int MaxQueries { get; set; } = 3;
    public int MaxSources { get; set; } = 5;
}
