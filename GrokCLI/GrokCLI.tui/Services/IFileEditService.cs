using GrokCLI.Models;

namespace GrokCLI.Services;

public interface IFileEditService
{
    Task<FileEditResult> ReplaceTextAsync(
        string filePath,
        string searchText,
        string replacementText,
        bool createBackup = true);

    Task<FileEditResult> InsertTextAsync(
        string filePath,
        int lineNumber,
        string content,
        bool createBackup = true);

    Task<FileEditResult> AppendTextAsync(
        string filePath,
        string content,
        bool createBackup = true);

    Task<FileEditResult> DeleteLinesAsync(
        string filePath,
        int startLine,
        int endLine,
        bool createBackup = true);

    Task<FileEditResult> WriteFileAsync(
        string filePath,
        string content,
        bool createBackup = true);

    Task<string> ReadFileAsync(string filePath);
}
