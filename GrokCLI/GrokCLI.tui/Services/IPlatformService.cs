using System.Diagnostics;

namespace GrokCLI.Services;

/// <summary>
/// Platform types supported by the application
/// </summary>
public enum PlatformType
{
    Windows,
    Linux,
    MacOS,
    Unknown
}

/// <summary>
/// Service for detecting and handling platform-specific operations
/// </summary>
public interface IPlatformService
{
    /// <summary>
    /// Gets the current platform type
    /// </summary>
    PlatformType Platform { get; }

    /// <summary>
    /// Gets the shell type for the current platform
    /// </summary>
    string ShellType { get; }

    /// <summary>
    /// Gets the path separator for the current platform
    /// </summary>
    string PathSeparator { get; }

    /// <summary>
    /// Gets the line ending for the current platform
    /// </summary>
    string LineEnding { get; }

    /// <summary>
    /// Gets the home directory for the current user
    /// </summary>
    string HomeDirectory { get; }

    /// <summary>
    /// Returns true if running on Windows
    /// </summary>
    bool IsWindows { get; }

    /// <summary>
    /// Returns true if running on Linux
    /// </summary>
    bool IsLinux { get; }

    /// <summary>
    /// Returns true if running on macOS
    /// </summary>
    bool IsMacOS { get; }

    /// <summary>
    /// Normalizes a path for the current platform
    /// </summary>
    /// <param name="path">The path to normalize</param>
    /// <returns>The normalized path</returns>
    string NormalizePath(string path);

    /// <summary>
    /// Creates a ProcessStartInfo configured for executing shell commands on the current platform
    /// </summary>
    /// <param name="command">The command to execute</param>
    /// <returns>A configured ProcessStartInfo</returns>
    ProcessStartInfo CreateShellProcess(string command);

    /// <summary>
    /// Gets the platform-specific command (can be used for command translation)
    /// </summary>
    /// <param name="command">The command to translate</param>
    /// <returns>The platform-specific command</returns>
    string GetShellCommand(string command);
}
