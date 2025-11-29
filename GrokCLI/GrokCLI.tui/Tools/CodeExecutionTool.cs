using System.Diagnostics;
using System.Text.Json;
using GrokCLI.Models;
using OpenAI.Chat;
using System.Collections.Generic;

namespace GrokCLI.Tools;

public class CodeExecutionTool : ITool
{
    private readonly AppConfig _config;

    public CodeExecutionTool(AppConfig config)
    {
        _config = config;
    }

    public string Name => "code_execution";
    public string Description => "Executes Python code";

    public ChatTool GetChatTool()
    {
        return ChatTool.CreateFunctionTool(
            Name,
            Description,
            BinaryData.FromString(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""code"": {
                        ""type"": ""string"",
                        ""description"": ""The Python code to execute""
                    }
                },
                ""required"": [""code""]
            }")
        );
    }

    public async Task<ToolExecutionResult> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var jsonDoc = JsonDocument.Parse(argumentsJson);
            var code = jsonDoc.RootElement.GetProperty("code").GetString() ?? "";

            var commandLabel = $"python3 -c {code}";
            var permission = EvaluateCommandPermission(commandLabel);
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

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "python3",
                    Arguments = $"-c \"{code.Replace("\"", "\\\"")}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(true);
                }
                catch
                {
                }
            });

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (process.ExitCode != 0)
                return ToolExecutionResult.CreateError(error, output, process.ExitCode);

            return ToolExecutionResult.CreateSuccess(output, error, process.ExitCode);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ToolExecutionResult.CreateError($"Error executing code: {ex.Message}", exitCode: -1);
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
