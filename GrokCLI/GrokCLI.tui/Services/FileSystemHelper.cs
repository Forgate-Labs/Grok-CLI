namespace GrokCLI.Services;

public class FileSystemHelper
{
    private readonly IPlatformService _platform;

    public FileSystemHelper(IPlatformService platform)
    {
        _platform = platform;
    }

    public StringComparison PathComparison =>
        _platform.IsLinux
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

    public bool PathsEqual(string path1, string path2)
    {
        if (string.IsNullOrWhiteSpace(path1) || string.IsNullOrWhiteSpace(path2))
            return false;

        var normalized1 = _platform.NormalizePath(path1);
        var normalized2 = _platform.NormalizePath(path2);
        return string.Equals(normalized1, normalized2, PathComparison);
    }

    public bool IsAbsolutePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            return Path.IsPathRooted(path);
        }
        catch
        {
            return false;
        }
    }

    public bool EnsureDirectoryExists(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string GetSafeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return fileName;

        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var c in invalidChars)
        {
            fileName = fileName.Replace(c, '_');
        }

        return fileName;
    }

    public string CombinePath(params string[] parts)
    {
        return Path.Combine(parts);
    }

    public string GetRelativePath(string fromPath, string toPath)
    {
        try
        {
            return Path.GetRelativePath(fromPath, toPath);
        }
        catch
        {
            return toPath;
        }
    }
}
