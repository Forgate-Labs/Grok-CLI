using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GrokCLI.Models;
using OpenAI.Chat;

namespace GrokCLI.UI;

public sealed class ToolSummaryBuilder
{
    private readonly Func<int> _windowWidthProvider;

    public ToolSummaryBuilder(Func<int> windowWidthProvider)
    {
        _windowWidthProvider = windowWidthProvider;
    }

    public string BuildToolCall(ToolCallEvent toolEvent)
    {
        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLine($"🔧 [Tool: {toolEvent.ToolName}]");

        if (!string.IsNullOrWhiteSpace(toolEvent.ArgumentsJson))
        {
            builder.AppendLine("📋 Arguments:");
            builder.AppendLine(toolEvent.ArgumentsJson);
        }

        return builder.ToString();
    }

    public string BuildToolSummary(ToolResultEvent toolEvent, ChatTokenUsage? lastUsage, Func<string> durationTextFactory)
    {
        return toolEvent.ToolName switch
        {
            "search" => BuildSearchSummary(toolEvent),
            "code_execution" => BuildCodeExecutionSummary(toolEvent),
            "run_command" => BuildCommandSummary(toolEvent),
            "read_local_file" => BuildReadSummary(toolEvent),
            "edit_file" => BuildEditSummary(toolEvent),
            "change_directory" => BuildChangeDirectorySummary(toolEvent),
            "web_search" => BuildWebSearchSummary(toolEvent),
            "workflow_done" => BuildDoneSummary(durationTextFactory(), lastUsage),
            _ => BuildGenericSummary(toolEvent)
        };
    }

    private string BuildSearchSummary(ToolResultEvent toolEvent)
    {
        var pattern = TryGetString(toolEvent.ArgumentsJson, "pattern") ?? "";
        var path = TryGetString(toolEvent.ArgumentsJson, "path") ?? Directory.GetCurrentDirectory();

        var builder = new StringBuilder();
        builder.Append(SummaryTextFormatter.BuildHeader($"Search(pattern: \"{pattern}\", path: \"{path}\")"));

        if (toolEvent.Result.Success)
        {
            var matchCount = TryGetInt(toolEvent.Result.Output, "total_matches") ?? 0;
            var noun = matchCount == 1 ? "match" : "matches";
            builder.Append(SummaryTextFormatter.BuildLine($"Found {matchCount} {noun}"));
        }
        else
        {
            var message = string.IsNullOrWhiteSpace(toolEvent.Result.Error)
                ? "Search failed"
                : toolEvent.Result.Error;
            builder.Append(SummaryTextFormatter.BuildLine(message));
        }

        return builder.ToString();
    }

    private string BuildCodeExecutionSummary(ToolResultEvent toolEvent)
    {
        var code = TryGetString(toolEvent.ArgumentsJson, "code") ?? "";
        var snippet = SummaryTextFormatter.Truncate(code.Replace("\n", " "), 80);
        var path = Directory.GetCurrentDirectory();

        var message = toolEvent.Result.Success
            ? SummaryTextFormatter.Normalize(toolEvent.Result.Output)
            : SummaryTextFormatter.Normalize(string.IsNullOrWhiteSpace(toolEvent.Result.Error)
                ? "Execution failed"
                : toolEvent.Result.Error);

        if (string.IsNullOrWhiteSpace(message))
        {
            message = toolEvent.Result.Success ? "Completed with no output" : "No error output";
        }

        var builder = new StringBuilder();
        builder.Append(SummaryTextFormatter.BuildHeader($"Python(path: \"{path}\", command: \"{snippet}\")"));
        builder.Append(SummaryTextFormatter.BuildBlock(message));
        return builder.ToString();
    }

