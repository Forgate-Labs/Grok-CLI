using System.Text;
using GrokCLI.Models;

namespace GrokCLI.Services;

public class FileEditService : IFileEditService
{
    private readonly IWorkingDirectoryService _workingDirService;
    private readonly IPlatformService _platformService;
    private const int MaxFileSizeBytes = 10 * 1024 * 1024;

    public FileEditService(
        IWorkingDirectoryService workingDirService,
        IPlatformService platformService)
    {
        _workingDirService = workingDirService;
        _platformService = platformService;
    }

    public async Task<FileEditResult> ReplaceTextAsync(
        string filePath,
        string searchText,
        string replacementText,
        bool createBackup = true)
    {
        try
        {
            var resolvedPath = _workingDirService.ResolveRelativePath(filePath);

            if (!File.Exists(resolvedPath))
            {
                return new FileEditResult
                {
                    Success = false,
                    FilePath = resolvedPath,
                    Error = $"File not found: {resolvedPath}"
                };
            }

            if (!ValidateFileSize(resolvedPath))
            {
                return new FileEditResult
                {
                    Success = false,
                    FilePath = resolvedPath,
                    Error = $"File too large (max {MaxFileSizeBytes / (1024 * 1024)} MB)"
                };
            }

            var content = await File.ReadAllTextAsync(resolvedPath, Encoding.UTF8);

            if (!content.Contains(searchText))
            {
                return new FileEditResult
                {
                    Success = false,
                    FilePath = resolvedPath,
                    Error = "Search text not found in file"
                };
            }

            string? backupPath = null;
            if (createBackup)
            {
                backupPath = await CreateBackupAsync(resolvedPath);
            }

            var occurrences = CountOccurrences(content, searchText);
            var newContent = content.Replace(searchText, replacementText);

            await File.WriteAllTextAsync(resolvedPath, newContent, Encoding.UTF8);

            return new FileEditResult
            {
                Success = true,
                FilePath = resolvedPath,
                LinesModified = occurrences,
                BackupPath = backupPath,
                Message = $"Replaced {occurrences} occurrence(s) of text"
            };
        }
        catch (Exception ex)
        {
            return new FileEditResult
            {
                Success = false,
                FilePath = filePath,
                Error = $"Error replacing text: {ex.Message}"
            };
        }
    }

    public async Task<FileEditResult> InsertTextAsync(
        string filePath,
        int lineNumber,
        string content,
        bool createBackup = true)
    {
        try
        {
            var resolvedPath = _workingDirService.ResolveRelativePath(filePath);

            if (!File.Exists(resolvedPath))
            {
                return new FileEditResult
                {
                    Success = false,
                    FilePath = resolvedPath,
                    Error = $"File not found: {resolvedPath}"
                };
            }

            if (!ValidateFileSize(resolvedPath))
            {
                return new FileEditResult
                {
                    Success = false,
                    FilePath = resolvedPath,
                    Error = $"File too large (max {MaxFileSizeBytes / (1024 * 1024)} MB)"
                };
            }

            var lines = (await File.ReadAllLinesAsync(resolvedPath, Encoding.UTF8)).ToList();

            if (lineNumber < 1 || lineNumber > lines.Count + 1)
            {
                return new FileEditResult
                {
                    Success = false,
                    FilePath = resolvedPath,
                    Error = $"Invalid line number: {lineNumber} (file has {lines.Count} lines)"
                };
            }

            string? backupPath = null;
            if (createBackup)
            {
                backupPath = await CreateBackupAsync(resolvedPath);
            }

            lines.Insert(lineNumber - 1, content);

            await File.WriteAllLinesAsync(resolvedPath, lines, Encoding.UTF8);

            return new FileEditResult
            {
                Success = true,
                FilePath = resolvedPath,
                LinesModified = 1,
                BackupPath = backupPath,
                Message = $"Inserted text at line {lineNumber}"
            };
        }
        catch (Exception ex)
        {
            return new FileEditResult
            {
                Success = false,
                FilePath = filePath,
                Error = $"Error inserting text: {ex.Message}"
            };
        }
    }

    public async Task<FileEditResult> AppendTextAsync(
        string filePath,
        string content,
        bool createBackup = true)
    {
        try
        {
            var resolvedPath = _workingDirService.ResolveRelativePath(filePath);

            if (!File.Exists(resolvedPath))
            {
                return new FileEditResult
                {
                    Success = false,
                    FilePath = resolvedPath,
                    Error = $"File not found: {resolvedPath}"
                };
            }

            if (!ValidateFileSize(resolvedPath))
            {
                return new FileEditResult
                {
                    Success = false,
                    FilePath = resolvedPath,
                    Error = $"File too large (max {MaxFileSizeBytes / (1024 * 1024)} MB)"
                };
            }

            string? backupPath = null;
            if (createBackup)
            {
                backupPath = await CreateBackupAsync(resolvedPath);
            }

            await File.AppendAllTextAsync(resolvedPath, content, Encoding.UTF8);

            return new FileEditResult
            {
                Success = true,
                FilePath = resolvedPath,
                BackupPath = backupPath,
                Message = "Content appended to file"
            };
        }
        catch (Exception ex)
        {
            return new FileEditResult
            {
                Success = false,
                FilePath = filePath,
                Error = $"Error appending text: {ex.Message}"
            };
        }
    }

