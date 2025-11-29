using GrokCLI.Models;

namespace GrokCLI.Services;

public interface ISearchService
{
    Task<SearchResult> SearchAsync(SearchOptions options);
    bool IsRipgrepAvailable();
    string GetPlatformType();
}
