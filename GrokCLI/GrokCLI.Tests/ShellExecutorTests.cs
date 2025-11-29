using GrokCLI.Services;

namespace GrokCLI.Tests;

[TestFixture]
public class ShellExecutorTests
{
    private IPlatformService _platformService = null!;
    private ShellExecutor _shellExecutor = null!;

    [SetUp]
    public void Setup()
    {
        _platformService = new PlatformService();
        _shellExecutor = new ShellExecutor(_platformService);
    }

    [Test]
    public async Task ExecuteAsync_WithSimpleCommand_ShouldReturnSuccess()
    {
        // Arrange
        var command = _platformService.IsWindows
            ? "Write-Output 'Hello World'"
            : "echo 'Hello World'";

        // Act
        var result = await _shellExecutor.ExecuteAsync(command);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(result.Output, Does.Contain("Hello World"));
        Assert.That(result.Command, Is.EqualTo(command));
        Assert.That(result.Platform, Is.Not.Empty);
    }

    [Test]
    public async Task ExecuteAsync_WithFailingCommand_ShouldReturnFailure()
    {
        // Arrange
        var command = _platformService.IsWindows
            ? "exit 1"
            : "exit 1";

        // Act
        var result = await _shellExecutor.ExecuteAsync(command);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.False);
        Assert.That(result.ExitCode, Is.EqualTo(1));
    }

    [Test]
    public async Task ExecuteAsync_WithWorkingDirectory_ShouldExecuteInCorrectDirectory()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var command = _platformService.IsWindows
            ? "Get-Location"
            : "pwd";

        // Act
        var result = await _shellExecutor.ExecuteAsync(command, tempDir);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Output.Trim(), Does.Contain(Path.GetFullPath(tempDir).TrimEnd(Path.DirectorySeparatorChar)));
    }

    [Test]
    public async Task ExecuteAsync_WithTimeout_ShouldTimeoutLongRunningCommand()
    {
        // Arrange
        var command = _platformService.IsWindows
            ? "Start-Sleep -Seconds 5"
            : "sleep 5";

        // Act
        var result = await _shellExecutor.ExecuteAsync(command, timeoutSeconds: 1);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.False);
        Assert.That(result.ExitCode, Is.EqualTo(-1));
        Assert.That(result.Error, Does.Contain("timed out"));
    }

    [Test]
    public async Task ExecuteAsync_WithInvalidCommand_ShouldReturnError()
    {
        // Arrange
        var command = "this-command-does-not-exist-12345";

        // Act
        var result = await _shellExecutor.ExecuteAsync(command);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.False);
        // The command might fail with exit code != 0 or error message
        Assert.That(result.ExitCode != 0 || !string.IsNullOrEmpty(result.Error), Is.True);
    }

    [Test]
    public async Task ExecuteAsync_ShouldCaptureStandardOutput()
    {
        // Arrange
        var testMessage = "Test Output Message";
        var command = _platformService.IsWindows
            ? $"Write-Output '{testMessage}'"
            : $"echo '{testMessage}'";

        // Act
        var result = await _shellExecutor.ExecuteAsync(command);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Output, Does.Contain(testMessage));
    }

    [Test]
    public async Task ExecuteAsync_ShouldCaptureStandardError()
    {
        // Arrange
        var testMessage = "Test Error Message";
        var command = _platformService.IsWindows
            ? $"Write-Error '{testMessage}'"
            : $"echo '{testMessage}' >&2";

        // Act
        var result = await _shellExecutor.ExecuteAsync(command);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Error, Does.Contain(testMessage));
    }

    [Test]
    public async Task ExecuteAsync_WithMultiLineOutput_ShouldCaptureAllLines()
    {
        // Arrange
        var command = _platformService.IsWindows
            ? "Write-Output 'Line1'; Write-Output 'Line2'; Write-Output 'Line3'"
            : "echo 'Line1'; echo 'Line2'; echo 'Line3'";

        // Act
        var result = await _shellExecutor.ExecuteAsync(command);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Output, Does.Contain("Line1"));
        Assert.That(result.Output, Does.Contain("Line2"));
        Assert.That(result.Output, Does.Contain("Line3"));
    }
}
