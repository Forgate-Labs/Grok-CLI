using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using GrokCLI.Services;
using GrokCLI.Tools;
using GrokCLI.UI;
using Terminal.Gui;

var services = new ServiceCollection();

services.AddSingleton<ITool, CodeExecutionTool>();
services.AddSingleton<ITool, WebSearchTool>();

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

// Initialize Terminal.Gui
Application.Init();
var top = Application.Top;

var window = new ChatWindow();

var chatService = hasApiKey
    ? serviceProvider.GetRequiredService<IChatService>()
    : new DisabledChatService();
var controller = new ChatViewController(chatService, window.ChatView, window.InputView, window.ProcessingLabel, hasApiKey);

window.Initialize(top, controller, !hasApiKey);

if (!hasApiKey)
{
    controller.ShowSystemMessage("XAI_API_KEY is not set. Set the environment variable or configure grok.config.json and restart. Press Ctrl+Q to exit.");
    window.InputView.CanFocus = false;
    window.InputView.ReadOnly = true;
}

Application.Run();
Application.Shutdown();

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
