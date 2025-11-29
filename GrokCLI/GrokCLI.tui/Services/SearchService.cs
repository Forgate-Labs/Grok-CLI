using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GrokCLI.Models;

namespace GrokCLI.Services;

public class SearchService : ISearchService
{
    private readonly IPlatformService _platformService;
    private readonly IShellExecutor _shellExecutor;
    private readonly IWorkingDirectoryService _workingDirService;

    public SearchService(
        IPlatformService platformService,
        IShellExecutor shellExecutor,
        IWorkingDirectoryService workingDirService)
    {
        _platformService = platformService;
        _shellExecutor = shellExecutor;
        _workingDirService = workingDirService;
    }

    public string GetPlatformType()
    {
        return _platformService.Platform.ToString();
    }

    public bool IsRipgrepAvailable()
    {
        if (_platformService.IsWindows)
            return false;

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "rg",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit(1000);

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<SearchResult> SearchAsync(SearchOptions options)
    {
        var resolvedPath = _workingDirService.ResolveRelativePath(options.SearchPath);
        options.SearchPath = resolvedPath;

        if (!Directory.Exists(resolvedPath))
        {
            return new SearchResult
            {
                Success = false,
                Error = $"Directory not found: {resolvedPath}",
                Platform = GetPlatformType()
            };
        }

        if (_platformService.IsWindows)
        {
            return await SearchWithPowerShellAsync(options);
        }
        else if (_platformService.IsLinux || _platformService.IsMacOS)
        {
            if (IsRipgrepAvailable())
            {
                return await SearchWithRipgrepAsync(options);
            }
            else
            {
                return await SearchWithGrepAsync(options);
            }
        }
        else
        {
            return new SearchResult
            {
                Success = false,
                Error = "Unsupported platform",
                Platform = GetPlatformType()
            };
        }
    }

    private async Task<SearchResult> SearchWithRipgrepAsync(SearchOptions options)
    {
        var args = new List<string>();

        if (options.IsRegex)
            args.Add("-e");
        else
            args.Add("-F");

        args.Add($"'{options.Pattern.Replace("'", "'\\''")}'");

        if (!options.CaseSensitive)
            args.Add("-i");

        if (options.ContextLines > 0)
            args.Add($"-C {options.ContextLines}");

        args.Add("-n");

        args.Add($"-m {options.MaxResults}");

        if (!string.IsNullOrEmpty(options.FileType))
            args.Add($"--glob '*.{options.FileType}'");

        args.Add("--json");

        args.Add($"'{options.SearchPath.Replace("'", "'\\''")}'");

        var command = $"rg {string.Join(" ", args)}";

        var result = new SearchResult
        {
            SearchCommand = command,
            Platform = $"Linux/macOS (ripgrep)"
        };

        var shellResult = await _shellExecutor.ExecuteAsync(command, options.SearchPath, options.TimeoutSeconds);

        if (shellResult.ExitCode == 0 || shellResult.ExitCode == 1)
        {
            result.Matches = ParseRipgrepJsonOutput(shellResult.Output);
            result.TotalMatches = result.Matches.Count;
            result.Success = true;
        }
        else
        {
            result.Error = !string.IsNullOrEmpty(shellResult.Error)
                ? shellResult.Error
                : "Search failed";
            result.Success = false;
        }

        return result;
    }

    private async Task<SearchResult> SearchWithGrepAsync(SearchOptions options)
    {
        var args = new List<string>();

        args.Add("-r");
        args.Add("-n");

        if (!options.CaseSensitive)
            args.Add("-i");

        if (options.ContextLines > 0)
            args.Add($"-C {options.ContextLines}");

        args.Add($"-m {options.MaxResults}");

        if (!options.IsRegex)
            args.Add("-F");

        if (!string.IsNullOrEmpty(options.FileType))
            args.Add($"--include='*.{options.FileType}'");

        args.Add($"'{options.Pattern.Replace("'", "'\\''")}'");

        args.Add($"'{options.SearchPath.Replace("'", "'\\''")}'");

        var command = $"grep {string.Join(" ", args)}";

        var result = new SearchResult
        {
            SearchCommand = command,
            Platform = "Linux/macOS (grep)"
        };

        var shellResult = await _shellExecutor.ExecuteAsync(command, options.SearchPath, options.TimeoutSeconds);

        if (shellResult.ExitCode == 0 || shellResult.ExitCode == 1)
        {
            result.Matches = ParseGrepOutput(shellResult.Output);
            result.TotalMatches = result.Matches.Count;
            result.Success = true;
        }
        else
        {
            result.Error = !string.IsNullOrEmpty(shellResult.Error)
                ? shellResult.Error
                : "Search failed";
            result.Success = false;
        }

        return result;
    }

    private async Task<SearchResult> SearchWithPowerShellAsync(SearchOptions options)
    {
        var psCommand = BuildPowerShellSearchCommand(options);

        var result = new SearchResult
        {
            SearchCommand = psCommand,
            Platform = "Windows (PowerShell)"
        };

        var shellResult = await _shellExecutor.ExecuteAsync(psCommand, options.SearchPath, options.TimeoutSeconds);

        if (shellResult.Success)
        {
            result.Matches = ParsePowerShellOutput(shellResult.Output);
            result.TotalMatches = result.Matches.Count;
            result.Success = true;
        }
        else
        {
            result.Error = !string.IsNullOrEmpty(shellResult.Error)
                ? shellResult.Error
                : "Search failed";
            result.Success = false;
        }

        return result;
    }

    private string BuildPowerShellSearchCommand(SearchOptions options)
    {
        var pattern = options.Pattern.Replace("\"", "`\"");

        var cmd = new StringBuilder();
        cmd.Append($"Get-ChildItem -Path '{options.SearchPath}' -Recurse -File");

        if (!string.IsNullOrEmpty(options.FileType))
        {
            cmd.Append($" -Filter '*.{options.FileType}'");
        }

        cmd.Append(" | Select-String");

        if (options.IsRegex)
        {
            cmd.Append($" -Pattern \"{pattern}\"");
        }
        else
        {
            cmd.Append($" -Pattern \"{Regex.Escape(pattern)}\"");
        }

        if (options.CaseSensitive)
        {
            cmd.Append(" -CaseSensitive");
        }

        if (options.ContextLines > 0)
        {
            cmd.Append($" -Context {options.ContextLines},{options.ContextLines}");
        }

        cmd.Append($" | Select-Object -First {options.MaxResults}");

        cmd.Append(" | Select-Object Path, LineNumber, Line, Context | ConvertTo-Json -Depth 3");

        return cmd.ToString();
    }

    private List<SearchMatch> ParseRipgrepJsonOutput(string jsonOutput)
    {
        var matches = new List<SearchMatch>();

        if (string.IsNullOrWhiteSpace(jsonOutput))
            return matches;

        var lines = jsonOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            try
            {
                var json = JsonDocument.Parse(line);
                var root = json.RootElement;

                if (root.TryGetProperty("type", out var typeProp) &&
                    typeProp.GetString() == "match")
                {
                    var data = root.GetProperty("data");
                    var match = new SearchMatch
                    {
                        FilePath = data.GetProperty("path").GetProperty("text").GetString() ?? "",
                        LineNumber = data.GetProperty("line_number").GetInt32(),
                        LineContent = data.GetProperty("lines").GetProperty("text").GetString() ?? ""
                    };
                    matches.Add(match);
                }
            }
            catch
            {
            }
        }

        return matches;
    }

