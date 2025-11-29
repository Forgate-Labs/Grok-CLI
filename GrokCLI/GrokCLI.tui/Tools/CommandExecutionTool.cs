using System.Text.Json;
using GrokCLI.Models;
using GrokCLI.Services;
using OpenAI.Chat;

namespace GrokCLI.Tools;

public class CommandExecutionTool : ITool
{
    private readonly IShellExecutor _shellExecutor;
    private readonly IWorkingDirectoryService _workingDirService;
    private readonly AppConfig _config;

    public string Name => "run_command";
    public string Description => "Executes CLI commands (e.g., build/test tools) in the working directory using the system shell.";

    public CommandExecutionTool(
        IShellExecutor shellExecutor,
        IWorkingDirectoryService workingDirService,
        AppConfig config)
    {
        _shellExecutor = shellExecutor;
        _workingDirService = workingDirService;
        _config = config;
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

    public async Task<ToolExecutionResult> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var jsonDoc = JsonDocument.Parse(argumentsJson);
            var root = jsonDoc.RootElement;

            var command = root.GetProperty("command").GetString() ?? "";

            if (string.IsNullOrWhiteSpace(command))
            {
                return ToolExecutionResult.CreateError("Command cannot be empty");
            }

            var permission = EvaluateCommandPermission(command);
            if (permission == CommandPermission.Deny)
            {
                var reason = PromptReason();
                var message = string.IsNullOrWhiteSpace(reason)
                    ? "Command execution denied"
                    : $"Command execution denied: {reason}";
                return ToolExecutionResult.CreateError(message);
            }

            if (permission == CommandPermission.Never)
            {
                return ToolExecutionResult.CreateError("Command is blocked by user blocklist");
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
                timeoutSeconds,
                cancellationToken);

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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ToolExecutionResult.CreateError($"Error executing command: {ex.Message}");
        }
    }

    private CommandPermission EvaluateCommandPermission(string command)
    {
        if (_config.BlockedCommands != null && IsBlockedCommand(command))
            return CommandPermission.Never;

        if (_config.AllowedCommands == null || _config.AllowedCommands.Count == 0)
            return CommandPermission.AllowOnce;

        if (IsAllowedCommand(command))
            return CommandPermission.AllowOnce;

        return PromptApproval(command);
    }

    private bool IsAllowedCommand(string command)
    {
        foreach (var allowed in _config.AllowedCommands)
        {
            if (string.IsNullOrWhiteSpace(allowed))
                continue;

            if (command.StartsWith(allowed, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private CommandPermission PromptApproval(string command)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine($"Command not allowed: \"{command}\"");
            Console.WriteLine("Choose an option:");
            Console.WriteLine("1) Run once");
            Console.WriteLine("2) Always allow");
            Console.WriteLine("3) Do not run");
            Console.WriteLine("4) Never run this command");
            Console.Write("Selection: ");

            var input = Console.ReadLine()?.Trim();

            if (input == "1")
                return CommandPermission.AllowOnce;

            if (input == "2")
            {
                AddAllowedCommand(command);
                return CommandPermission.AllowAlways;
            }

            if (input == "3")
                return CommandPermission.Deny;

            if (input == "4")
            {
                AddBlockedCommand(command);
                return CommandPermission.Never;
            }
        }
    }

    private void AddAllowedCommand(string command)
    {
        if (_config.AllowedCommands == null)
            _config.AllowedCommands = new List<string>();

        if (!_config.AllowedCommands.Contains(command, StringComparer.OrdinalIgnoreCase))
        {
            _config.AllowedCommands.Add(command);
            PersistConfig();
        }
    }

    private void AddBlockedCommand(string command)
    {
        if (_config.BlockedCommands == null)
            _config.BlockedCommands = new List<string>();

        if (!_config.BlockedCommands.Contains(command, StringComparer.OrdinalIgnoreCase))
        {
            _config.BlockedCommands.Add(command);
            PersistConfig();
        }
    }

    private void PersistConfig()
    {
        if (string.IsNullOrWhiteSpace(_config.ConfigPath))
            return;

        var payload = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(_config.XaiApiKey))
            payload["XAI_API_KEY"] = _config.XaiApiKey;

        if (!string.IsNullOrWhiteSpace(_config.PrePrompt))
            payload["pre_prompt"] = _config.PrePrompt;

        if (_config.AllowedCommands != null && _config.AllowedCommands.Count > 0)
            payload["allowed_commands"] = _config.AllowedCommands;

        if (_config.BlockedCommands != null && _config.BlockedCommands.Count > 0)
            payload["blocked_commands"] = _config.BlockedCommands;

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(payload, options);
        File.WriteAllText(_config.ConfigPath!, json);
    }

    private string? PromptReason()
    {
        Console.Write("Reason for denial (optional): ");
        return Console.ReadLine()?.Trim();
    }

    private bool IsBlockedCommand(string command)
    {
        foreach (var blocked in _config.BlockedCommands)
        {
            if (string.IsNullOrWhiteSpace(blocked))
                continue;

            if (command.StartsWith(blocked, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private enum CommandPermission
    {
        AllowOnce,
        AllowAlways,
        Deny,
        Never
    }
}
