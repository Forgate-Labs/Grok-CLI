namespace GrokCLI.Services;

/// <summary>
/// Command adapter implementation for translating commands between platforms
/// </summary>
public class CommandAdapter : ICommandAdapter
{
    private readonly IPlatformService _platformService;

    public CommandAdapter(IPlatformService platformService)
    {
        _platformService = platformService;
    }

    public string ListDirectory(string path)
    {
        return _platformService.IsWindows
            ? $"Get-ChildItem -Path '{path}' | Format-Table Name, Length, LastWriteTime"
            : $"ls -lah '{path}'";
    }

    public string ChangeDirectory(string path)
    {
        return _platformService.IsWindows
            ? $"Set-Location '{path}'"
            : $"cd '{path}'";
    }

    public string ReadFile(string path)
    {
        return _platformService.IsWindows
            ? $"Get-Content -Path '{path}' -Encoding UTF8"
            : $"cat '{path}'";
    }

    public string WriteFile(string path, string content)
    {
        var escapedContent = content.Replace("'", "''");
        return _platformService.IsWindows
            ? $"Set-Content -Path '{path}' -Value '{escapedContent}' -Encoding UTF8"
            : $"echo '{escapedContent}' > '{path}'";
    }

    public string DeleteFile(string path)
    {
        return _platformService.IsWindows
            ? $"Remove-Item -Path '{path}' -Force"
            : $"rm -f '{path}'";
    }

    public string CopyFile(string source, string destination)
    {
        return _platformService.IsWindows
            ? $"Copy-Item -Path '{source}' -Destination '{destination}' -Force"
            : $"cp '{source}' '{destination}'";
    }

    public string MoveFile(string source, string destination)
    {
        return _platformService.IsWindows
            ? $"Move-Item -Path '{source}' -Destination '{destination}' -Force"
            : $"mv '{source}' '{destination}'";
    }

    public string CreateDirectory(string path)
    {
        return _platformService.IsWindows
            ? $"New-Item -ItemType Directory -Path '{path}' -Force"
            : $"mkdir -p '{path}'";
    }

    public string FindFiles(string pattern, string path)
    {
        return _platformService.IsWindows
            ? $"Get-ChildItem -Path '{path}' -Filter '{pattern}' -Recurse -File"
            : $"find '{path}' -name '{pattern}' -type f";
    }

    public string SearchInFiles(string pattern, string path)
    {
        return _platformService.IsWindows
            ? $"Get-ChildItem -Path '{path}' -Recurse -File | Select-String -Pattern '{pattern}'"
            : $"grep -r '{pattern}' '{path}'";
    }
}
