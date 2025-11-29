using System.Threading;
using GrokCLI.Models;

namespace GrokCLI.Services;

public interface ISearchService
{
    Task<SearchResult> SearchAsync(SearchOptions options, CancellationToken cancellationToken);
    bool IsRipgrepAvailable();
    string GetPlatformType();
}
