using OpenAI.Chat;

namespace GrokCLI.Services;

public class DisabledChatService : IChatService
{
    public event Action<string>? OnTextReceived = delegate { };
    public event Action<string, string>? OnToolCalled = delegate { };
    public event Action<string, string>? OnToolResult = delegate { };

    public Task SendMessageAsync(string userMessage, List<ChatMessage> conversation)
    {
        return Task.CompletedTask;
    }
}
