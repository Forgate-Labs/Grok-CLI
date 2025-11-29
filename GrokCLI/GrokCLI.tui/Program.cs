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
    controller.ShowSystemMessage("Press Ctrl+C to exit.");
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

        // Debug: Log key info to understand what's being pressed
        var debugInfo = $"Key={key.Key}, KeyChar={(int)key.KeyChar}, Modifiers={key.Modifiers}";

        // Handle special keys
        // Ctrl+J produces KeyChar = '\n' (10) and Key = ConsoleKey.Enter on some terminals
        // We need to check if Control modifier is present
        if (key.Key == ConsoleKey.Enter && (key.Modifiers & ConsoleModifiers.Control) != 0)
        {
            // Ctrl+Enter or Ctrl+J: Insert newline in input
            ui.InsertNewline();
        }
        else if (key.Key == ConsoleKey.Enter && hasApiKey)
        {
            // Enter: Check for special commands or send message
            var userInput = ui.GetCurrentInput()?.Trim();

            if (!string.IsNullOrWhiteSpace(userInput) && (userInput.Equals("clear", StringComparison.OrdinalIgnoreCase) || userInput.Equals("/clear", StringComparison.OrdinalIgnoreCase)))
            {
                // Execute clear command without sending to Grok (same as /cmd clear)
                var command = "clear";

                _ = Task.Run(async () =>
                {
                    ui.HideInputLine();
                    Console.WriteLine($"[You]: {userInput}");

                    ui.ClearInput();
                    ui.SetProcessingStatus("executing...");

                    var shellExecutor = serviceProvider.GetRequiredService<IShellExecutor>();
                    var result = await shellExecutor.ExecuteAsync(command, 300); // 5 minute timeout

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
            else if (!string.IsNullOrWhiteSpace(userInput) && userInput.StartsWith("/cmd "))
            {
                // Execute direct terminal command
                var command = userInput.Substring(5).Trim(); // Remove "/cmd "

                if (!string.IsNullOrWhiteSpace(command))
                {
                    _ = Task.Run(async () =>
                    {
                        ui.HideInputLine();
                        Console.WriteLine($"[You]: {userInput}");

                        ui.ClearInput();
                        ui.SetProcessingStatus("executing...");

                        var shellExecutor = serviceProvider.GetRequiredService<IShellExecutor>();
                        var result = await shellExecutor.ExecuteAsync(command, 300); // 5 minute timeout

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
                else
                {
                    ui.ClearInput();
                }
            }
            else
            {
                // Send message to Grok
                _ = Task.Run(async () =>
                {
                    await controller.SendMessageAsync();
                });
            }
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
