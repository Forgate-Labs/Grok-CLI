using System;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace GrokCLI.Services;

public class GrokClient : IGrokClient
{
    private readonly string _apiKey;
    private ChatClient _chatClient;
    private string _model;

    public GrokClient(string apiKey, string model = "grok-4-1-fast-reasoning")
    {
        _apiKey = apiKey;
        _model = model;
        _chatClient = CreateClient(_model);
    }

    public IAsyncEnumerable<StreamingChatCompletionUpdate> StreamChatAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions options,
        CancellationToken cancellationToken = default)
    {
        return _chatClient.CompleteChatStreamingAsync(messages, options, cancellationToken);
    }

    public void SetModel(string model)
    {
        if (string.Equals(_model, model, StringComparison.Ordinal))
            return;

        _model = model;
        _chatClient = CreateClient(_model);
    }

    private ChatClient CreateClient(string model)
    {
        return new ChatClient(
            model: model,
            credential: new ApiKeyCredential(_apiKey),
            options: new OpenAIClientOptions { Endpoint = new Uri("https://api.x.ai/v1") }
        );
    }
}
