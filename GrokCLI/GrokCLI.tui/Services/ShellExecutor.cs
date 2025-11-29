using System.Diagnostics;

namespace GrokCLI.Services;

public class ShellExecutor : IShellExecutor
{
    private readonly IPlatformService _platformService;

    public ShellExecutor(IPlatformService platformService)
    {
        _platformService = platformService;
    }

    public async Task<ShellResult> ExecuteAsync(
        string command,
        int timeoutSeconds = 30,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(command, Directory.GetCurrentDirectory(), timeoutSeconds, cancellationToken);
    }

    public async Task<ShellResult> ExecuteAsync(
        string command,
        string workingDirectory,
        int timeoutSeconds = 30,
        CancellationToken cancellationToken = default)
    {
        var processInfo = _platformService.CreateShellProcess(command);
        processInfo.WorkingDirectory = workingDirectory;

        var result = new ShellResult
        {
            Command = command,
            Platform = _platformService.ShellType
        };

        try
        {
            using var process = new Process { StartInfo = processInfo };

            process.Start();

            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(true);
                }
                catch
                {
                }
            });

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            var timeout = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
            var processTask = process.WaitForExitAsync();

            if (await Task.WhenAny(processTask, timeout) == timeout)
            {
                try
                {
                    process.Kill(true);
                }
                catch
                {
                }

                result.Error = $"Command timed out after {timeoutSeconds} seconds";
                result.ExitCode = -1;
                result.Success = false;
                return result;
            }

            cancellationToken.ThrowIfCancellationRequested();

            result.Output = await outputTask;
            result.Error = await errorTask;
            result.ExitCode = process.ExitCode;
            result.Success = process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
                throw;

            result.Error = $"Error executing command: {ex.Message}";
            result.ExitCode = -1;
            result.Success = false;
        }

        return result;
    }
}
