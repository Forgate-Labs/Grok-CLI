namespace GrokCLI.Services;

public interface IWorkingDirectoryService
{
    string GetCurrentDirectory();

    void SetCurrentDirectory(string path);

    string ResolveRelativePath(string path);

    bool DirectoryExists(string path);

    string GetInitialDirectory();
}