    private List<SearchMatch> ParseGrepOutput(string output)
    {
        var matches = new List<SearchMatch>();

        if (string.IsNullOrWhiteSpace(output))
            return matches;

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var parts = line.Split(':', 3);
            if (parts.Length >= 3)
            {
                var filePath = parts[0];
                if (int.TryParse(parts[1], out var lineNumber))
                {
                    var match = new SearchMatch
                    {
                        FilePath = filePath,
                        LineNumber = lineNumber,
                        LineContent = parts[2]
                    };
                    matches.Add(match);
                }
            }
        }

        return matches;
    }

    private List<SearchMatch> ParsePowerShellOutput(string jsonOutput)
    {
        var matches = new List<SearchMatch>();

        if (string.IsNullOrWhiteSpace(jsonOutput))
            return matches;

        try
        {
            var json = JsonDocument.Parse(jsonOutput);
            var items = json.RootElement.ValueKind == JsonValueKind.Array
                ? json.RootElement.EnumerateArray()
                : new[] { json.RootElement }.AsEnumerable();

            foreach (var item in items)
            {
                var match = new SearchMatch
                {
                    FilePath = item.GetProperty("Path").GetString() ?? "",
                    LineNumber = item.GetProperty("LineNumber").GetInt32(),
                    LineContent = item.GetProperty("Line").GetString() ?? ""
                };

                if (item.TryGetProperty("Context", out var context))
                {
                    if (context.TryGetProperty("PreContext", out var pre))
                    {
                        match.ContextBefore = pre.EnumerateArray()
                            .Select(e => e.GetString() ?? "")
                            .ToList();
                    }
                    if (context.TryGetProperty("PostContext", out var post))
                    {
                        match.ContextAfter = post.EnumerateArray()
                            .Select(e => e.GetString() ?? "")
                            .ToList();
                    }
                }

                matches.Add(match);
            }
        }
        catch
        {
        }

        return matches;
    }
}