    public async Task<FileEditResult> DeleteLinesAsync(
        string filePath,
        int startLine,
        int endLine,
        bool createBackup = true)
    {
        try
        {
            var resolvedPath = _workingDirService.ResolveRelativePath(filePath);

            if (!File.Exists(resolvedPath))
            {
                return new FileEditResult
                {
                    Success = false,
                    FilePath = resolvedPath,
                    Error = $"File not found: {resolvedPath}"
                };
            }

            if (!ValidateFileSize(resolvedPath))
            {
                return new FileEditResult
                {
                    Success = false,
                    FilePath = resolvedPath,
                    Error = $"File too large (max {MaxFileSizeBytes / (1024 * 1024)} MB)"
                };
            }

            var lines = (await File.ReadAllLinesAsync(resolvedPath, Encoding.UTF8)).ToList();

            if (startLine < 1 || endLine > lines.Count || startLine > endLine)
            {
                return new FileEditResult
                {
                    Success = false,
                    FilePath = resolvedPath,
                    Error = $"Invalid line range: {startLine}-{endLine} (file has {lines.Count} lines)"
                };
            }

            string? backupPath = null;
            if (createBackup)
            {
                backupPath = await CreateBackupAsync(resolvedPath);
            }

            var count = endLine - startLine + 1;
            lines.RemoveRange(startLine - 1, count);

            await File.WriteAllLinesAsync(resolvedPath, lines, Encoding.UTF8);

            return new FileEditResult
            {
                Success = true,
                FilePath = resolvedPath,
                LinesModified = count,
                BackupPath = backupPath,
                Message = $"Deleted {count} line(s)"
            };
        }
        catch (Exception ex)
        {
            return new FileEditResult
            {
                Success = false,
                FilePath = filePath,
                Error = $"Error deleting lines: {ex.Message}"
            };
        }
    }

    public async Task<FileEditResult> WriteFileAsync(
        string filePath,
        string content,
        bool createBackup = true)
    {
        try
        {
            var resolvedPath = _workingDirService.ResolveRelativePath(filePath);
            var fileExists = File.Exists(resolvedPath);

            if (fileExists && !ValidateFileSize(resolvedPath))
            {
                return new FileEditResult
                {
                    Success = false,
                    FilePath = resolvedPath,
                    Error = $"File too large (max {MaxFileSizeBytes / (1024 * 1024)} MB)"
                };
            }

            string? backupPath = null;
            if (fileExists && createBackup)
            {
                backupPath = await CreateBackupAsync(resolvedPath);
            }

            await File.WriteAllTextAsync(resolvedPath, content, Encoding.UTF8);

            return new FileEditResult
            {
                Success = true,
                FilePath = resolvedPath,
                BackupPath = backupPath,
                Message = fileExists ? "File updated" : "File created"
            };
        }
        catch (Exception ex)
        {
            return new FileEditResult
            {
                Success = false,
                FilePath = filePath,
                Error = $"Error writing file: {ex.Message}"
            };
        }
    }

    public async Task<string> ReadFileAsync(string filePath)
    {
        var resolvedPath = _workingDirService.ResolveRelativePath(filePath);

        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException($"File not found: {resolvedPath}");
        }

        if (!ValidateFileSize(resolvedPath))
        {
            throw new InvalidOperationException($"File too large (max {MaxFileSizeBytes / (1024 * 1024)} MB)");
        }

        return await File.ReadAllTextAsync(resolvedPath, Encoding.UTF8);
    }

    private async Task<string> CreateBackupAsync(string filePath)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        var fileName = Path.GetFileName(filePath);
        var backupPath = Path.Combine(directory, $"{fileName}.backup_{timestamp}");

        await Task.Run(() => File.Copy(filePath, backupPath));

        return backupPath;
    }

    private bool ValidateFileSize(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        return fileInfo.Length <= MaxFileSizeBytes;
    }

    private int CountOccurrences(string content, string searchText)
    {
        if (string.IsNullOrEmpty(searchText))
            return 0;

        int count = 0;
        int position = 0;

        while ((position = content.IndexOf(searchText, position, StringComparison.Ordinal)) != -1)
        {
            count++;
            position += searchText.Length;
        }

        return count;
    }
}
