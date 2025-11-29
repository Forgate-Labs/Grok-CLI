using GrokCLI.Models;
using GrokCLI.Tools;

namespace GrokCLI.Services;

public class ToolExecutor : IToolExecutor
{
    private readonly IEnumerable<ITool> _tools;

    public ToolExecutor(IEnumerable<ITool> tools)
    {
        _tools = tools;
    }

    public async Task<ToolExecutionResult> ExecuteAsync(
        string toolName,
        string argumentsJson,
        CancellationToken cancellationToken)
    {
        var tool = _tools.FirstOrDefault(t => t.Name == toolName);

        if (tool == null)
        {
            return ToolExecutionResult.CreateError($"Tool '{toolName}' not found");
        }

        return await tool.ExecuteAsync(argumentsJson, cancellationToken);
    }
}
