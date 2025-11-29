using GrokCLI.Services;

namespace GrokCLI.Tests;

[TestFixture]
public class FileSystemHelperTests
{
    private IPlatformService _platformService = null!;
    private FileSystemHelper _fileSystemHelper = null!;

    [SetUp]
    public void Setup()
    {
        _platformService = new PlatformService();
        _fileSystemHelper = new FileSystemHelper(_platformService);
    }

    [Test]
    public void PathComparison_OnLinux_ShouldBeCaseSensitive()
    {
        // Act
        var comparison = _fileSystemHelper.PathComparison;

        // Assert
        if (_platformService.IsLinux)
        {
            Assert.That(comparison, Is.EqualTo(StringComparison.Ordinal));
        }
        else
        {
            Assert.That(comparison, Is.EqualTo(StringComparison.OrdinalIgnoreCase));
        }
    }

    [Test]
    public void PathsEqual_WithIdenticalPaths_ShouldReturnTrue()
    {
        // Arrange
        var path1 = Path.Combine(Path.GetTempPath(), "test.txt");
        var path2 = Path.Combine(Path.GetTempPath(), "test.txt");

        // Act
        var result = _fileSystemHelper.PathsEqual(path1, path2);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void PathsEqual_WithDifferentPaths_ShouldReturnFalse()
    {
        // Arrange
        var path1 = Path.Combine(Path.GetTempPath(), "test1.txt");
        var path2 = Path.Combine(Path.GetTempPath(), "test2.txt");

        // Act
        var result = _fileSystemHelper.PathsEqual(path1, path2);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void PathsEqual_WithNullOrEmpty_ShouldReturnFalse()
    {
        // Arrange & Act & Assert
        Assert.That(_fileSystemHelper.PathsEqual(null!, "test"), Is.False);
        Assert.That(_fileSystemHelper.PathsEqual("test", null!), Is.False);
        Assert.That(_fileSystemHelper.PathsEqual("", "test"), Is.False);
        Assert.That(_fileSystemHelper.PathsEqual("test", ""), Is.False);
    }

    [Test]
    public void IsAbsolutePath_WithAbsolutePath_ShouldReturnTrue()
    {
        // Arrange
        var path = Path.GetTempPath();

        // Act
        var result = _fileSystemHelper.IsAbsolutePath(path);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void IsAbsolutePath_WithRelativePath_ShouldReturnFalse()
    {
        // Arrange
        var path = "relative/path/to/file.txt";

        // Act
        var result = _fileSystemHelper.IsAbsolutePath(path);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsAbsolutePath_WithNullOrEmpty_ShouldReturnFalse()
    {
        // Arrange & Act & Assert
        Assert.That(_fileSystemHelper.IsAbsolutePath(null!), Is.False);
        Assert.That(_fileSystemHelper.IsAbsolutePath(""), Is.False);
        Assert.That(_fileSystemHelper.IsAbsolutePath("   "), Is.False);
    }

    [Test]
    public void EnsureDirectoryExists_WithNewDirectory_ShouldCreateIt()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            // Act
            var result = _fileSystemHelper.EnsureDirectoryExists(tempDir);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(Directory.Exists(tempDir), Is.True);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir);
            }
        }
    }

    [Test]
    public void EnsureDirectoryExists_WithExistingDirectory_ShouldReturnTrue()
    {
        // Arrange
        var tempDir = Path.GetTempPath();

        // Act
        var result = _fileSystemHelper.EnsureDirectoryExists(tempDir);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(Directory.Exists(tempDir), Is.True);
    }

    [Test]
    public void GetSafeFileName_WithValidFileName_ShouldReturnUnchanged()
    {
        // Arrange
        var fileName = "valid_filename.txt";

        // Act
        var result = _fileSystemHelper.GetSafeFileName(fileName);

        // Assert
        Assert.That(result, Is.EqualTo(fileName));
    }

    [Test]
    public void GetSafeFileName_WithInvalidCharacters_ShouldReplaceWithUnderscore()
    {
        // Arrange
        var invalidChars = Path.GetInvalidFileNameChars();
        var fileName = "invalid" + new string(invalidChars.Take(3).ToArray()) + "file.txt";

        // Act
        var result = _fileSystemHelper.GetSafeFileName(fileName);

        // Assert
        Assert.That(result, Is.Not.Null);

        // Verify that invalid characters were replaced
        foreach (var c in invalidChars.Take(3))
        {
            Assert.That(result.Contains(c), Is.False,
                $"Result should not contain invalid character: {(int)c}");
        }

        // Should contain replacement character
        Assert.That(result, Does.Contain("_"));
        Assert.That(result, Does.Contain("invalid"));
        Assert.That(result, Does.Contain("file.txt"));
    }

    [Test]
    public void GetSafeFileName_WithNullOrEmpty_ShouldReturnSameValue()
    {
        // Arrange & Act & Assert
        Assert.That(_fileSystemHelper.GetSafeFileName(null!), Is.Null);
        Assert.That(_fileSystemHelper.GetSafeFileName(""), Is.Empty);
    }

    [Test]
    public void CombinePath_WithMultipleParts_ShouldCombineCorrectly()
    {
        // Arrange
        var parts = new[] { "path", "to", "file.txt" };

        // Act
        var result = _fileSystemHelper.CombinePath(parts);

        // Assert
        Assert.That(result, Does.Contain("path"));
        Assert.That(result, Does.Contain("to"));
        Assert.That(result, Does.Contain("file.txt"));
        Assert.That(result, Is.EqualTo(Path.Combine(parts)));
    }

    [Test]
    public void GetRelativePath_WithValidPaths_ShouldReturnRelativePath()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var fromPath = tempDir;
        var toPath = Path.Combine(tempDir, "subdir", "file.txt");

        // Act
        var result = _fileSystemHelper.GetRelativePath(fromPath, toPath);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Contain("subdir"));
        Assert.That(result, Does.Contain("file.txt"));
    }

    [Test]
    public void GetRelativePath_WithSamePath_ShouldReturnDot()
    {
        // Arrange
        var tempDir = Path.GetTempPath();

        // Act
        var result = _fileSystemHelper.GetRelativePath(tempDir, tempDir);

        // Assert
        Assert.That(result, Is.EqualTo("."));
    }
}
