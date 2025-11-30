using System.Text;

namespace GrokCLI.UI;

public static class SummaryTextFormatter
{
    public static string Normalize(string text)
    {
        return text.ReplaceLineEndings("\n").TrimEnd('\n');
    }

    public static string BuildHeader(string text)
    {
        return $"\n● {text}\n";
    }

    public static string BuildLine(string text)
    {
        return $"⎿ {text}\n";
    }

    public static string BuildBlock(string text)
    {
        var builder = new StringBuilder();
        foreach (var line in SummarizeLines(Normalize(text).Split('\n')))
        {
            builder.Append(BuildLine(line));
        }

        return builder.ToString();
    }

    public static IEnumerable<string> SummarizeLines(string[] lines)
    {
        if (lines.Length <= 4)
            return lines;

        return new[]
        {
            lines[0],
            $"... +{lines.Length - 3} lines",
            lines[^2],
            lines[^1]
        };
    }

    public static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;

        return value[..maxLength] + "...";
    }
}
