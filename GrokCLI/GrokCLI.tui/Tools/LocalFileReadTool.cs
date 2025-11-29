using System.Text.Json;
using System.Threading;
using GrokCLI.Models;
using OpenAI.Chat;

namespace GrokCLI.Tools;

public class LocalFileReadTool : ITool
{
    public string Name => "read_local_file";
    public string Description => "Reads a file from the current working directory";

    public ChatTool GetChatTool()
    {
        return ChatTool.CreateFunctionTool(
            Name,
            Description,
            BinaryData.FromString(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""path"": {
                        ""type"": ""string"",
                        ""description"": ""Relative path to the file to read from the current working directory""
                    }
                },
                ""required"": [""path""]
            }")
        );
    }

    public async Task<ToolExecutionResult> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var jsonDoc = JsonDocument.Parse(argumentsJson);
            var relativePath = jsonDoc.RootElement.GetProperty("path").GetString();

            if (string.IsNullOrWhiteSpace(relativePath))
                return ToolExecutionResult.CreateError("Path is required");

            var baseDirectory = Path.GetFullPath(Directory.GetCurrentDirectory());
            var targetPath = Path.GetFullPath(Path.Combine(baseDirectory, relativePath));
            var baseWithSeparator = baseDirectory.EndsWith(Path.DirectorySeparatorChar)
                ? baseDirectory
                : baseDirectory + Path.DirectorySeparatorChar;

            if (!targetPath.StartsWith(baseWithSeparator, StringComparison.OrdinalIgnoreCase))
                return ToolExecutionResult.CreateError("Path must stay inside the working directory");

            if (!File.Exists(targetPath))
                return ToolExecutionResult.CreateError($"File not found: {relativePath}");

            var fileInfo = new FileInfo(targetPath);
            const long maxBytes = 200_000;
            if (fileInfo.Length > maxBytes)
                return ToolExecutionResult.CreateError($"File is too large to read (limit {maxBytes} bytes)");

            var content = await File.ReadAllTextAsync(targetPath, cancellationToken);
            return ToolExecutionResult.CreateSuccess(content);
        }
        catch (JsonException)
        {
            return ToolExecutionResult.CreateError("Invalid arguments payload");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ToolExecutionResult.CreateError($"Error reading file: {ex.Message}");
        }
    }
}
