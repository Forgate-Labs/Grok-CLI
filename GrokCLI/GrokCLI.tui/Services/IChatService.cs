using System.Threading;
using GrokCLI.Models;
using OpenAI.Chat;

namespace GrokCLI.Services;

public interface IChatService
{
    event Action<string>? OnTextReceived;
    event Action<ToolCallEvent>? OnToolCalled;
    event Action<ToolResultEvent>? OnToolResult;

    Task SendMessageAsync(string userMessage, List<ChatMessage> conversation, CancellationToken cancellationToken);
}
