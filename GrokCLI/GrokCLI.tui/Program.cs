using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using GrokCLI.Services;
using GrokCLI.Tools;
using GrokCLI.UI;
using GrokCLI.tui.Tools;

var services = new ServiceCollection();

// Register cross-platform services
services.AddSingleton<IPlatformService, PlatformService>();
services.AddSingleton<ICommandAdapter, CommandAdapter>();
services.AddSingleton<IShellExecutor, ShellExecutor>();
services.AddSingleton<FileSystemHelper>();

// Register tools
services.AddSingleton<ITool, CodeExecutionTool>();
services.AddSingleton<ITool, WebSearchTool>();
services.AddSingleton<ITool, LocalFileReadTool>();
services.AddSingleton<ITool, TestTool>();

Console.OutputEncoding = new UTF8Encoding(false);
Console.InputEncoding = new UTF8Encoding(false);

var apiKey = Environment.GetEnvironmentVariable("XAI_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
#if DEBUG
    EnsureConfigInWorkingDirectory();
#endif
    apiKey = LoadApiKeyFromConfig();
}
var hasApiKey = !string.IsNullOrWhiteSpace(apiKey);

if (hasApiKey)
{
    services.AddSingleton<IGrokClient>(sp => new GrokClient(apiKey!));
    services.AddSingleton<IToolExecutor, ToolExecutor>();
    services.AddSingleton<IChatService, ChatService>();
}

var serviceProvider = services.BuildServiceProvider();

// Initialize simple UI
var ui = new SimpleTerminalUI();
var chatService = hasApiKey
    ? serviceProvider.GetRequiredService<IChatService>()
    : new DisabledChatService();
var controller = new SimpleChatViewController(chatService, ui, hasApiKey);

// Show initial message
if (!hasApiKey)
{
    controller.ShowSystemMessage("XAI_API_KEY is not set. Set the environment variable or configure grok.config.json and restart.");
    controller.ShowSystemMessage("Press Ctrl+Q to exit.");
    controller.ShowSystemMessage("");
}
else
{
    controller.ShowWelcomeMessage();
}

// Draw initial input line
ui.UpdateInputLine();

// Main input loop
while (ui.IsRunning)
{
    if (Console.KeyAvailable)
    {
        var key = Console.ReadKey(true);

        // Handle special keys
        if (key.Key == ConsoleKey.Q && (key.Modifiers & ConsoleModifiers.Control) != 0)
        {
            // Ctrl+Q: Exit
            ui.Stop();
            break;
        }
        else if (key.Key == ConsoleKey.L && (key.Modifiers & ConsoleModifiers.Control) != 0 && hasApiKey)
        {
            // Ctrl+L: Clear chat
            controller.ClearChat();
            ui.UpdateInputLine();
        }
        else if (key.Key == ConsoleKey.Enter && hasApiKey)
        {
            // Enter: Send message
            _ = Task.Run(async () =>
            {
                await controller.SendMessageAsync();
            });
        }
        else
        {
            // Regular input handling
            ui.HandleInput(key);
        }
    }
    else
    {
        // Small delay to avoid CPU spinning
        Thread.Sleep(10);
    }
}

// Clean exit
Console.WriteLine();
Console.CursorVisible = true;

string? LoadApiKeyFromConfig()
{
    var configPath = Path.Combine(Directory.GetCurrentDirectory(), "grok.config.json");
    if (!File.Exists(configPath))
        return null;

    var json = File.ReadAllText(configPath);
    if (string.IsNullOrWhiteSpace(json))
        return null;

    try
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("XAI_API_KEY", out var prop))
        {
            var value = prop.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }
    }
    catch
    {
    }

    return null;
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
