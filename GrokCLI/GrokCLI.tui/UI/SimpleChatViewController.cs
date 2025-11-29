using System.Text.Json;
using System.Text.RegularExpressions;
using GrokCLI.Models;
using GrokCLI.Services;
using OpenAI.Chat;

namespace GrokCLI.UI;

public class SimpleChatViewController
{
    private readonly IChatService _chatService;
    private readonly List<ChatMessage> _conversation;
    private readonly SimpleTerminalUI _ui;
    private readonly bool _isEnabled;
    private ChatDisplayMode _displayMode;

    public SimpleChatViewController(
        IChatService chatService,
        SimpleTerminalUI ui,
        bool isEnabled,
        ChatDisplayMode displayMode)
    {
        _chatService = chatService;
        _conversation = new List<ChatMessage>();
        _ui = ui;
        _isEnabled = isEnabled;
        _displayMode = displayMode;

        _chatService.OnTextReceived += OnTextReceived;
        _chatService.OnToolCalled += OnToolCalled;
        _chatService.OnToolResult += OnToolResult;
    }

    public async Task SendMessageAsync()
    {
        if (!_isEnabled) return;

        var userText = _ui.GetCurrentInput()?.Trim();
        if (string.IsNullOrWhiteSpace(userText)) return;

        _ui.HideInputLine();

        Console.WriteLine($"[You]: {userText}");
        Console.Write("[Grok]: ");

        _ui.ClearInput();

        _ui.SetProcessingStatus("thinking...");

        try
        {
            await _chatService.SendMessageAsync(userText, _conversation);
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
        }
        finally
        {
            _ui.SetProcessingStatus("");
            _ui.ShowInputLine();
        }
    }

    public void ClearChat()
    {
        if (!_isEnabled) return;
        Console.Clear();
        _conversation.Clear();
        ShowWelcomeMessage();
    }

    public void SetDisplayMode(ChatDisplayMode mode)
    {
        _displayMode = mode;
        _ui.WriteLine($"Mode switched to {_displayMode}");
    }

    public void ShowWelcomeMessage()
    {
        _ui.WriteLine("Grok CLI - Agentic Mode");
        _ui.WriteLine($"Mode: {_displayMode} (type \"debug\" or \"normal\" to switch)");
        _ui.WriteLine("Commands: Ctrl+J (newline) | Ctrl+C (exit) | clear (clear terminal) | cmd <command> (run shell command)");
        _ui.WriteLine("Model: grok-4-1-fast-reasoning");
        _ui.WriteLine("");
    }

    public void ShowSystemMessage(string message)
    {
        _ui.WriteLine(message);
    }

    private void OnTextReceived(string text)
    {
        Console.Write(text);
    }

    private void OnToolCalled(ToolCallEvent toolEvent)
    {
        if (_displayMode != ChatDisplayMode.Debug)
            return;

        if (string.IsNullOrEmpty(toolEvent.ArgumentsJson))
        {
            Console.WriteLine($"\n🔧 [Tool: {toolEvent.ToolName}]");
        }
        else
        {
            Console.WriteLine("📋 Arguments:");
            Console.WriteLine(toolEvent.ArgumentsJson);
        }
    }

    private void OnToolResult(ToolResultEvent toolEvent)
    {
        if (_displayMode == ChatDisplayMode.Debug)
        {
            RenderDebugToolResult(toolEvent);
        }
        else
        {
            RenderNormalToolResult(toolEvent);
        }

        Console.WriteLine();
    }

    private void RenderDebugToolResult(ToolResultEvent toolEvent)
    {
        Console.WriteLine("✅ Result:");
        Console.WriteLine(GetDebugResultText(toolEvent));
    }

    private string GetDebugResultText(ToolResultEvent toolEvent)
    {
        if (toolEvent.ToolName == "read_local_file" && toolEvent.Result.Success)
        {
            var path = TryGetString(toolEvent.ArgumentsJson, "path") ?? "unknown";
            var tokens = EstimateTokenCount(toolEvent.Result.Output);
            return $"{path} ({tokens} tokens)";
        }

        return toolEvent.Result.Success
            ? toolEvent.Result.Output
            : toolEvent.Result.Error;
    }

    private void RenderNormalToolResult(ToolResultEvent toolEvent)
    {
        switch (toolEvent.ToolName)
        {
            case "search":
                RenderSearchSummary(toolEvent);
                break;
            case "code_execution":
                RenderCodeExecutionSummary(toolEvent);
                break;
            case "read_local_file":
                RenderReadSummary(toolEvent);
                break;
            case "edit_file":
                RenderEditSummary(toolEvent);
                break;
            case "change_directory":
                RenderChangeDirectorySummary(toolEvent);
                break;
            case "web_search":
                RenderWebSearchSummary(toolEvent);
                break;
            case "test":
                RenderTestSummary(toolEvent);
                break;
            default:
                RenderGenericSummary(toolEvent);
                break;
        }
    }

