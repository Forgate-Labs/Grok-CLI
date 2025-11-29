using System.Text.Json;
using GrokCLI.Models;
using GrokCLI.Services;
using OpenAI.Chat;

namespace GrokCLI.Tools;

public class EditFileTool : ITool
{
    private readonly IFileEditService _fileEditService;

    public string Name => "edit_file";
    public string Description => "Edits text files with various operations: replace text, insert lines, append content, delete lines, or write entire file.";

    public EditFileTool(IFileEditService fileEditService)
    {
        _fileEditService = fileEditService;
    }

    public ChatTool GetChatTool()
    {
        return ChatTool.CreateFunctionTool(
            Name,
            Description,
            BinaryData.FromString(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""file_path"": {
                        ""type"": ""string"",
                        ""description"": ""Path to the file to edit (relative or absolute)""
                    },
                    ""operation"": {
                        ""type"": ""string"",
                        ""enum"": [""replace"", ""insert"", ""append"", ""delete"", ""write""],
                        ""description"": ""The edit operation to perform""
                    },
                    ""search_text"": {
                        ""type"": ""string"",
                        ""description"": ""Text to search for (required for 'replace' operation)""
                    },
                    ""replacement_text"": {
                        ""type"": ""string"",
                        ""description"": ""Text to replace with (required for 'replace' operation)""
                    },
                    ""content"": {
                        ""type"": ""string"",
                        ""description"": ""Content to insert/append/write (required for 'insert', 'append', 'write' operations)""
                    },
                    ""line_number"": {
                        ""type"": ""integer"",
                        ""description"": ""Line number for insert operation (1-based index)""
                    },
                    ""start_line"": {
                        ""type"": ""integer"",
                        ""description"": ""Start line for delete operation (1-based index)""
                    },
                    ""end_line"": {
                        ""type"": ""integer"",
                        ""description"": ""End line for delete operation (1-based index, defaults to start_line)""
                    },
                    ""create_backup"": {
                        ""type"": ""boolean"",
                        ""description"": ""Create backup before editing, used when requested (default: false)""
                    }
                },
                ""required"": [""file_path"", ""operation""]
            }")
        );
    }

    public async Task<ToolExecutionResult> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var jsonDoc = JsonDocument.Parse(argumentsJson);
            var root = jsonDoc.RootElement;

            var filePath = root.GetProperty("file_path").GetString() ?? "";
            var operation = root.GetProperty("operation").GetString() ?? "";
            var createBackup = root.TryGetProperty("create_backup", out var backupProp)
                ? backupProp.GetBoolean()
                : true;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return ToolExecutionResult.CreateError("File path cannot be empty");
            }

            FileEditResult result;

            switch (operation.ToLowerInvariant())
            {
                case "replace":
                    if (!root.TryGetProperty("search_text", out var searchProp) ||
                        !root.TryGetProperty("replacement_text", out var replacementProp))
                    {
                        return ToolExecutionResult.CreateError("'replace' operation requires 'search_text' and 'replacement_text' parameters");
                    }

                    var searchText = searchProp.GetString() ?? "";
                    var replacementText = replacementProp.GetString() ?? "";

                    result = await _fileEditService.ReplaceTextAsync(
                        filePath, searchText, replacementText, createBackup);
                    break;

                case "insert":
                    if (!root.TryGetProperty("content", out var insertContentProp) ||
                        !root.TryGetProperty("line_number", out var lineProp))
                    {
                        return ToolExecutionResult.CreateError("'insert' operation requires 'content' and 'line_number' parameters");
                    }

                    var insertContent = insertContentProp.GetString() ?? "";
                    var lineNumber = lineProp.GetInt32();

                    result = await _fileEditService.InsertTextAsync(
                        filePath, lineNumber, insertContent, createBackup);
                    break;

                case "append":
                    if (!root.TryGetProperty("content", out var appendContentProp))
                    {
                        return ToolExecutionResult.CreateError("'append' operation requires 'content' parameter");
                    }

                    var appendContent = appendContentProp.GetString() ?? "";

                    result = await _fileEditService.AppendTextAsync(
                        filePath, appendContent, createBackup);
                    break;

                case "delete":
                    if (!root.TryGetProperty("start_line", out var startLineProp))
                    {
                        return ToolExecutionResult.CreateError("'delete' operation requires 'start_line' parameter");
                    }

                    var startLine = startLineProp.GetInt32();
                    var endLine = root.TryGetProperty("end_line", out var endLineProp)
                        ? endLineProp.GetInt32()
                        : startLine;

                    result = await _fileEditService.DeleteLinesAsync(
                        filePath, startLine, endLine, createBackup);
                    break;

                case "write":
                    if (!root.TryGetProperty("content", out var writeContentProp))
                    {
                        return ToolExecutionResult.CreateError("'write' operation requires 'content' parameter");
                    }

                    var writeContent = writeContentProp.GetString() ?? "";

                    result = await _fileEditService.WriteFileAsync(
                        filePath, writeContent, createBackup);
                    break;

                default:
                    return ToolExecutionResult.CreateError(
                        $"Unknown operation: {operation}. Valid operations are: replace, insert, append, delete, write");
            }

            if (!result.Success)
            {
                return ToolExecutionResult.CreateError(result.Error ?? "Unknown error");
            }

            var response = JsonSerializer.Serialize(new
            {
                success = result.Success,
                message = result.Message,
                file_path = result.FilePath,
                lines_modified = result.LinesModified,
                backup_path = result.BackupPath
            });

            return ToolExecutionResult.CreateSuccess(response);
        }
        catch (JsonException ex)
        {
            return ToolExecutionResult.CreateError($"Invalid JSON: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ToolExecutionResult.CreateError($"Error executing file edit: {ex.Message}");
        }
    }
}
