using OpenAI.Chat;

namespace GrokCLI.Services;

public interface IGrokClient
{
    IAsyncEnumerable<StreamingChatCompletionUpdate> StreamChatAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions options,
        CancellationToken cancellationToken = default);
}
