using GrokCLI.Models;
using OpenAI.Chat;

namespace GrokCLI.Services;

public class DisabledChatService : IChatService
{
    public event Action<string>? OnTextReceived = delegate { };
    public event Action<ToolCallEvent>? OnToolCalled = delegate { };
    public event Action<ToolResultEvent>? OnToolResult = delegate { };

    public Task SendMessageAsync(string userMessage, List<ChatMessage> conversation)
    {
        return Task.CompletedTask;
    }
}
