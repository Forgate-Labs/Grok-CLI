namespace GrokCLI.Services;

/// <summary>
/// Helper class for cross-platform file system operations
/// </summary>
public class FileSystemHelper
{
    private readonly IPlatformService _platform;

    public FileSystemHelper(IPlatformService platform)
    {
        _platform = platform;
    }

    /// <summary>
    /// Gets the appropriate string comparison for path comparisons on the current platform
    /// </summary>
    public StringComparison PathComparison =>
        _platform.IsLinux
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

    /// <summary>
    /// Compares two paths for equality considering platform-specific case sensitivity
    /// </summary>
    /// <param name="path1">First path to compare</param>
    /// <param name="path2">Second path to compare</param>
    /// <returns>True if paths are equal, false otherwise</returns>
    public bool PathsEqual(string path1, string path2)
    {
        if (string.IsNullOrWhiteSpace(path1) || string.IsNullOrWhiteSpace(path2))
            return false;

        var normalized1 = _platform.NormalizePath(path1);
        var normalized2 = _platform.NormalizePath(path2);
        return string.Equals(normalized1, normalized2, PathComparison);
    }

    /// <summary>
    /// Checks if a path is absolute
    /// </summary>
    /// <param name="path">The path to check</param>
    /// <returns>True if the path is absolute, false otherwise</returns>
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

    /// <summary>
    /// Ensures a directory exists, creating it if necessary
    /// </summary>
    /// <param name="path">The directory path</param>
    /// <returns>True if directory exists or was created, false otherwise</returns>
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

    /// <summary>
    /// Gets a safe file name by removing invalid characters
    /// </summary>
    /// <param name="fileName">The file name to sanitize</param>
    /// <returns>A safe file name</returns>
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

    /// <summary>
    /// Combines path parts using the platform-specific separator
    /// </summary>
    /// <param name="parts">The path parts to combine</param>
    /// <returns>The combined path</returns>
    public string CombinePath(params string[] parts)
    {
        return Path.Combine(parts);
    }

    /// <summary>
    /// Gets the relative path from one path to another
    /// </summary>
    /// <param name="fromPath">The starting path</param>
    /// <param name="toPath">The target path</param>
    /// <returns>The relative path, or the toPath if relative path cannot be determined</returns>
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
