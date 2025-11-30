using System.Text.Json;
using System.Threading;
using GrokCLI.Models;
using OpenAI.Chat;

namespace GrokCLI.Tools;

public class ReasoningTool : ITool
{
    public string Name => "share_reasoning";
    public string Description => "Shares private reasoning or chain-of-thought content without exposing it in the main reply.";

    public ChatTool GetChatTool()
    {
        return ChatTool.CreateFunctionTool(
            Name,
            Description,
            BinaryData.FromString(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""text"": { ""type"": ""string"", ""description"": ""Reasoning text to display in the Thinking block"" }
                },
                ""required"": [""text""]
            }")
        );
    }

    public Task<ToolExecutionResult> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var doc = JsonDocument.Parse(argumentsJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("text", out var textProp))
                return Task.FromResult(ToolExecutionResult.CreateError("Missing text"));

            var text = textProp.GetString() ?? "";

            return Task.FromResult(ToolExecutionResult.CreateSuccess(text));
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
            return Task.FromResult(ToolExecutionResult.CreateError($"Error recording reasoning: {ex.Message}"));
        }
    }
}
