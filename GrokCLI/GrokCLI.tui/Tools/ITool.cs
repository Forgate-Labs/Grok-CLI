using System.Threading;
using GrokCLI.Models;
using OpenAI.Chat;

namespace GrokCLI.Tools;

public interface ITool
{
    string Name { get; }
    string Description { get; }
    ChatTool GetChatTool();
    Task<ToolExecutionResult> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken);
}
