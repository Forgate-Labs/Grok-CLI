using GrokCLI.Services;

namespace GrokCLI.Tests;

[TestFixture]
public class LineEndingDetectorTests
{
    [Test]
    public void DetectLineEnding_WithCRLF_ShouldReturnCRLF()
    {
        // Arrange
        var content = "Line1\r\nLine2\r\nLine3";

        // Act
        var lineEnding = LineEndingDetector.DetectLineEnding(content);

        // Assert
        Assert.That(lineEnding, Is.EqualTo("\r\n"));
    }

    [Test]
    public void DetectLineEnding_WithLF_ShouldReturnLF()
    {
        // Arrange
        var content = "Line1\nLine2\nLine3";

        // Act
        var lineEnding = LineEndingDetector.DetectLineEnding(content);

        // Assert
        Assert.That(lineEnding, Is.EqualTo("\n"));
    }

    [Test]
    public void DetectLineEnding_WithMixedLineEndings_ShouldReturnCRLF()
    {
        // Arrange
        var content = "Line1\r\nLine2\nLine3";

        // Act
        var lineEnding = LineEndingDetector.DetectLineEnding(content);

        // Assert
        // CRLF takes precedence
        Assert.That(lineEnding, Is.EqualTo("\r\n"));
    }

    [Test]
    public void DetectLineEnding_WithNoLineEndings_ShouldReturnSystemDefault()
    {
        // Arrange
        var content = "Single line content";

        // Act
        var lineEnding = LineEndingDetector.DetectLineEnding(content);

        // Assert
        Assert.That(lineEnding, Is.EqualTo(Environment.NewLine));
    }

    [Test]
    public void DetectLineEnding_WithEmptyString_ShouldReturnSystemDefault()
    {
        // Arrange
        var content = "";

        // Act
        var lineEnding = LineEndingDetector.DetectLineEnding(content);

        // Assert
        Assert.That(lineEnding, Is.EqualTo(Environment.NewLine));
    }

    [Test]
    public void DetectLineEnding_WithNull_ShouldReturnSystemDefault()
    {
        // Arrange
        string? content = null;

        // Act
        var lineEnding = LineEndingDetector.DetectLineEnding(content!);

        // Assert
        Assert.That(lineEnding, Is.EqualTo(Environment.NewLine));
    }

    [Test]
    public void NormalizeLineEndings_FromCRLFToLF_ShouldConvert()
    {
        // Arrange
        var content = "Line1\r\nLine2\r\nLine3";
        var targetLineEnding = "\n";

        // Act
        var result = LineEndingDetector.NormalizeLineEndings(content, targetLineEnding);

        // Assert
        Assert.That(result, Is.EqualTo("Line1\nLine2\nLine3"));
        Assert.That(result, Does.Not.Contain("\r\n"));
    }

    [Test]
    public void NormalizeLineEndings_FromLFToCRLF_ShouldConvert()
    {
        // Arrange
        var content = "Line1\nLine2\nLine3";
        var targetLineEnding = "\r\n";

        // Act
        var result = LineEndingDetector.NormalizeLineEndings(content, targetLineEnding);

        // Assert
        Assert.That(result, Is.EqualTo("Line1\r\nLine2\r\nLine3"));
    }

    [Test]
    public void NormalizeLineEndings_WithMixedLineEndings_ShouldNormalizeAll()
    {
        // Arrange
        var content = "Line1\r\nLine2\nLine3\r\nLine4";
        var targetLineEnding = "\n";

        // Act
        var result = LineEndingDetector.NormalizeLineEndings(content, targetLineEnding);

        // Assert
        Assert.That(result, Is.EqualTo("Line1\nLine2\nLine3\nLine4"));
        Assert.That(result, Does.Not.Contain("\r"));
    }

    [Test]
    public void NormalizeLineEndings_WithEmptyString_ShouldReturnEmpty()
    {
        // Arrange
        var content = "";
        var targetLineEnding = "\n";

        // Act
        var result = LineEndingDetector.NormalizeLineEndings(content, targetLineEnding);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void NormalizeLineEndings_WithNull_ShouldReturnNull()
    {
        // Arrange
        string? content = null;
        var targetLineEnding = "\n";

        // Act
        var result = LineEndingDetector.NormalizeLineEndings(content!, targetLineEnding);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void GetLineEndingName_WithCRLF_ShouldReturnCRLF()
    {
        // Arrange
        var lineEnding = "\r\n";

        // Act
        var name = LineEndingDetector.GetLineEndingName(lineEnding);

        // Assert
        Assert.That(name, Is.EqualTo("CRLF"));
    }

    [Test]
    public void GetLineEndingName_WithLF_ShouldReturnLF()
    {
        // Arrange
        var lineEnding = "\n";

        // Act
        var name = LineEndingDetector.GetLineEndingName(lineEnding);

        // Assert
        Assert.That(name, Is.EqualTo("LF"));
    }

    [Test]
    public void GetLineEndingName_WithCR_ShouldReturnCR()
    {
        // Arrange
        var lineEnding = "\r";

        // Act
        var name = LineEndingDetector.GetLineEndingName(lineEnding);

        // Assert
        Assert.That(name, Is.EqualTo("CR"));
    }

    [Test]
    public void GetLineEndingName_WithUnknown_ShouldReturnUnknown()
    {
        // Arrange
        var lineEnding = "something else";

        // Act
        var name = LineEndingDetector.GetLineEndingName(lineEnding);

        // Assert
        Assert.That(name, Is.EqualTo("Unknown"));
    }
}
