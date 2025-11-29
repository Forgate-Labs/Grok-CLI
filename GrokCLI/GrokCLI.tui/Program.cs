using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using GrokCLI.Models;
using GrokCLI.Services;
using GrokCLI.Tools;
using GrokCLI.UI;
using GrokCLI.tui.Tools;

var services = new ServiceCollection();

services.AddSingleton<IPlatformService, PlatformService>();
services.AddSingleton<ICommandAdapter, CommandAdapter>();
services.AddSingleton<IShellExecutor, ShellExecutor>();
services.AddSingleton<FileSystemHelper>();
services.AddSingleton<IWorkingDirectoryService, WorkingDirectoryService>();
services.AddSingleton<IFileEditService, FileEditService>();
services.AddSingleton<ISearchService, SearchService>();

services.AddSingleton<ITool, CodeExecutionTool>();
services.AddSingleton<ITool, CommandExecutionTool>();
services.AddSingleton<ITool, CompletionTool>();
services.AddSingleton<ITool, WebSearchTool>();
services.AddSingleton<ITool, LocalFileReadTool>();
services.AddSingleton<ITool, TestTool>();
services.AddSingleton<ITool, ChangeDirectoryTool>();
services.AddSingleton<ITool, EditFileTool>();
services.AddSingleton<ITool, SearchTool>();

var appConfig = LoadConfig() ?? new AppConfig
{
    ConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "grok.config.json")
};
services.AddSingleton(appConfig);
var displayMode = ResolveDisplayMode(args);

Console.OutputEncoding = new UTF8Encoding(false);
Console.InputEncoding = new UTF8Encoding(false);

var apiKey = Environment.GetEnvironmentVariable("XAI_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
#if DEBUG
    EnsureConfigInWorkingDirectory();
#endif
    apiKey = appConfig?.XaiApiKey;
}
var hasApiKey = !string.IsNullOrWhiteSpace(apiKey);

if (hasApiKey)
{
    services.AddSingleton<IGrokClient>(sp => new GrokClient(apiKey!));
    services.AddSingleton<IToolExecutor, ToolExecutor>();
    services.AddSingleton<IChatService>(sp =>
    {
        var config = sp.GetService<AppConfig>();
        var prePrompt = config?.PrePrompt;
        return new ChatService(
            sp.GetRequiredService<IGrokClient>(),
            sp.GetRequiredService<IToolExecutor>(),
            sp.GetRequiredService<IEnumerable<ITool>>(),
            prePrompt);
    });
}

var serviceProvider = services.BuildServiceProvider();

var ui = new SimpleTerminalUI();
var chatService = hasApiKey
    ? serviceProvider.GetRequiredService<IChatService>()
    : new DisabledChatService();
var controller = new SimpleChatViewController(chatService, ui, hasApiKey, displayMode);

if (!hasApiKey)
{
    controller.ShowSystemMessage("XAI_API_KEY is not set. Set the environment variable or configure grok.config.json and restart.");
    controller.ShowSystemMessage("Press Ctrl+C to exit.");
    controller.ShowSystemMessage("");
}
else
{
    controller.ShowWelcomeMessage();
}

ui.UpdateInputLine();