    private string BuildCommandSummary(ToolResultEvent toolEvent)
    {
        var command = TryGetString(toolEvent.ArgumentsJson, "command") ?? "";
        var workingDirectory = TryGetString(toolEvent.ArgumentsJson, "working_directory");
        var location = string.IsNullOrWhiteSpace(workingDirectory)
            ? Directory.GetCurrentDirectory()
            : workingDirectory;
        var snippet = SummaryTextFormatter.Truncate(command.Replace("\n", " "), 80);

        var successMessage = toolEvent.Result.Output;
        if (!string.IsNullOrWhiteSpace(toolEvent.Result.Error))
        {
            successMessage = string.IsNullOrWhiteSpace(successMessage)
                ? toolEvent.Result.Error
                : $"{successMessage.TrimEnd()}\n{toolEvent.Result.Error}";
        }

        if (string.IsNullOrWhiteSpace(successMessage))
        {
            successMessage = $"Exit code {toolEvent.Result.ExitCode} with no output";
        }

        var failureMessage = !string.IsNullOrWhiteSpace(toolEvent.Result.Error)
            ? toolEvent.Result.Error
            : (!string.IsNullOrWhiteSpace(toolEvent.Result.Output)
                ? toolEvent.Result.Output
                : $"Command failed with exit code {toolEvent.Result.ExitCode}");

        var message = toolEvent.Result.Success
            ? SummaryTextFormatter.Normalize(successMessage)
            : SummaryTextFormatter.Normalize(failureMessage);

        var builder = new StringBuilder();
        builder.Append(SummaryTextFormatter.BuildHeader($"Command(path: \"{location}\", command: \"{snippet}\")"));
        builder.Append(SummaryTextFormatter.BuildBlock(message));
        return builder.ToString();
    }

    private string BuildReadSummary(ToolResultEvent toolEvent)
    {
        var path = TryGetString(toolEvent.ArgumentsJson, "path") ?? "unknown";
        if (toolEvent.Result.Success)
        {
            var lines = CountLines(toolEvent.Result.Output);
            var tokens = EstimateTokenCount(toolEvent.Result.Output);
            var builder = new StringBuilder();
            builder.Append(SummaryTextFormatter.BuildHeader($"Read({path})"));
            builder.Append(SummaryTextFormatter.BuildLine($"Read {lines} lines ({tokens} tokens)"));
            return builder.ToString();
        }

        var message = string.IsNullOrWhiteSpace(toolEvent.Result.Error)
            ? "Read failed"
            : toolEvent.Result.Error;
        var errorBuilder = new StringBuilder();
        errorBuilder.Append(SummaryTextFormatter.BuildHeader($"Read({path})"));
        errorBuilder.Append(SummaryTextFormatter.BuildLine(SummaryTextFormatter.Normalize(message)));
        return errorBuilder.ToString();
    }

    private string BuildEditSummary(ToolResultEvent toolEvent)
    {
        var path = TryGetString(toolEvent.ArgumentsJson, "file_path") ?? "unknown";
        if (!toolEvent.Result.Success)
        {
            var errorMessage = string.IsNullOrWhiteSpace(toolEvent.Result.Error)
                ? "Update failed"
                : toolEvent.Result.Error;
            var builder = new StringBuilder();
            builder.Append(SummaryTextFormatter.BuildHeader($"Update({path})"));
            builder.Append(SummaryTextFormatter.BuildLine(SummaryTextFormatter.Normalize(errorMessage)));
            return builder.ToString();
        }

        var metadata = ParseEditMetadata(toolEvent.Result.Output);
        var count = metadata?.LinesModified ?? 0;
        var successBuilder = new StringBuilder();
        successBuilder.Append(SummaryTextFormatter.BuildHeader($"Update({path})"));
        successBuilder.Append(SummaryTextFormatter.BuildLine($"Update {count} lines"));
        return successBuilder.ToString();
    }

