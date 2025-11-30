using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using GrokCLI.Models;
using GrokCLI.Services;
using GrokCLI.Tools;
using GrokCLI.UI;

const string DefaultPrePrompt = "You are an assistant using Grok CLI. Before executing tasks, call the `set_plan` tool with a title and checklist of the tasks you will perform, and update it whenever the plan changes. When you finish any step, call `set_plan` again to mark it complete. After you complete the user's request, always call the tool `workflow_done` to signal completion. Do not end a response without that tool call. If GROK.md is available, follow its instructions. When you want to share your reasoning, call the `share_reasoning` tool with that text instead of including it in the reply. Do not use emojis in responses.";

var services = new ServiceCollection();

services.AddSingleton<IPlatformService, PlatformService>();
services.AddSingleton<ICommandAdapter, CommandAdapter>();
services.AddSingleton<IShellExecutor, ShellExecutor>();
services.AddSingleton<FileSystemHelper>();
services.AddSingleton<IWorkingDirectoryService, WorkingDirectoryService>();
services.AddSingleton<IFileEditService, FileEditService>();
services.AddSingleton<ISearchService, SearchService>();
services.AddSingleton<IToolExecutor, ToolExecutor>();

services.AddSingleton<ITool, CodeExecutionTool>();
services.AddSingleton<ITool, CommandExecutionTool>();
services.AddSingleton<ITool, CompletionTool>();
services.AddSingleton<ITool, WebSearchTool>();
services.AddSingleton<ITool, LocalFileReadTool>();
services.AddSingleton<ITool, ChangeDirectoryTool>();
services.AddSingleton<ITool, EditFileTool>();
services.AddSingleton<ITool, SearchTool>();
services.AddSingleton<ITool, PlanTool>();
services.AddSingleton<ITool, ReasoningTool>();

var installDirectory = GetInstallDirectory();
var configPath = Path.Combine(installDirectory, "grok.config.json");

var appConfig = LoadConfig(configPath) ?? new AppConfig
{
    ConfigPath = configPath
};
services.AddSingleton(appConfig);
var displayMode = ResolveDisplayMode(args);

Console.OutputEncoding = new UTF8Encoding(false);
Console.InputEncoding = new UTF8Encoding(false);

var apiKey = Environment.GetEnvironmentVariable("XAI_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    apiKey = appConfig?.XaiApiKey;
}

bool HasApiKey() => !string.IsNullOrWhiteSpace(apiKey);

var serviceProvider = services.BuildServiceProvider();

var prePrompt = ComposePrePrompt(appConfig);
Func<string, IChatService> createChatService = key =>
    new ChatService(
        new GrokClient(key),
        serviceProvider.GetRequiredService<IToolExecutor>(),
        serviceProvider.GetRequiredService<IEnumerable<ITool>>(),
        prePrompt);

var chatService = HasApiKey()
    ? createChatService(apiKey!)
    : new DisabledChatService();

Action<string?> persistApiKey = key =>
{
    apiKey = PersistApiKeyIfProvided(appConfig, key);
};

var guiController = new TerminalGuiChatViewController(
    chatService,
    serviceProvider,
    HasApiKey(),
    displayMode,
    GetVersion(),
    configPath,
    createChatService,
    persistApiKey);

guiController.Run();

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

string GetInstallDirectory()
{
    var baseDir = AppContext.BaseDirectory;
    return string.IsNullOrWhiteSpace(baseDir)
        ? Directory.GetCurrentDirectory()
        : Path.GetFullPath(baseDir);
}

string GetVersion()
{
    var assembly = typeof(Program).Assembly;
    var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    if (!string.IsNullOrWhiteSpace(informational))
    {
        var plusIndex = informational.IndexOf('+');
        return plusIndex >= 0 ? informational[..plusIndex] : informational;
    }

    var version = assembly.GetName().Version;
    return version?.ToString(3) ?? "1.0.0";
}

string? PersistApiKeyIfProvided(AppConfig? config, string? key)
{
    if (string.IsNullOrWhiteSpace(key))
    {
        Environment.SetEnvironmentVariable("XAI_API_KEY", null);

        if (config != null)
        {
            config.XaiApiKey = null;
            SaveConfig(config);
        }

        return null;
    }

    if (config != null)
    {
        config.XaiApiKey = key;
        SaveConfig(config);
    }

    Environment.SetEnvironmentVariable("XAI_API_KEY", key);
    return key;
}

AppConfig? LoadConfig(string configPath)
{
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

string? ComposePrePrompt(AppConfig? config)
{
    var segments = new List<string> { DefaultPrePrompt };

    if (!string.IsNullOrWhiteSpace(config?.PrePrompt) &&
        !string.Equals(config.PrePrompt, DefaultPrePrompt, StringComparison.Ordinal))
    {
        segments.Add(config.PrePrompt);
    }

    var grokInstructions = LoadGrokInstructions();
    if (!string.IsNullOrWhiteSpace(grokInstructions))
        segments.Add(grokInstructions);

    return string.Join("\n\n", segments);
}

string? LoadGrokInstructions()
{
    var grokPath = FindConfigInAncestors(Directory.GetCurrentDirectory(), "GROK.md");
    if (string.IsNullOrWhiteSpace(grokPath))
        return null;

    try
    {
        var content = File.ReadAllText(grokPath);
        if (string.IsNullOrWhiteSpace(content))
            return "Follow the instructions in GROK.md.";

        return $"Follow the instructions from GROK.md below:\n{content}";
    }
    catch
    {
    }

    return "GROK.md is present but could not be read.";
}

AppConfig CreateDefaultConfig(string configPath)
{
    var config = new AppConfig
    {
        ConfigPath = configPath,
        AllowedCommands = new List<string>(),
        BlockedCommands = GetDefaultBlockedCommands(),
        PrePrompt = DefaultPrePrompt
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
