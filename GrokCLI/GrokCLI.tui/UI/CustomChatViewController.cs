using GrokCLI.Services;
using OpenAI.Chat;

namespace GrokCLI.UI;

public class CustomChatViewController
{
    private readonly IChatService _chatService;
    private readonly List<ChatMessage> _conversation;
    private readonly CustomTerminalUI _ui;
    private readonly bool _isEnabled;

    public CustomChatViewController(
        IChatService chatService,
        CustomTerminalUI ui,
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

        _ui.ClearInput();

        // Add user message to the UI
        _ui.AddChatMessage($"\n[You]: {userText}");
        _ui.AddChatMessage("[Grok]: ");

        // Show processing status
        _ui.SetProcessingStatus("thinking...");

        try
        {
            await _chatService.SendMessageAsync(userText, _conversation);
            _ui.AddChatMessage("");
        }
        catch (Exception ex)
        {
            _ui.AddChatMessage($"\nError: {ex.Message}");
        }
        finally
        {
            _ui.SetProcessingStatus("");
        }
    }

    public void ClearChat()
    {
        if (!_isEnabled) return;
        _ui.ClearChat();
        _conversation.Clear();
    }

    public void ShowSystemMessage(string message)
    {
        _ui.AddChatMessage(message);
    }

    private void OnTextReceived(string text)
    {
        _ui.AppendToLastMessage(text);
    }

    private void OnToolCalled(string toolName, string args)
    {
        if (string.IsNullOrEmpty(args))
        {
            // First notification - only the name
            _ui.AddChatMessage($"\n🔧 [Tool: {toolName}]");
        }
        else
        {
            // Second notification - with arguments
            _ui.AddChatMessage($"📋 Arguments:\n{args}");
        }
    }

    private void OnToolResult(string toolName, string result)
    {
        _ui.AddChatMessage($"✅ Result:\n{result}\n");
    }
}
