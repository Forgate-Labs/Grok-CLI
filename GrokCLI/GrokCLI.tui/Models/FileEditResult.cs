namespace GrokCLI.Models;

public class FileEditResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int LinesModified { get; set; }
    public string? BackupPath { get; set; }
    public string? Error { get; set; }
}
