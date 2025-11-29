using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GrokCLI.Models;
using GrokCLI.Tools;
using OpenAI.Chat;

namespace GrokCLI.Services;

public class ChatService : IChatService
{
    private readonly IGrokClient _grokClient;
    private readonly IToolExecutor _toolExecutor;
    private readonly IEnumerable<ITool> _tools;

    public event Action<string>? OnTextReceived;
    public event Action<string, string>? OnToolCalled;
    public event Action<string, string>? OnToolResult;

    public ChatService(IGrokClient grokClient, IToolExecutor toolExecutor, IEnumerable<ITool> tools)
    {
        _grokClient = grokClient;
        _toolExecutor = toolExecutor;
        _tools = tools;
    }

    public async Task SendMessageAsync(string userMessage, List<ChatMessage> conversation)
    {
        conversation.Add(new UserChatMessage(userMessage));

        var options = new ChatCompletionOptions
        {
            ToolChoice = ChatToolChoice.CreateAutoChoice()
        };

        // Add all available tools
        foreach (var tool in _tools)
        {
            options.Tools.Add(tool.GetChatTool());
        }

        // Agentic loop: continue until there are no more tool calls
        bool continueLoop = true;
        while (continueLoop)
        {
            var assistantBuffer = new StringBuilder();
            char? pendingHighSurrogate = null;
            var toolCallsInfo = new Dictionary<int, ToolCallInfo>();
            var displayedTools = new HashSet<int>();

            // Streaming
            var completionUpdates = _grokClient.StreamChatAsync(conversation, options);

            await foreach (var update in completionUpdates)
            {
                // Detect tool calls
                if (update.ToolCallUpdates != null && update.ToolCallUpdates.Count > 0)
                {
                    foreach (var toolUpdate in update.ToolCallUpdates)
                    {
                        var toolIndex = toolUpdate.Index;

                        // Initialize entry for this tool call
                        if (!toolCallsInfo.ContainsKey(toolIndex))
                        {
                            toolCallsInfo[toolIndex] = new ToolCallInfo(
                                toolUpdate.ToolCallId ?? "",
                                toolUpdate.FunctionName ?? ""
                            );
                        }

                        // Update information
                        if (!string.IsNullOrEmpty(toolUpdate.ToolCallId))
                            toolCallsInfo[toolIndex].Id = toolUpdate.ToolCallId;

                        if (!string.IsNullOrEmpty(toolUpdate.FunctionName))
                            toolCallsInfo[toolIndex].Name = toolUpdate.FunctionName;

                        // Notify UI about tool call (only once)
                        if (!string.IsNullOrEmpty(toolUpdate.FunctionName) && !displayedTools.Contains(toolIndex))
                        {
                            OnToolCalled?.Invoke(toolUpdate.FunctionName, "");
                            displayedTools.Add(toolIndex);
                        }

                        // Accumulate arguments
                        if (toolUpdate.FunctionArgumentsUpdate != null)
                        {
                            toolCallsInfo[toolIndex].Arguments.Append(toolUpdate.FunctionArgumentsUpdate.ToString());
                        }
                    }
                }

                // Stream text
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

            // If there are tool calls, execute and continue the loop
            if (toolCallsInfo.Count > 0)
            {
                // Add assistant message with tool calls to the conversation
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

                // Execute each tool call
                foreach (var kvp in toolCallsInfo)
                {
                    var toolInfo = kvp.Value;
                    var argsJson = toolInfo.Arguments.ToString();

                    // Notify about arguments
                    OnToolCalled?.Invoke(toolInfo.Name, argsJson);

                    // Execute tool
                    var result = await _toolExecutor.ExecuteAsync(toolInfo.Name, argsJson);

                    var resultText = result.Success ? result.Output : result.Error;
                    var displayText = resultText;
                    if (result.Success && toolInfo.Name == "read_local_file")
                    {
                        var path = ExtractPath(argsJson) ?? "unknown";
                        var tokenCount = EstimateTokenCount(result.Output);
                        displayText = $"{path} ({tokenCount} tokens)";
                    }
                    var resultPayload = result.ToModelPayload();
                    OnToolResult?.Invoke(toolInfo.Name, displayText);

                    // Add result to conversation
                    conversation.Add(new ToolChatMessage(toolInfo.Id, resultPayload));
                }

                // Continue loop to process results
                continueLoop = true;
            }
            else
            {
                // No tool calls, save final response and exit loop
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

        static string? ExtractPath(string argsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(argsJson);
                if (doc.RootElement.TryGetProperty("path", out var pathProp))
                    return pathProp.GetString();
            }
            catch
            {
            }

            return null;
        }

        static int EstimateTokenCount(string content)
        {
            if (string.IsNullOrEmpty(content))
                return 0;

            return Regex.Matches(content, @"\S+").Count;
        }
    }
}
