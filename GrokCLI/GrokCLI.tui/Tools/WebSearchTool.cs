using GrokCLI.Models;
using OpenAI.Chat;

namespace GrokCLI.Tools;

public class WebSearchTool : ITool
{
    public string Name => "web_search";
    public string Description => "Performs real-time web search";

    public ChatTool GetChatTool()
    {
        return ChatTool.CreateFunctionTool(
            Name,
            Description,
            BinaryData.FromString(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""query"": {
                        ""type"": ""string"",
                        ""description"": ""The search query""
                    }
                },
                ""required"": [""query""]
            }")
        );
    }

    public Task<ToolExecutionResult> ExecuteAsync(string argumentsJson)
    {
        // TODO: Implement real web search
        return Task.FromResult(
            ToolExecutionResult.CreateError("Web search not implemented yet (local only)")
        );
    }
}
