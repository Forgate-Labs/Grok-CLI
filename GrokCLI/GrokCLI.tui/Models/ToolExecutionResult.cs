namespace GrokCLI.Models;

public class ToolExecutionResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;

    public static ToolExecutionResult CreateSuccess(string output)
    {
        return new ToolExecutionResult
        {
            Success = true,
            Output = output
        };
    }

    public static ToolExecutionResult CreateError(string error)
    {
        return new ToolExecutionResult
        {
            Success = false,
            Error = error
        };
    }
}
