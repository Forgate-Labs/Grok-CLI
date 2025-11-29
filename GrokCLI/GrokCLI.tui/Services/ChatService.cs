using System.Text;
using System.Text.Json;
using GrokCLI.Models;
using GrokCLI.Tools;
using OpenAI.Chat;

namespace GrokCLI.Services;

public class ChatService : IChatService
{
    private readonly IGrokClient _grokClient;
    private readonly IToolExecutor _toolExecutor;
    private readonly IEnumerable<ITool> _tools;
    private readonly string? _prePrompt;

    public event Action<string>? OnTextReceived;
    public event Action<ToolCallEvent>? OnToolCalled;
    public event Action<ToolResultEvent>? OnToolResult;

    public ChatService(IGrokClient grokClient, IToolExecutor toolExecutor, IEnumerable<ITool> tools, string? prePrompt = null)
    {
        _grokClient = grokClient;
        _toolExecutor = toolExecutor;
        _tools = tools;
        _prePrompt = prePrompt;
    }

    public async Task SendMessageAsync(string userMessage, List<ChatMessage> conversation)
    {
        if (!string.IsNullOrWhiteSpace(_prePrompt) && conversation.Count == 0)
        {
            conversation.Add(new SystemChatMessage(_prePrompt));
        }

        conversation.Add(new UserChatMessage(userMessage));

        var options = new ChatCompletionOptions
        {
            ToolChoice = ChatToolChoice.CreateAutoChoice()
        };

        foreach (var tool in _tools)
        {
            options.Tools.Add(tool.GetChatTool());
        }

        bool continueLoop = true;
        while (continueLoop)
        {
            var assistantBuffer = new StringBuilder();
            char? pendingHighSurrogate = null;
            var toolCallsInfo = new Dictionary<int, ToolCallInfo>();

            var completionUpdates = _grokClient.StreamChatAsync(conversation, options);

            await foreach (var update in completionUpdates)
            {
                if (update.ToolCallUpdates != null && update.ToolCallUpdates.Count > 0)
                {
                    foreach (var toolUpdate in update.ToolCallUpdates)
                    {
                        var toolIndex = toolUpdate.Index;

                        if (!toolCallsInfo.ContainsKey(toolIndex))
                        {
                            toolCallsInfo[toolIndex] = new ToolCallInfo(
                                toolUpdate.ToolCallId ?? "",
                                toolUpdate.FunctionName ?? ""
                            );
                        }

                        if (!string.IsNullOrEmpty(toolUpdate.ToolCallId))
                            toolCallsInfo[toolIndex].Id = toolUpdate.ToolCallId;

                        if (!string.IsNullOrEmpty(toolUpdate.FunctionName))
                            toolCallsInfo[toolIndex].Name = toolUpdate.FunctionName;

                        if (toolUpdate.FunctionArgumentsUpdate != null)
                        {
                            toolCallsInfo[toolIndex].Arguments.Append(toolUpdate.FunctionArgumentsUpdate.ToString());
                        }
                    }
                }

                if (update.ContentUpdate.Count > 0)
                {
                    var text = update.ContentUpdate[0].Text;
                    var processed = ProcessChunk(text, ref pendingHighSurrogate);
                    assistantBuffer.Append(processed);
                    OnTextReceived?.Invoke(processed);
                }
            }

            if (pendingHighSurrogate.HasValue)
            {
                assistantBuffer.Append('\uFFFD');
                pendingHighSurrogate = null;
            }

            if (toolCallsInfo.Count > 0)
            {
                var assistantMsg = new AssistantChatMessage(assistantBuffer.ToString());
                foreach (var kvp in toolCallsInfo)
                {
                    var toolInfo = kvp.Value;
                    assistantMsg.ToolCalls.Add(ChatToolCall.CreateFunctionToolCall(
                        toolInfo.Id,
                        toolInfo.Name,
                        BinaryData.FromString(toolInfo.Arguments.ToString())
                    ));
                }
                conversation.Add(assistantMsg);

                foreach (var kvp in toolCallsInfo)
                {
                    var toolInfo = kvp.Value;
                    var argsJson = toolInfo.Arguments.ToString();

                    OnToolCalled?.Invoke(new ToolCallEvent(
                        toolInfo.Name,
                        toolInfo.Id,
                        argsJson));

                    var result = await _toolExecutor.ExecuteAsync(toolInfo.Name, argsJson);
                    var resultPayload = result.ToModelPayload();
                    OnToolResult?.Invoke(new ToolResultEvent(
                        toolInfo.Name,
                        toolInfo.Id,
                        argsJson,
                        result));

                    conversation.Add(new ToolChatMessage(toolInfo.Id, resultPayload));
                }

                continueLoop = true;
            }
            else
            {
                conversation.Add(new AssistantChatMessage(assistantBuffer.ToString()));
                continueLoop = false;
            }
        }

        static string ProcessChunk(string chunk, ref char? pendingHighSurrogate)
        {
            var builder = new StringBuilder();
            var index = 0;

            if (pendingHighSurrogate.HasValue)
            {
                var high = pendingHighSurrogate.Value;
                pendingHighSurrogate = null;

                if (chunk.Length > 0 && char.IsLowSurrogate(chunk[0]))
                {
                    builder.Append(high);
                    builder.Append(chunk[0]);
                    index = 1;
                }
                else
                {
                    builder.Append('\uFFFD');
                }
            }

            while (index < chunk.Length)
            {
                var current = chunk[index];

                if (char.IsHighSurrogate(current))
                {
                    if (index + 1 < chunk.Length && char.IsLowSurrogate(chunk[index + 1]))
                    {
                        builder.Append(current);
                        builder.Append(chunk[index + 1]);
                        index += 2;
                        continue;
                    }

                    if (index + 1 == chunk.Length)
                    {
                        pendingHighSurrogate = current;
                        index++;
                        continue;
                    }

                    builder.Append('\uFFFD');
                    index++;
                    continue;
                }

                if (char.IsLowSurrogate(current))
                {
                    builder.Append('\uFFFD');
                    index++;
                    continue;
                }

                builder.Append(current);
                index++;
            }

            return builder.ToString();
        }
    }
}
