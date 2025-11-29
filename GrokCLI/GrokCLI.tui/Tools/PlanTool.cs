using System.Text.Json;
using System.Threading;
using GrokCLI.Models;
using OpenAI.Chat;

namespace GrokCLI.Tools;

public class PlanTool : ITool
{
    public string Name => "set_plan";
    public string Description => "Updates the current execution plan to show above the input prompt.";

    public ChatTool GetChatTool()
    {
        return ChatTool.CreateFunctionTool(
            Name,
            Description,
            BinaryData.FromString(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""title"": {
                        ""type"": ""string"",
                        ""description"": ""Headline for the plan block""
                    },
                    ""items"": {
                        ""type"": ""array"",
                        ""items"": {
                            ""type"": ""object"",
                            ""properties"": {
                                ""title"": { ""type"": ""string"", ""description"": ""Task title"" },
                                ""status"": { ""type"": ""string"", ""description"": ""Task status: pending|in_progress|done"" }
                            },
                            ""required"": [""title""]
                        }
                    }
                },
                ""required"": [""items""]
            }")
        );
    }

    public Task<ToolExecutionResult> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = JsonDocument.Parse(argumentsJson);
            return Task.FromResult(ToolExecutionResult.CreateSuccess("plan updated"));
        }
        catch (JsonException ex)
        {
            return Task.FromResult(ToolExecutionResult.CreateError($"Invalid JSON: {ex.Message}"));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolExecutionResult.CreateError($"Error applying plan: {ex.Message}"));
        }
    }
}
