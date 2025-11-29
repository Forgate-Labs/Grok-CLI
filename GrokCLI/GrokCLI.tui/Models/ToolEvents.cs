namespace GrokCLI.Models;

public class ToolCallEvent
{
    public ToolCallEvent(string toolName, string toolCallId, string argumentsJson)
    {
        ToolName = toolName;
        ToolCallId = toolCallId;
        ArgumentsJson = argumentsJson;
    }

    public string ToolName { get; }
    public string ToolCallId { get; }
    public string ArgumentsJson { get; }
}

public class ToolResultEvent
{
    public ToolResultEvent(string toolName, string toolCallId, string argumentsJson, ToolExecutionResult result)
    {
        ToolName = toolName;
        ToolCallId = toolCallId;
        ArgumentsJson = argumentsJson;
        Result = result;
    }

    public string ToolName { get; }
    public string ToolCallId { get; }
    public string ArgumentsJson { get; }
    public ToolExecutionResult Result { get; }
}
