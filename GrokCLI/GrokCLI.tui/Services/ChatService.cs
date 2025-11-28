using System.Text;
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
                    assistantBuffer.Append(text);
                    OnTextReceived?.Invoke(text);
                }
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
                    OnToolResult?.Invoke(toolInfo.Name, resultText);

                    // Add result to conversation
                    conversation.Add(new ToolChatMessage(toolInfo.Id, resultText));
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
    }
}
