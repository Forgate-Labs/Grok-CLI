using System.Text.Json;
using GrokCLI.Models;
using GrokCLI.Services;
using OpenAI.Chat;

namespace GrokCLI.Tools;

public class CommandExecutionTool : ITool
{
    private readonly IShellExecutor _shellExecutor;
    private readonly IWorkingDirectoryService _workingDirService;

    public string Name => "run_command";
    public string Description => "Executes CLI commands (e.g., build/test tools) in the working directory using the system shell.";

    public CommandExecutionTool(
        IShellExecutor shellExecutor,
        IWorkingDirectoryService workingDirService)
    {
        _shellExecutor = shellExecutor;
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
                    ""command"": {
                        ""type"": ""string"",
                        ""description"": ""The command to execute (e.g., 'dotnet build')""
                    },
                    ""working_directory"": {
                        ""type"": ""string"",
                        ""description"": ""Optional path to run the command in (relative to the current working directory)""
                    },
                    ""timeout_seconds"": {
                        ""type"": ""integer"",
                        ""description"": ""Maximum time to allow the command to run (default: 300 seconds)""
                    }
                },
                ""required"": [""command""]
            }")
        );
    }

    public async Task<ToolExecutionResult> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var jsonDoc = JsonDocument.Parse(argumentsJson);
            var root = jsonDoc.RootElement;

            var command = root.GetProperty("command").GetString() ?? "";

            if (string.IsNullOrWhiteSpace(command))
            {
                return ToolExecutionResult.CreateError("Command cannot be empty");
            }

            var workingDirectory = root.TryGetProperty("working_directory", out var pathProp)
                ? pathProp.GetString()
                : null;

            var resolvedWorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                ? _workingDirService.GetCurrentDirectory()
                : _workingDirService.ResolveRelativePath(workingDirectory);

            if (!_workingDirService.DirectoryExists(resolvedWorkingDirectory))
            {
                return ToolExecutionResult.CreateError(
                    $"Working directory not found: {resolvedWorkingDirectory}");
            }

            var timeoutSeconds = 300;
            if (root.TryGetProperty("timeout_seconds", out var timeoutProp) &&
                timeoutProp.ValueKind == JsonValueKind.Number &&
                timeoutProp.TryGetInt32(out var parsedTimeout) &&
                parsedTimeout > 0)
            {
                timeoutSeconds = parsedTimeout;
            }

            var result = await _shellExecutor.ExecuteAsync(
                command,
                resolvedWorkingDirectory,
                timeoutSeconds);

            if (result.Success)
            {
                return ToolExecutionResult.CreateSuccess(
                    result.Output,
                    result.Error,
                    result.ExitCode);
            }

            var errorMessage = !string.IsNullOrWhiteSpace(result.Error)
                ? result.Error
                : "Command failed";

            return ToolExecutionResult.CreateError(
                errorMessage,
                result.Output,
                result.ExitCode);
        }
        catch (JsonException ex)
        {
            return ToolExecutionResult.CreateError($"Invalid JSON: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ToolExecutionResult.CreateError($"Error executing command: {ex.Message}");
        }
    }
}
