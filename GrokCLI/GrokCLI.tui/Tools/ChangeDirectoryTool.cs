using System.Text.Json;
using System.Threading;
using GrokCLI.Models;
using GrokCLI.Services;
using OpenAI.Chat;

namespace GrokCLI.Tools;

public class ChangeDirectoryTool : ITool
{
    private readonly IWorkingDirectoryService _workingDirService;

    public string Name => "change_directory";
    public string Description => "Changes the current working directory. Supports relative paths, absolute paths, '..' for parent directory, and '~' for home directory.";

    public ChangeDirectoryTool(IWorkingDirectoryService workingDirService)
    {
        _workingDirService = workingDirService;
    }

    public ChatTool GetChatTool()
    {
        return ChatTool.CreateFunctionTool(
            Name,
            Description,
            BinaryData.FromString(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""path"": {
                        ""type"": ""string"",
                        ""description"": ""The target directory path (relative or absolute). Use '..' for parent directory, '~' for home directory.""
                    }
                },
                ""required"": [""path""]
            }")
        );
    }

    public Task<ToolExecutionResult> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var jsonDoc = JsonDocument.Parse(argumentsJson);
            var path = jsonDoc.RootElement.GetProperty("path").GetString() ?? "";

            if (string.IsNullOrWhiteSpace(path))
            {
                return Task.FromResult(ToolExecutionResult.CreateError("Path cannot be empty"));
            }

            var currentDir = _workingDirService.GetCurrentDirectory();

            var resolvedPath = _workingDirService.ResolveRelativePath(path);

            if (!_workingDirService.DirectoryExists(path))
            {
                return Task.FromResult(ToolExecutionResult.CreateError(
                    $"Directory not found: {resolvedPath}",
                    $"Current directory: {currentDir}"
                ));
            }

            _workingDirService.SetCurrentDirectory(path);
            var newDir = _workingDirService.GetCurrentDirectory();

            var result = JsonSerializer.Serialize(new
            {
                success = true,
                previous_directory = currentDir,
                current_directory = newDir,
                message = $"Changed directory to {newDir}"
            });

            return Task.FromResult(ToolExecutionResult.CreateSuccess(result));
        }
        catch (DirectoryNotFoundException ex)
        {
            return Task.FromResult(ToolExecutionResult.CreateError(ex.Message));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolExecutionResult.CreateError(
                $"Error changing directory: {ex.Message}"
            ));
        }
    }
}
