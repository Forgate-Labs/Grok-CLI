namespace GrokCLI.Models;

public class SearchOptions
{
    public string Pattern { get; set; } = string.Empty;
    public string SearchPath { get; set; } = ".";
    public string? FileType { get; set; }
    public bool CaseSensitive { get; set; } = false;
    public int ContextLines { get; set; } = 0;
    public int MaxResults { get; set; } = 100;
    public bool IsRegex { get; set; } = false;
    public int TimeoutSeconds { get; set; } = 30;
}

public class SearchResult
{
    public bool Success { get; set; }
    public List<SearchMatch> Matches { get; set; } = new();
    public int TotalMatches { get; set; }
    public string SearchCommand { get; set; } = string.Empty;
    public string? Error { get; set; }
    public string Platform { get; set; } = string.Empty;
}

public class SearchMatch
{
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string LineContent { get; set; } = string.Empty;
    public List<string> ContextBefore { get; set; } = new();
    public List<string> ContextAfter { get; set; } = new();
}
