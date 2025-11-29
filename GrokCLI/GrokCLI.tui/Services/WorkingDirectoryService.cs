namespace GrokCLI.Services;

public class WorkingDirectoryService : IWorkingDirectoryService
{
    private string _currentDirectory;
    private readonly string _initialDirectory;
    private readonly IPlatformService _platformService;
    private readonly object _lock = new object();

    public WorkingDirectoryService(IPlatformService platformService)
    {
        _platformService = platformService;
        _initialDirectory = Directory.GetCurrentDirectory();
        _currentDirectory = _initialDirectory;
    }

    public string GetCurrentDirectory()
    {
        lock (_lock)
        {
            return _currentDirectory;
        }
    }

    public void SetCurrentDirectory(string path)
    {
        lock (_lock)
        {
            var resolvedPath = ResolveRelativePath(path);
            if (!Directory.Exists(resolvedPath))
            {
                throw new DirectoryNotFoundException($"Directory not found: {resolvedPath}");
            }
            _currentDirectory = resolvedPath;
        }
    }

    public string ResolveRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return GetCurrentDirectory();
        }

        var normalizedPath = _platformService.NormalizePath(path);

        if (Path.IsPathRooted(normalizedPath))
        {
            return Path.GetFullPath(normalizedPath);
        }

        string currentDir;
        lock (_lock)
        {
            currentDir = _currentDirectory;
        }

        var combined = Path.Combine(currentDir, normalizedPath);
        return Path.GetFullPath(combined);
    }

    public bool DirectoryExists(string path)
    {
        try
        {
            var resolvedPath = ResolveRelativePath(path);
            return Directory.Exists(resolvedPath);
        }
        catch
        {
            return false;
        }
    }

    public string GetInitialDirectory()
    {
        return _initialDirectory;
    }
}
