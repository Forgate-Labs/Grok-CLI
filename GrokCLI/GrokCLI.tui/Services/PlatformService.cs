using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace GrokCLI.Services;

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

        if (path.StartsWith("~"))
        {
            path = path.Replace("~", HomeDirectory);
        }

        if (IsWindows)
        {
            path = path.Replace('/', '\\');
        }
        else
        {
            path = path.Replace('\\', '/');
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
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
        return command;
    }
}
