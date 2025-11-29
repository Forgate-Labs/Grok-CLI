namespace GrokCLI.Services;

/// <summary>
/// Helper class for detecting and normalizing line endings
/// </summary>
public static class LineEndingDetector
{
    /// <summary>
    /// Detects the line ending used in the content
    /// </summary>
    /// <param name="content">The content to analyze</param>
    /// <returns>The detected line ending (CRLF, LF, or system default)</returns>
    public static string DetectLineEnding(string content)
    {
        if (string.IsNullOrEmpty(content))
            return Environment.NewLine;

        if (content.Contains("\r\n"))
            return "\r\n";
        if (content.Contains("\n"))
            return "\n";

        return Environment.NewLine;
    }

    /// <summary>
    /// Normalizes line endings in the content to the target line ending
    /// </summary>
    /// <param name="content">The content to normalize</param>
    /// <param name="targetLineEnding">The target line ending</param>
    /// <returns>Content with normalized line endings</returns>
    public static string NormalizeLineEndings(string content, string targetLineEnding)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        // First normalize to LF only, then replace with target
        return content.Replace("\r\n", "\n").Replace("\n", targetLineEnding);
    }

    /// <summary>
    /// Gets the line ending name for display
    /// </summary>
    /// <param name="lineEnding">The line ending to identify</param>
    /// <returns>Human-readable name (CRLF, LF, or Unknown)</returns>
    public static string GetLineEndingName(string lineEnding)
    {
        return lineEnding switch
        {
            "\r\n" => "CRLF",
            "\n" => "LF",
            "\r" => "CR",
            _ => "Unknown"
        };
    }
}
