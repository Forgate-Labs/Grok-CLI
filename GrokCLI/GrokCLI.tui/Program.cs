using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
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
services.AddSingleton<ITool, WebSearchTool>();
services.AddSingleton<ITool, LocalFileReadTool>();
services.AddSingleton<ITool, TestTool>();
services.AddSingleton<ITool, ChangeDirectoryTool>();
services.AddSingleton<ITool, EditFileTool>();
services.AddSingleton<ITool, SearchTool>();

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

var ui = new SimpleTerminalUI();
var chatService = hasApiKey
    ? serviceProvider.GetRequiredService<IChatService>()
    : new DisabledChatService();
var controller = new SimpleChatViewController(chatService, ui, hasApiKey);

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