while (ui.IsRunning)
{
    if (Console.KeyAvailable)
    {
        var key = Console.ReadKey(true);

        if (key.Key == ConsoleKey.Enter && (key.Modifiers & ConsoleModifiers.Control) != 0)
        {
            ui.InsertNewline();
        }
        else if (key.Key == ConsoleKey.Enter && hasApiKey)
        {
            var userInput = ui.GetCurrentInput()?.Trim();

            if (!string.IsNullOrWhiteSpace(userInput) && (userInput.Equals("clear", StringComparison.OrdinalIgnoreCase) || userInput.Equals("/clear", StringComparison.OrdinalIgnoreCase)))
            {
                var command = "clear";

                _ = Task.Run(async () =>
                {
                    ui.HideInputLine();
                    Console.WriteLine($"[You]: {userInput}");

                    ui.ClearInput();
                    ui.SetProcessingStatus("executing...");

                    var shellExecutor = serviceProvider.GetRequiredService<IShellExecutor>();
                    var result = await shellExecutor.ExecuteAsync(command, 300);

                    ui.SetProcessingStatus("");

                    Console.WriteLine($"\n💻 [Command]: {command}");
                    Console.WriteLine($"📋 [Exit Code]: {result.ExitCode}");

                    if (!string.IsNullOrEmpty(result.Output))
                    {
                        Console.WriteLine($"\n✅ [Output]:");
                        Console.WriteLine(result.Output);
                    }

                    if (!string.IsNullOrEmpty(result.Error))
                    {
                        Console.WriteLine($"\n❌ [Error]:");
                        Console.WriteLine(result.Error);
                    }

                    Console.WriteLine();
                    ui.ShowInputLine();
                });
            }
            else if (!string.IsNullOrWhiteSpace(userInput) && (userInput.StartsWith("/cmd ") || userInput.StartsWith("cmd ")))
            {
                var command = userInput.StartsWith("/cmd ")
                    ? userInput.Substring(5).Trim()
                    : userInput.Substring(4).Trim();

                if (!string.IsNullOrWhiteSpace(command))
                {
                    _ = Task.Run(async () =>
                    {
                        ui.HideInputLine();
                        Console.WriteLine($"[You]: {userInput}");

                        ui.ClearInput();
                        ui.SetProcessingStatus("executing...");

                        var shellExecutor = serviceProvider.GetRequiredService<IShellExecutor>();
                        var result = await shellExecutor.ExecuteAsync(command, 300);

                        ui.SetProcessingStatus("");

                        if (displayMode == ChatDisplayMode.Debug)
                        {
                            Console.WriteLine($"\n💻 [Command]: {command}");
                            Console.WriteLine($"📋 [Exit Code]: {result.ExitCode}");

                            if (!string.IsNullOrEmpty(result.Output))
                            {
                                Console.WriteLine($"\n✅ [Output]:");
                                Console.WriteLine(result.Output);
                            }

                            if (!string.IsNullOrEmpty(result.Error))
                            {
                                Console.WriteLine($"\n❌ [Error]:");
                                Console.WriteLine(result.Error);
                            }
                        }
                        else
                        {
                            Console.WriteLine($"\n● Run({command})");

                            var success = result.ExitCode == 0;
                            var body = success
                                ? (!string.IsNullOrWhiteSpace(result.Output)
                                    ? result.Output
                                    : "Completed with no output")
                                : (!string.IsNullOrWhiteSpace(result.Error)
                                    ? result.Error
                                    : (!string.IsNullOrWhiteSpace(result.Output)
                                        ? result.Output
                                        : "Command failed"));

                            var lines = body.ReplaceLineEndings("\n").Split('\n');
                            var color = success ? ConsoleColor.Green : ConsoleColor.Red;
                            var previous = Console.ForegroundColor;
                            Console.ForegroundColor = color;
                            foreach (var line in lines)
                            {
                                Console.WriteLine($"⎿ {line}");
                            }
                            Console.ForegroundColor = previous;
                        }

                        Console.WriteLine();
                        ui.ShowInputLine();
                    });
                }
                else
                {
                    ui.ClearInput();
                }
            }
            else if (!string.IsNullOrWhiteSpace(userInput) &&
                (userInput.Equals("debug", StringComparison.OrdinalIgnoreCase) ||
                 userInput.Equals("normal", StringComparison.OrdinalIgnoreCase)))
            {
                var targetMode = userInput.Equals("debug", StringComparison.OrdinalIgnoreCase)
                    ? ChatDisplayMode.Debug
                    : ChatDisplayMode.Normal;

                ui.HideInputLine();
                Console.WriteLine($"[You]: {userInput}");
                ui.ClearInput();

                displayMode = targetMode;
                controller.SetDisplayMode(targetMode);

                ui.ShowInputLine();
            }
            else
            {
                _ = Task.Run(async () =>
                {
                    await controller.SendMessageAsync();
                });
            }
        }
        else
        {
            ui.HandleInput(key);
        }
    }
    else
    {
        Thread.Sleep(10);
    }
}

Console.WriteLine();
Console.CursorVisible = true;

ChatDisplayMode ResolveDisplayMode(string[] args)
{
    var modeArg = args.FirstOrDefault(a =>
        a.StartsWith("--mode=", StringComparison.OrdinalIgnoreCase));
    if (modeArg != null)
    {
        var value = modeArg.Substring("--mode=".Length);
        return ParseDisplayMode(value);
    }

    if (args.Any(a => a.Equals("--debug", StringComparison.OrdinalIgnoreCase)))
        return ChatDisplayMode.Debug;

    if (args.Any(a => a.Equals("--normal", StringComparison.OrdinalIgnoreCase)))
        return ChatDisplayMode.Normal;

    var env = Environment.GetEnvironmentVariable("GROK_MODE");
    if (!string.IsNullOrWhiteSpace(env))
        return ParseDisplayMode(env);

    return ChatDisplayMode.Normal;
}

