using System.Threading;

namespace GrokCLI.Services;

/// <summary>
/// Result of a shell command execution
/// </summary>
public class ShellResult
{
    /// <summary>
    /// Indicates if the command was successful (exit code 0)
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The exit code from the process
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// Standard output from the command
    /// </summary>
    public string Output { get; set; } = "";

    /// <summary>
    /// Standard error from the command
    /// </summary>
    public string Error { get; set; } = "";

    /// <summary>
    /// The command that was executed
    /// </summary>
    public string Command { get; set; } = "";

    /// <summary>
    /// The platform/shell type that executed the command
    /// </summary>
    public string Platform { get; set; } = "";
}

/// <summary>
/// Service for executing shell commands with timeout and error handling
/// </summary>
public interface IShellExecutor
{
    /// <summary>
    /// Executes a shell command asynchronously
    /// </summary>
    /// <param name="command">The command to execute</param>
    /// <param name="timeoutSeconds">Maximum time to wait for command completion</param>
    /// <returns>The result of the command execution</returns>
    Task<ShellResult> ExecuteAsync(string command, int timeoutSeconds = 30, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a shell command in a specific working directory
    /// </summary>
    /// <param name="command">The command to execute</param>
    /// <param name="workingDirectory">The directory to execute the command in</param>
    /// <param name="timeoutSeconds">Maximum time to wait for command completion</param>
    /// <returns>The result of the command execution</returns>
    Task<ShellResult> ExecuteAsync(string command, string workingDirectory, int timeoutSeconds = 30, CancellationToken cancellationToken = default);
}