    private void RenderSearchSummary(ToolResultEvent toolEvent)
    {
        var pattern = TryGetString(toolEvent.ArgumentsJson, "pattern") ?? "";
        var path = TryGetString(toolEvent.ArgumentsJson, "path") ?? Directory.GetCurrentDirectory();
        WriteSummaryHeader($"Search(pattern: \"{pattern}\", path: \"{path}\")");

        if (toolEvent.Result.Success)
        {
            var matchCount = TryGetInt(toolEvent.Result.Output, "total_matches") ?? 0;
            var noun = matchCount == 1 ? "match" : "matches";
            WriteSummaryLine($"Found {matchCount} {noun}");
        }
        else
        {
            var message = string.IsNullOrWhiteSpace(toolEvent.Result.Error)
                ? "Search failed"
                : toolEvent.Result.Error;
            WriteSummaryLine(message, ConsoleColor.Red);
        }
    }

    private void RenderCodeExecutionSummary(ToolResultEvent toolEvent)
    {
        var code = TryGetString(toolEvent.ArgumentsJson, "code") ?? "";
        var snippet = Truncate(code.Replace("\n", " "), 80);
        var path = Directory.GetCurrentDirectory();
        WriteSummaryHeader($"Python(path: \"{path}\", command: \"{snippet}\")");

        var message = toolEvent.Result.Success
            ? NormalizeOutput(toolEvent.Result.Output)
            : NormalizeOutput(string.IsNullOrWhiteSpace(toolEvent.Result.Error)
                ? "Execution failed"
                : toolEvent.Result.Error);

        if (string.IsNullOrWhiteSpace(message))
        {
            message = toolEvent.Result.Success ? "Completed with no output" : "No error output";
        }

        var color = toolEvent.Result.Success ? ConsoleColor.Green : ConsoleColor.Red;
        WriteSummaryBlock(message, color);
    }

    private void RenderReadSummary(ToolResultEvent toolEvent)
    {
        var path = TryGetString(toolEvent.ArgumentsJson, "path") ?? "unknown";
        WriteSummaryHeader($"Read({path})");

        if (toolEvent.Result.Success)
        {
            var lines = CountLines(toolEvent.Result.Output);
            var tokens = EstimateTokenCount(toolEvent.Result.Output);
            WriteSummaryLine($"Read {lines} lines ({tokens} tokens)");
        }
        else
        {
            var message = string.IsNullOrWhiteSpace(toolEvent.Result.Error)
                ? "Read failed"
                : toolEvent.Result.Error;
            WriteSummaryLine(message, ConsoleColor.Red);
        }
    }

    private void RenderEditSummary(ToolResultEvent toolEvent)
    {
        var path = TryGetString(toolEvent.ArgumentsJson, "file_path") ?? "unknown";
        WriteSummaryHeader($"Update({path})");

        if (!toolEvent.Result.Success)
        {
            var errorMessage = string.IsNullOrWhiteSpace(toolEvent.Result.Error)
                ? "Update failed"
                : toolEvent.Result.Error;
            WriteSummaryLine(errorMessage, ConsoleColor.Red);
            return;
        }

        var metadata = ParseEditMetadata(toolEvent.Result.Output);
        var count = metadata?.LinesModified ?? 0;
        WriteSummaryLine($"Update {count} lines");

        var changes = BuildFileChanges(metadata, path);
        if (changes.Count == 0)
            return;

        Console.WriteLine(" │");
        foreach (var change in changes)
        {
            if (change.Type == FileChangeType.Removed || change.Type == FileChangeType.Modified)
            {
                WriteSummaryLine($"- {change.OldLine}", ConsoleColor.Red);
            }

            if (change.Type == FileChangeType.Added || change.Type == FileChangeType.Modified)
            {
                WriteSummaryLine($"+ {change.NewLine}", ConsoleColor.Green);
            }
        }
    }

    private void RenderChangeDirectorySummary(ToolResultEvent toolEvent)
    {
        var path = TryGetString(toolEvent.ArgumentsJson, "path") ?? "";
        WriteSummaryHeader($"ChangeDirectory(path: \"{path}\")");

        if (toolEvent.Result.Success)
        {
            var destination = TryGetString(toolEvent.Result.Output, "current_directory") ?? "unknown";
            WriteSummaryLine($"Now at {destination}");
        }
        else
        {
            var message = string.IsNullOrWhiteSpace(toolEvent.Result.Error)
                ? "Directory change failed"
                : toolEvent.Result.Error;
            WriteSummaryLine(message, ConsoleColor.Red);
        }
    }

    private void RenderWebSearchSummary(ToolResultEvent toolEvent)
    {
        var query = TryGetString(toolEvent.ArgumentsJson, "query") ?? "";
        WriteSummaryHeader($"WebSearch(query: \"{query}\")");

        var message = string.IsNullOrWhiteSpace(toolEvent.Result.Error)
            ? "Web search is not available"
            : toolEvent.Result.Error;
        WriteSummaryLine(message, ConsoleColor.Red);
    }

