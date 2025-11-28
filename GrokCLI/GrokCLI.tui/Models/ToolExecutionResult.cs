using System.Text.Json;

namespace GrokCLI.Models;

public class ToolExecutionResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public int ExitCode { get; set; }

    public static ToolExecutionResult CreateSuccess(string output, string error = "", int exitCode = 0)
    {
        return new ToolExecutionResult
        {
            Success = true,
            Output = output,
            Error = error,
            ExitCode = exitCode
        };
    }

    public static ToolExecutionResult CreateError(string error, string output = "", int exitCode = 1)
    {
        return new ToolExecutionResult
        {
            Success = false,
            Output = output,
            Error = error,
            ExitCode = exitCode
        };
    }

    public string ToModelPayload()
    {
        return JsonSerializer.Serialize(new
        {
            success = Success,
            stdout = Output,
            stderr = Error,
            exitCode = ExitCode
        });
    }
}
