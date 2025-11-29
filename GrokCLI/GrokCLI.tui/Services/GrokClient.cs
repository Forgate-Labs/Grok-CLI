using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace GrokCLI.Services;

public class GrokClient : IGrokClient
{
    private readonly ChatClient _chatClient;

    public GrokClient(string apiKey, string model = "grok-4-1-fast-reasoning")
    {
        _chatClient = new ChatClient(
            model: model,
            credential: new ApiKeyCredential(apiKey),
            options: new OpenAIClientOptions { Endpoint = new Uri("https://api.x.ai/v1") }
        );
    }

    public IAsyncEnumerable<StreamingChatCompletionUpdate> StreamChatAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions options,
        CancellationToken cancellationToken = default)
    {
        return _chatClient.CompleteChatStreamingAsync(messages, options, cancellationToken);
    }
}