    private void RenderTestSummary(ToolResultEvent toolEvent)
    {
        WriteSummaryHeader("Test()");
        var color = toolEvent.Result.Success ? ConsoleColor.Green : ConsoleColor.Red;
        var message = toolEvent.Result.Success
            ? toolEvent.Result.Output
            : string.IsNullOrWhiteSpace(toolEvent.Result.Error)
                ? "Test failed"
                : toolEvent.Result.Error;
        WriteSummaryBlock(message, color);
    }

    private void RenderGenericSummary(ToolResultEvent toolEvent)
    {
        WriteSummaryHeader($"{toolEvent.ToolName}()");
        var color = toolEvent.Result.Success ? (ConsoleColor?)null : ConsoleColor.Red;
        var message = toolEvent.Result.Success && !string.IsNullOrWhiteSpace(toolEvent.Result.Output)
            ? toolEvent.Result.Output
            : toolEvent.Result.Success
                ? "Completed"
                : string.IsNullOrWhiteSpace(toolEvent.Result.Error)
                    ? "Tool failed"
                    : toolEvent.Result.Error;
        WriteSummaryBlock(NormalizeOutput(message), color);
    }

    private EditResultMetadata? ParseEditMetadata(string output)
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

    private List<FileChange> BuildFileChanges(EditResultMetadata? metadata, string argumentPath)
    {
        var path = metadata?.FilePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = Path.IsPathRooted(argumentPath)
                ? argumentPath
                : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), argumentPath));
        }

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return new List<FileChange>();

        var previous = Array.Empty<string>();
        if (!string.IsNullOrWhiteSpace(metadata?.BackupPath) && File.Exists(metadata.BackupPath))
        {
            previous = File.ReadAllLines(metadata.BackupPath);
        }

        var current = File.ReadAllLines(path);
        return ComputeDiff(previous, current);
    }

    private List<FileChange> ComputeDiff(string[] oldLines, string[] newLines)
    {
        var changes = new List<FileChange>();
        var i = 0;
        var j = 0;

        while (i < oldLines.Length && j < newLines.Length)
        {
            if (oldLines[i] == newLines[j])
            {
                i++;
                j++;
                continue;
            }

            if (i + 1 < oldLines.Length && oldLines[i + 1] == newLines[j])
            {
                changes.Add(new FileChange
                {
                    Type = FileChangeType.Removed,
                    OldLine = oldLines[i]
                });
                i++;
                continue;
            }

            if (j + 1 < newLines.Length && oldLines[i] == newLines[j + 1])
            {
                changes.Add(new FileChange
                {
                    Type = FileChangeType.Added,
                    NewLine = newLines[j]
                });
                j++;
                continue;
            }

            changes.Add(new FileChange
            {
                Type = FileChangeType.Modified,
                OldLine = oldLines[i],
                NewLine = newLines[j]
            });
            i++;
            j++;
        }

        while (i < oldLines.Length)
        {
            changes.Add(new FileChange
            {
                Type = FileChangeType.Removed,
                OldLine = oldLines[i]
            });
            i++;
        }

        while (j < newLines.Length)
        {
            changes.Add(new FileChange
            {
                Type = FileChangeType.Added,
                NewLine = newLines[j]
            });
            j++;
        }

        return changes;
    }

    private void WriteSummaryHeader(string text)
    {
        Console.WriteLine($"● {text}");
    }

    private void WriteSummaryLine(string text, ConsoleColor? color = null)
    {
        if (color.HasValue)
        {
            var previous = Console.ForegroundColor;
            Console.ForegroundColor = color.Value;
            Console.WriteLine($"⎿ {text}");
            Console.ForegroundColor = previous;
        }
        else
        {
            Console.WriteLine($"⎿ {text}");
        }
    }

    private void WriteSummaryBlock(string text, ConsoleColor? color = null)
    {
        var lines = NormalizeOutput(text).Split('\n');
        foreach (var line in lines)
        {
            WriteSummaryLine(line, color);
        }
    }

    private string? TryGetString(string json, string propertyName)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(propertyName, out var prop))
            {
                return prop.GetString();
            }
        }
        catch
        {
        }

        return null;
    }

    private int? TryGetInt(string json, string propertyName)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(propertyName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var value))
                    return value;
            }
        }
        catch
        {
        }

        return null;
    }

    private string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;

        return value[..maxLength] + "...";
    }

    private string NormalizeOutput(string text)
    {
        return text.ReplaceLineEndings("\n").TrimEnd('\n');
    }

    private int EstimateTokenCount(string content)
    {
        if (string.IsNullOrEmpty(content))
            return 0;

        return Regex.Matches(content, @"\S+").Count;
    }

    private int CountLines(string content)
    {
        if (string.IsNullOrEmpty(content))
            return 0;

        return content.Split('\n').Length;
    }

    private sealed class EditResultMetadata
    {
        public string? FilePath { get; init; }
        public string? BackupPath { get; init; }
        public int LinesModified { get; init; }
    }

    private sealed class FileChange
    {
        public FileChangeType Type { get; init; }
        public string? OldLine { get; init; }
        public string? NewLine { get; init; }
    }

    private enum FileChangeType
    {
        Added,
        Removed,
        Modified
    }
}
