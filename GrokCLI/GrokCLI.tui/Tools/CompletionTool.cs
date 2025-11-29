using System.Text.Json;
using System.Threading;
using GrokCLI.Models;
using OpenAI.Chat;

namespace GrokCLI.Tools;

public class CompletionTool : ITool
{
    public string Name => "workflow_done";
    public string Description => "Signals that the assistant has finished.";

    public ChatTool GetChatTool()
    {
        return ChatTool.CreateFunctionTool(
            Name,
            Description,
            BinaryData.FromString(@"{
                ""type"": ""object"",
                ""properties"": {}
            }")
        );
    }

    public Task<ToolExecutionResult> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var payload = JsonSerializer.Serialize(new { status = "done" });
            if (!string.IsNullOrWhiteSpace(argumentsJson))
            {
                _ = JsonDocument.Parse(argumentsJson);
            }

            return Task.FromResult(ToolExecutionResult.CreateSuccess(payload));
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
            return Task.FromResult(ToolExecutionResult.CreateError($"Error finishing workflow: {ex.Message}"));
        }
    }
}