    private string BuildChangeDirectorySummary(ToolResultEvent toolEvent)
    {
        var path = TryGetString(toolEvent.ArgumentsJson, "path") ?? "";

        var builder = new StringBuilder();
        builder.Append(SummaryTextFormatter.BuildHeader($"ChangeDirectory(path: \"{path}\")"));

        if (toolEvent.Result.Success)
        {
            var destination = TryGetString(toolEvent.Result.Output, "current_directory") ?? "unknown";
            builder.Append(SummaryTextFormatter.BuildLine($"Now at {destination}"));
        }
        else
        {
            var message = string.IsNullOrWhiteSpace(toolEvent.Result.Error)
                ? "Directory change failed"
                : toolEvent.Result.Error;
            builder.Append(SummaryTextFormatter.BuildLine(SummaryTextFormatter.Normalize(message)));
        }

        return builder.ToString();
    }

    private string BuildWebSearchSummary(ToolResultEvent toolEvent)
    {
        var query = TryGetString(toolEvent.ArgumentsJson, "query") ?? "";
        var message = string.IsNullOrWhiteSpace(toolEvent.Result.Error)
            ? "Web search is not available"
            : toolEvent.Result.Error;
        var builder = new StringBuilder();
        builder.Append(SummaryTextFormatter.BuildHeader($"WebSearch(query: \"{query}\")"));
        builder.Append(SummaryTextFormatter.BuildLine(SummaryTextFormatter.Normalize(message)));
        return builder.ToString();
    }

    private string BuildDoneSummary(string durationText, ChatTokenUsage? lastUsage)
    {
        var totalTokens = lastUsage?.TotalTokenCount ?? 0;
        var reasoningTokens = lastUsage?.OutputTokenDetails?.ReasoningTokenCount ?? 0;
        return $"\n{BuildDoneLine(durationText, totalTokens, reasoningTokens)}\n";
    }

    private string BuildGenericSummary(ToolResultEvent toolEvent)
    {
        var message = toolEvent.Result.Success && !string.IsNullOrWhiteSpace(toolEvent.Result.Output)
            ? toolEvent.Result.Output
            : toolEvent.Result.Success
                ? "Completed"
                : string.IsNullOrWhiteSpace(toolEvent.Result.Error)
                    ? "Tool failed"
                    : toolEvent.Result.Error;
        message = SummaryTextFormatter.Normalize(message);
        var builder = new StringBuilder();
        builder.Append(SummaryTextFormatter.BuildHeader($"{toolEvent.ToolName}()"));
        builder.Append(SummaryTextFormatter.BuildBlock(message));
        return builder.ToString();
    }

    private string BuildDoneLine(string durationText, int totalTokens, int reasoningTokens)
    {
        var prefix = $"─ Worked for {durationText} - {totalTokens} total tokens - {reasoningTokens} reasoning tokens ";
        var width = _windowWidthProvider();
        var remaining = Math.Max(0, width - prefix.Length);
        return prefix + new string('─', remaining);
    }

    private static int EstimateTokenCount(string content)
    {
        if (string.IsNullOrEmpty(content))
            return 0;

        return Regex.Matches(content, @"\S+").Count;
    }

    private static int CountLines(string content)
    {
        if (string.IsNullOrEmpty(content))
            return 0;

        return content.Split('\n').Length;
    }

    private static int? TryGetInt(string? json, string property)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetInt32();
            }
        }
        catch
        {
        }

        return null;
    }

    private static string? TryGetString(string? json, string property)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty(property, out var prop))
            {
                return prop.GetString();
            }
        }
        catch
        {
        }

        return null;
    }

    private static EditResultMetadata? ParseEditMetadata(string output)
    {
        try
        {
            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;

            return new EditResultMetadata
            {
                FilePath = root.TryGetProperty("file_path", out var filePathProp)
                    ? filePathProp.GetString()
                    : null,
                BackupPath = root.TryGetProperty("backup_path", out var backupProp)
                    ? backupProp.GetString()
                    : null,
                LinesModified = root.TryGetProperty("lines_modified", out var linesProp)
                    ? linesProp.GetInt32()
                    : 0
            };
        }
        catch
        {
            return null;
        }
    }

    private sealed class EditResultMetadata
    {
        public string? FilePath { get; init; }
        public string? BackupPath { get; init; }
        public int LinesModified { get; init; }
    }
}
