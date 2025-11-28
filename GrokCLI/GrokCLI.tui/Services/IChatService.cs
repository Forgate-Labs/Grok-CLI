using OpenAI.Chat;

namespace GrokCLI.Services;

public interface IChatService
{
    event Action<string>? OnTextReceived;
    event Action<string, string>? OnToolCalled;
    event Action<string, string>? OnToolResult;

    Task SendMessageAsync(string userMessage, List<ChatMessage> conversation);
}