ChatDisplayMode ParseDisplayMode(string? value)
{
    if (string.Equals(value, "debug", StringComparison.OrdinalIgnoreCase))
        return ChatDisplayMode.Debug;

    if (string.Equals(value, "normal", StringComparison.OrdinalIgnoreCase))
        return ChatDisplayMode.Normal;

    return ChatDisplayMode.Normal;
}

AppConfig? LoadConfig()
{
    var configPath = Path.Combine(Directory.GetCurrentDirectory(), "grok.config.json");
    if (!File.Exists(configPath))
        return CreateDefaultConfig(configPath);

    var json = File.ReadAllText(configPath);
    if (string.IsNullOrWhiteSpace(json))
        return CreateDefaultConfig(configPath);

    try
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var config = new AppConfig
        {
            ConfigPath = configPath
        };

        if (root.TryGetProperty("XAI_API_KEY", out var keyProp))
        {
            var value = keyProp.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                config.XaiApiKey = value;
        }

        if (root.TryGetProperty("pre_prompt", out var prePromptProp))
        {
            var value = prePromptProp.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                config.PrePrompt = value;
        }
        else if (root.TryGetProperty("pre-prompt", out var prePromptHyphenProp))
        {
            var value = prePromptHyphenProp.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                config.PrePrompt = value;
        }

        if (root.TryGetProperty("blocked_commands", out var blockedProp) &&
            blockedProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in blockedProp.EnumerateArray())
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    config.BlockedCommands.Add(value);
            }
        }
        else if (root.TryGetProperty("blocked-commands", out var blockedHyphenProp) &&
            blockedHyphenProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in blockedHyphenProp.EnumerateArray())
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    config.BlockedCommands.Add(value);
            }
        }

        if (root.TryGetProperty("allowed_commands", out var allowedProp) &&
            allowedProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in allowedProp.EnumerateArray())
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    config.AllowedCommands.Add(value);
            }
        }
        else if (root.TryGetProperty("allowed-commands", out var allowedHyphenProp) &&
            allowedHyphenProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in allowedHyphenProp.EnumerateArray())
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    config.AllowedCommands.Add(value);
            }
        }

        return config;
    }
    catch
    {
    }

    return CreateDefaultConfig(configPath);
}

void EnsureConfigInWorkingDirectory()
{
    var workingDir = Directory.GetCurrentDirectory();
    var fileName = "grok.config.json";
    var sourcePath = FindConfigInAncestors(workingDir, fileName);
    if (sourcePath == null)
        return;

    var destinationPath = Path.Combine(workingDir, fileName);
    if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
        return;

    try
    {
        File.Copy(sourcePath, destinationPath, true);
    }
    catch
    {
    }
}

string? FindConfigInAncestors(string startPath, string fileName)
{
    var directory = new DirectoryInfo(startPath);
    while (directory != null)
    {
        var candidate = Path.Combine(directory.FullName, fileName);
        if (File.Exists(candidate))
            return candidate;

        directory = directory.Parent;
    }

    return null;
}

AppConfig CreateDefaultConfig(string configPath)
{
    var config = new AppConfig
    {
        ConfigPath = configPath,
        AllowedCommands = new List<string>(),
        BlockedCommands = GetDefaultBlockedCommands()
    };

    SaveConfig(config);
    return config;
}

void SaveConfig(AppConfig config)
{
    if (string.IsNullOrWhiteSpace(config.ConfigPath))
        return;

    var payload = new Dictionary<string, object?>();
    payload["XAI_API_KEY"] = config.XaiApiKey ?? "";

    if (!string.IsNullOrWhiteSpace(config.PrePrompt))
        payload["pre_prompt"] = config.PrePrompt;

    payload["allowed_commands"] = config.AllowedCommands ?? new List<string>();
    payload["blocked_commands"] = config.BlockedCommands ?? new List<string>();

    var options = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    var json = JsonSerializer.Serialize(payload, options);
    File.WriteAllText(config.ConfigPath, json);
}

List<string> GetDefaultBlockedCommands()
{
    return new List<string>
    {
        "rm -rf /",
        "rm -rf .*",
        "dd ",
        "mkfs",
        "shutdown",
        "reboot",
        "halt",
        "poweroff",
        "format",
        "del /s /q",
        "remove-item -recurse -force",
        "stop-computer",
        "restart-computer"
    };
}
