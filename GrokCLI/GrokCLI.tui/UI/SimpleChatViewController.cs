using GrokCLI.Services;
using OpenAI.Chat;

namespace GrokCLI.UI;

public class SimpleChatViewController
{
    private readonly IChatService _chatService;
    private readonly List<ChatMessage> _conversation;
    private readonly SimpleTerminalUI _ui;
    private readonly bool _isEnabled;

    public SimpleChatViewController(
        IChatService chatService,
        SimpleTerminalUI ui,
        bool isEnabled)
    {
        _chatService = chatService;
        _conversation = new List<ChatMessage>();
        _ui = ui;
        _isEnabled = isEnabled;

        // Subscribe to ChatService events
        _chatService.OnTextReceived += OnTextReceived;
        _chatService.OnToolCalled += OnToolCalled;
        _chatService.OnToolResult += OnToolResult;
    }

    public async Task SendMessageAsync()
    {
        if (!_isEnabled) return;

        var userText = _ui.GetCurrentInput()?.Trim();
        if (string.IsNullOrWhiteSpace(userText)) return;

        // Hide input line during processing
        _ui.HideInputLine();

        // Display user message (WriteLine will not redraw input since it's hidden)
        Console.WriteLine($"[You]: {userText}");
        Console.Write("[Grok]: ");

        // Clear input
        _ui.ClearInput();

        // Show processing status
        _ui.SetProcessingStatus("thinking...");

        try
        {
            await _chatService.SendMessageAsync(userText, _conversation);
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
        }
        finally
        {
            _ui.SetProcessingStatus("");
            _ui.ShowInputLine();
        }
    }

    public void ClearChat()
    {
        if (!_isEnabled) return;
        Console.Clear();
        _conversation.Clear();
        ShowWelcomeMessage();
    }

    public void ShowWelcomeMessage()
    {
        _ui.WriteLine("Grok CLI - Agentic Mode");
        _ui.WriteLine("Commands: Ctrl+J (newline) | Ctrl+C (exit)");
        _ui.WriteLine("Model: grok-4-1-fast-reasoning");
        _ui.WriteLine("");
    }

    public void ShowSystemMessage(string message)
    {
        _ui.WriteLine(message);
    }

    private void OnTextReceived(string text)
    {
        Console.Write(text);
    }

    private void OnToolCalled(string toolName, string args)
    {
        if (string.IsNullOrEmpty(args))
        {
            // First notification - only the name
            Console.WriteLine($"\n🔧 [Tool: {toolName}]");
        }
        else
        {
            // Second notification - with arguments
            Console.WriteLine($"📋 Arguments:");
            Console.WriteLine(args);
        }
    }

    private void OnToolResult(string toolName, string result)
    {
        Console.WriteLine($"✅ Result:");
        Console.WriteLine(result);
        Console.WriteLine();
    }
}
