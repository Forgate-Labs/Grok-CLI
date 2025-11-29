using System.Text.Json;
using GrokCLI.Models;
using GrokCLI.Services;
using OpenAI.Chat;

namespace GrokCLI.Tools;

public class SearchTool : ITool
{
    private readonly ISearchService _searchService;

    public string Name => "search";
    public string Description => "Search for text patterns in files. Supports regex, file type filters, and context lines. Uses ripgrep on Linux/macOS or PowerShell on Windows.";

    public SearchTool(ISearchService searchService)
    {
        _searchService = searchService;
    }

    public ChatTool GetChatTool()
    {
        return ChatTool.CreateFunctionTool(
            Name,
            Description,
            BinaryData.FromString(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""pattern"": {
                        ""type"": ""string"",
                        ""description"": ""The text or regex pattern to search for""
                    },
                    ""path"": {
                        ""type"": ""string"",
                        ""description"": ""Directory or file to search in (default: current directory)""
                    },
                    ""file_type"": {
                        ""type"": ""string"",
                        ""description"": ""Filter by file extension (e.g., 'cs', 'txt', 'json')""
                    },
                    ""case_sensitive"": {
                        ""type"": ""boolean"",
                        ""description"": ""Whether the search should be case-sensitive (default: false)""
                    },
                    ""context_lines"": {
                        ""type"": ""integer"",
                        ""description"": ""Number of context lines to show before and after match (default: 0)""
                    },
                    ""max_results"": {
                        ""type"": ""integer"",
                        ""description"": ""Maximum number of results to return (default: 100)""
                    },
                    ""regex"": {
                        ""type"": ""boolean"",
                        ""description"": ""Treat pattern as regex (default: false, literal search)""
                    }
                },
                ""required"": [""pattern""]
            }")
        );
    }

    public async Task<ToolExecutionResult> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var jsonDoc = JsonDocument.Parse(argumentsJson);
            var root = jsonDoc.RootElement;

            var pattern = root.GetProperty("pattern").GetString() ?? "";

            if (string.IsNullOrWhiteSpace(pattern))
            {
                return ToolExecutionResult.CreateError("Pattern cannot be empty");
            }

            var options = new SearchOptions
            {
                Pattern = pattern,
                SearchPath = root.TryGetProperty("path", out var pathProp)
                    ? pathProp.GetString() ?? "."
                    : ".",
                FileType = root.TryGetProperty("file_type", out var fileTypeProp)
                    ? fileTypeProp.GetString()
                    : null,
                CaseSensitive = root.TryGetProperty("case_sensitive", out var caseProp)
                    ? caseProp.GetBoolean()
                    : false,
                ContextLines = root.TryGetProperty("context_lines", out var contextProp)
                    ? contextProp.GetInt32()
                    : 0,
                MaxResults = root.TryGetProperty("max_results", out var maxProp)
                    ? maxProp.GetInt32()
                    : 100,
                IsRegex = root.TryGetProperty("regex", out var regexProp)
                    ? regexProp.GetBoolean()
                    : false
            };

            var result = await _searchService.SearchAsync(options);

            if (!result.Success)
            {
                return ToolExecutionResult.CreateError(result.Error ?? "Search failed");
            }

            var response = JsonSerializer.Serialize(new
            {
                success = result.Success,
                total_matches = result.TotalMatches,
                platform = result.Platform,
                search_command = result.SearchCommand,
                matches = result.Matches.Select(m => new
                {
                    file_path = m.FilePath,
                    line_number = m.LineNumber,
                    line_content = m.LineContent,
                    context_before = m.ContextBefore,
                    context_after = m.ContextAfter
                })
            });

            return ToolExecutionResult.CreateSuccess(response);
        }
        catch (JsonException ex)
        {
            return ToolExecutionResult.CreateError($"Invalid JSON: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ToolExecutionResult.CreateError($"Error executing search: {ex.Message}");
        }
    }
}
