using GrokCLI.Models;

namespace GrokCLI.Services;

public interface IToolExecutor
{
    Task<ToolExecutionResult> ExecuteAsync(string toolName, string argumentsJson);
}
