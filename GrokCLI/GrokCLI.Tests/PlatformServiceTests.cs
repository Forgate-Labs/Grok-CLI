using GrokCLI.Services;
using System.Runtime.InteropServices;

namespace GrokCLI.Tests;

[TestFixture]
public class PlatformServiceTests
{
    private PlatformService _platformService = null!;

    [SetUp]
    public void Setup()
    {
        _platformService = new PlatformService();
    }

    [Test]
    public void PlatformService_ShouldDetectCorrectPlatform()
    {
        // Arrange & Act
        var platform = _platformService.Platform;

        // Assert
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.That(platform, Is.EqualTo(PlatformType.Windows));
            Assert.That(_platformService.IsWindows, Is.True);
            Assert.That(_platformService.IsLinux, Is.False);
            Assert.That(_platformService.IsMacOS, Is.False);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Assert.That(platform, Is.EqualTo(PlatformType.Linux));
            Assert.That(_platformService.IsWindows, Is.False);
            Assert.That(_platformService.IsLinux, Is.True);
            Assert.That(_platformService.IsMacOS, Is.False);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Assert.That(platform, Is.EqualTo(PlatformType.MacOS));
            Assert.That(_platformService.IsWindows, Is.False);
            Assert.That(_platformService.IsLinux, Is.False);
            Assert.That(_platformService.IsMacOS, Is.True);
        }
    }

    [Test]
    public void ShellType_ShouldBeCorrectForPlatform()
    {
        // Arrange & Act
        var shellType = _platformService.ShellType;

        // Assert
        if (_platformService.IsWindows)
        {
            Assert.That(shellType, Is.EqualTo("PowerShell"));
        }
        else if (_platformService.IsLinux)
        {
            Assert.That(shellType, Is.EqualTo("Bash"));
        }
        else if (_platformService.IsMacOS)
        {
            Assert.That(shellType, Is.EqualTo("Bash/Zsh"));
        }
    }

    [Test]
    public void LineEnding_ShouldBeCorrectForPlatform()
    {
        // Arrange & Act
        var lineEnding = _platformService.LineEnding;

        // Assert
        if (_platformService.IsWindows)
        {
            Assert.That(lineEnding, Is.EqualTo("\r\n"));
        }
        else
        {
            Assert.That(lineEnding, Is.EqualTo("\n"));
        }
    }

    [Test]
    public void PathSeparator_ShouldBeCorrectForPlatform()
    {
        // Arrange & Act
        var separator = _platformService.PathSeparator;

        // Assert
        Assert.That(separator, Is.EqualTo(Path.DirectorySeparatorChar.ToString()));
    }

    [Test]
    public void HomeDirectory_ShouldNotBeEmpty()
    {
        // Arrange & Act
        var homeDir = _platformService.HomeDirectory;

        // Assert
        Assert.That(homeDir, Is.Not.Null);
        Assert.That(homeDir, Is.Not.Empty);
        Assert.That(Directory.Exists(homeDir), Is.True);
    }

    [Test]
    public void NormalizePath_ShouldExpandTilde()
    {
        // Arrange
        var tildePathTest = "~/test";

        // Act
        var normalized = _platformService.NormalizePath(tildePathTest);

        // Assert
        Assert.That(normalized, Does.Not.StartWith("~"));
        Assert.That(normalized, Does.StartWith(_platformService.HomeDirectory));
    }

    [Test]
    public void NormalizePath_ShouldHandleNullOrEmpty()
    {
        // Arrange & Act & Assert
        Assert.That(_platformService.NormalizePath(null!), Is.Null);
        Assert.That(_platformService.NormalizePath(""), Is.Empty);
        Assert.That(_platformService.NormalizePath("   "), Is.EqualTo("   "));
    }

    [Test]
    public void CreateShellProcess_ShouldReturnCorrectConfiguration()
    {
        // Arrange
        var command = "echo test";

        // Act
        var processInfo = _platformService.CreateShellProcess(command);

        // Assert
        Assert.That(processInfo, Is.Not.Null);
        Assert.That(processInfo.RedirectStandardOutput, Is.True);
        Assert.That(processInfo.RedirectStandardError, Is.True);
        Assert.That(processInfo.UseShellExecute, Is.False);
        Assert.That(processInfo.CreateNoWindow, Is.True);

        if (_platformService.IsWindows)
        {
            Assert.That(processInfo.FileName, Is.EqualTo("powershell.exe"));
            Assert.That(processInfo.Arguments, Does.Contain("-NoProfile"));
            Assert.That(processInfo.Arguments, Does.Contain("-Command"));
        }
        else
        {
            Assert.That(processInfo.FileName, Is.EqualTo("/bin/bash"));
            Assert.That(processInfo.Arguments, Does.Contain("-c"));
        }
    }

    [Test]
    public void GetShellCommand_ShouldReturnCommand()
    {
        // Arrange
        var command = "test command";

        // Act
        var result = _platformService.GetShellCommand(command);

        // Assert
        Assert.That(result, Is.EqualTo(command));
    }
}
