using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace GrokCLI.Services;

/// <summary>
/// Platform service implementation for detecting and handling platform-specific operations
/// </summary>
public class PlatformService : IPlatformService
{
    public PlatformType Platform { get; }
    public string ShellType { get; }
    public string PathSeparator { get; }
    public string LineEnding { get; }
    public string HomeDirectory { get; }

    public bool IsWindows => Platform == PlatformType.Windows;
    public bool IsLinux => Platform == PlatformType.Linux;
    public bool IsMacOS => Platform == PlatformType.MacOS;

    public PlatformService()
    {
        Platform = DetectPlatform();
        ShellType = GetShellType();
        PathSeparator = Path.DirectorySeparatorChar.ToString();
        LineEnding = IsWindows ? "\r\n" : "\n";
        HomeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private PlatformType DetectPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return PlatformType.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return PlatformType.Linux;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return PlatformType.MacOS;
        return PlatformType.Unknown;
    }

    private string GetShellType()
    {
        return Platform switch
        {
            PlatformType.Windows => "PowerShell",
            PlatformType.Linux => "Bash",
            PlatformType.MacOS => "Bash/Zsh",
            _ => "Unknown"
        };
    }

    public string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        // Expand ~
        if (path.StartsWith("~"))
        {
            path = path.Replace("~", HomeDirectory);
        }

        // Normalize separators
        if (IsWindows)
        {
            // Windows accepts both / and \
            path = path.Replace('/', '\\');
        }
        else
        {
            // Linux/macOS uses only /
            path = path.Replace('\\', '/');
        }

        // Normalize full path
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            // If path normalization fails, return the cleaned path
            return path;
        }
    }

    public ProcessStartInfo CreateShellProcess(string command)
    {
        if (IsWindows)
        {
            return new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"{command.Replace("\"", "`\"")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
        }
        else
        {
            // Linux/macOS
            return new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
        }
    }

    public string GetShellCommand(string command)
    {
        // Can be used to translate commands between platforms
        // For now, just return the command as-is
        return command;
    }
}
