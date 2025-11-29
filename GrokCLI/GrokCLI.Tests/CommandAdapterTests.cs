using GrokCLI.Services;

namespace GrokCLI.Tests;

[TestFixture]
public class CommandAdapterTests
{
    private IPlatformService _platformService = null!;
    private CommandAdapter _commandAdapter = null!;

    [SetUp]
    public void Setup()
    {
        _platformService = new PlatformService();
        _commandAdapter = new CommandAdapter(_platformService);
    }

    [Test]
    public void ListDirectory_ShouldReturnPlatformSpecificCommand()
    {
        // Arrange
        var path = "/test/path";

        // Act
        var command = _commandAdapter.ListDirectory(path);

        // Assert
        Assert.That(command, Is.Not.Null);
        Assert.That(command, Is.Not.Empty);

        if (_platformService.IsWindows)
        {
            Assert.That(command, Does.Contain("Get-ChildItem"));
            Assert.That(command, Does.Contain(path));
        }
        else
        {
            Assert.That(command, Does.Contain("ls"));
            Assert.That(command, Does.Contain(path));
        }
    }

    [Test]
    public void ChangeDirectory_ShouldReturnPlatformSpecificCommand()
    {
        // Arrange
        var path = "/test/path";

        // Act
        var command = _commandAdapter.ChangeDirectory(path);

        // Assert
        Assert.That(command, Is.Not.Null);
        Assert.That(command, Does.Contain(path));

        if (_platformService.IsWindows)
        {
            Assert.That(command, Does.Contain("Set-Location"));
        }
        else
        {
            Assert.That(command, Does.Contain("cd"));
        }
    }

    [Test]
    public void ReadFile_ShouldReturnPlatformSpecificCommand()
    {
        // Arrange
        var path = "/test/file.txt";

        // Act
        var command = _commandAdapter.ReadFile(path);

        // Assert
        Assert.That(command, Is.Not.Null);
        Assert.That(command, Does.Contain(path));

        if (_platformService.IsWindows)
        {
            Assert.That(command, Does.Contain("Get-Content"));
            Assert.That(command, Does.Contain("UTF8"));
        }
        else
        {
            Assert.That(command, Does.Contain("cat"));
        }
    }

    [Test]
    public void WriteFile_ShouldReturnPlatformSpecificCommand()
    {
        // Arrange
        var path = "/test/file.txt";
        var content = "Test content";

        // Act
        var command = _commandAdapter.WriteFile(path, content);

        // Assert
        Assert.That(command, Is.Not.Null);
        Assert.That(command, Does.Contain(path));

        if (_platformService.IsWindows)
        {
            Assert.That(command, Does.Contain("Set-Content"));
            Assert.That(command, Does.Contain("UTF8"));
        }
        else
        {
            Assert.That(command, Does.Contain("echo"));
        }
    }

    [Test]
    public void WriteFile_ShouldEscapeSingleQuotes()
    {
        // Arrange
        var path = "/test/file.txt";
        var content = "Test's content with 'quotes'";

        // Act
        var command = _commandAdapter.WriteFile(path, content);

        // Assert
        Assert.That(command, Is.Not.Null);
        // Single quotes should be escaped
        Assert.That(command, Does.Contain("''"));
    }

    [Test]
    public void DeleteFile_ShouldReturnPlatformSpecificCommand()
    {
        // Arrange
        var path = "/test/file.txt";

        // Act
        var command = _commandAdapter.DeleteFile(path);

        // Assert
        Assert.That(command, Is.Not.Null);
        Assert.That(command, Does.Contain(path));

        if (_platformService.IsWindows)
        {
            Assert.That(command, Does.Contain("Remove-Item"));
            Assert.That(command, Does.Contain("-Force"));
        }
        else
        {
            Assert.That(command, Does.Contain("rm"));
            Assert.That(command, Does.Contain("-f"));
        }
    }

    [Test]
    public void CopyFile_ShouldReturnPlatformSpecificCommand()
    {
        // Arrange
        var source = "/test/source.txt";
        var destination = "/test/dest.txt";

        // Act
        var command = _commandAdapter.CopyFile(source, destination);

        // Assert
        Assert.That(command, Is.Not.Null);
        Assert.That(command, Does.Contain(source));
        Assert.That(command, Does.Contain(destination));

        if (_platformService.IsWindows)
        {
            Assert.That(command, Does.Contain("Copy-Item"));
        }
        else
        {
            Assert.That(command, Does.Contain("cp"));
        }
    }

    [Test]
    public void MoveFile_ShouldReturnPlatformSpecificCommand()
    {
        // Arrange
        var source = "/test/source.txt";
        var destination = "/test/dest.txt";

        // Act
        var command = _commandAdapter.MoveFile(source, destination);

        // Assert
        Assert.That(command, Is.Not.Null);
        Assert.That(command, Does.Contain(source));
        Assert.That(command, Does.Contain(destination));

        if (_platformService.IsWindows)
        {
            Assert.That(command, Does.Contain("Move-Item"));
        }
        else
        {
            Assert.That(command, Does.Contain("mv"));
        }
    }

    [Test]
    public void CreateDirectory_ShouldReturnPlatformSpecificCommand()
    {
        // Arrange
        var path = "/test/newdir";

        // Act
        var command = _commandAdapter.CreateDirectory(path);

        // Assert
        Assert.That(command, Is.Not.Null);
        Assert.That(command, Does.Contain(path));

        if (_platformService.IsWindows)
        {
            Assert.That(command, Does.Contain("New-Item"));
            Assert.That(command, Does.Contain("Directory"));
        }
        else
        {
            Assert.That(command, Does.Contain("mkdir"));
            Assert.That(command, Does.Contain("-p"));
        }
    }

    [Test]
    public void FindFiles_ShouldReturnPlatformSpecificCommand()
    {
        // Arrange
        var pattern = "*.txt";
        var path = "/test";

        // Act
        var command = _commandAdapter.FindFiles(pattern, path);

        // Assert
        Assert.That(command, Is.Not.Null);
        Assert.That(command, Does.Contain(pattern));
        Assert.That(command, Does.Contain(path));

        if (_platformService.IsWindows)
        {
            Assert.That(command, Does.Contain("Get-ChildItem"));
            Assert.That(command, Does.Contain("-Recurse"));
        }
        else
        {
            Assert.That(command, Does.Contain("find"));
            Assert.That(command, Does.Contain("-name"));
        }
    }

    [Test]
    public void SearchInFiles_ShouldReturnPlatformSpecificCommand()
    {
        // Arrange
        var pattern = "search term";
        var path = "/test";

        // Act
        var command = _commandAdapter.SearchInFiles(pattern, path);

        // Assert
        Assert.That(command, Is.Not.Null);
        Assert.That(command, Does.Contain(pattern));
        Assert.That(command, Does.Contain(path));

        if (_platformService.IsWindows)
        {
            Assert.That(command, Does.Contain("Select-String"));
            Assert.That(command, Does.Contain("Get-ChildItem"));
        }
        else
        {
            Assert.That(command, Does.Contain("grep"));
            Assert.That(command, Does.Contain("-r"));
        }
    }
}
