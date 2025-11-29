namespace GrokCLI.Services;

/// <summary>
/// Adapter for translating common commands between different platforms
/// </summary>
public interface ICommandAdapter
{
    /// <summary>
    /// Gets the command to list directory contents
    /// </summary>
    /// <param name="path">The directory path</param>
    /// <returns>Platform-specific command</returns>
    string ListDirectory(string path);

    /// <summary>
    /// Gets the command to change directory
    /// </summary>
    /// <param name="path">The target directory path</param>
    /// <returns>Platform-specific command</returns>
    string ChangeDirectory(string path);

    /// <summary>
    /// Gets the command to read a file
    /// </summary>
    /// <param name="path">The file path</param>
    /// <returns>Platform-specific command</returns>
    string ReadFile(string path);

    /// <summary>
    /// Gets the command to write content to a file
    /// </summary>
    /// <param name="path">The file path</param>
    /// <param name="content">The content to write</param>
    /// <returns>Platform-specific command</returns>
    string WriteFile(string path, string content);

    /// <summary>
    /// Gets the command to delete a file
    /// </summary>
    /// <param name="path">The file path</param>
    /// <returns>Platform-specific command</returns>
    string DeleteFile(string path);

    /// <summary>
    /// Gets the command to copy a file
    /// </summary>
    /// <param name="source">The source file path</param>
    /// <param name="destination">The destination file path</param>
    /// <returns>Platform-specific command</returns>
    string CopyFile(string source, string destination);

    /// <summary>
    /// Gets the command to move a file
    /// </summary>
    /// <param name="source">The source file path</param>
    /// <param name="destination">The destination file path</param>
    /// <returns>Platform-specific command</returns>
    string MoveFile(string source, string destination);

    /// <summary>
    /// Gets the command to create a directory
    /// </summary>
    /// <param name="path">The directory path</param>
    /// <returns>Platform-specific command</returns>
    string CreateDirectory(string path);

    /// <summary>
    /// Gets the command to find files matching a pattern
    /// </summary>
    /// <param name="pattern">The file pattern to match</param>
    /// <param name="path">The directory to search in</param>
    /// <returns>Platform-specific command</returns>
    string FindFiles(string pattern, string path);

    /// <summary>
    /// Gets the command to search for text in files
    /// </summary>
    /// <param name="pattern">The text pattern to search for</param>
    /// <param name="path">The directory to search in</param>
    /// <returns>Platform-specific command</returns>
    string SearchInFiles(string pattern, string path);
}
