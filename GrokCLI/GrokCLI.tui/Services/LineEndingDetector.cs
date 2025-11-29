namespace GrokCLI.Services;

public static class LineEndingDetector
{
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

    public static string NormalizeLineEndings(string content, string targetLineEnding)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        return content.Replace("\r\n", "\n").Replace("\n", targetLineEnding);
    }

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
