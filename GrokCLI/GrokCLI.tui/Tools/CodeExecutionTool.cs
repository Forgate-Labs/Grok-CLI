using System.Diagnostics;
using System.Text.Json;
using GrokCLI.Models;
using OpenAI.Chat;

namespace GrokCLI.Tools;

public class CodeExecutionTool : ITool
{
    public string Name => "code_execution";
    public string Description => "Executes Python code";

    public ChatTool GetChatTool()
    {
        return ChatTool.CreateFunctionTool(
            Name,
            Description,
            BinaryData.FromString(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""code"": {
                        ""type"": ""string"",
                        ""description"": ""The Python code to execute""
                    }
                },
                ""required"": [""code""]
            }")
        );
    }

    public async Task<ToolExecutionResult> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var jsonDoc = JsonDocument.Parse(argumentsJson);
            var code = jsonDoc.RootElement.GetProperty("code").GetString() ?? "";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "python3",
                    Arguments = $"-c \"{code.Replace("\"", "\\\"")}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                return ToolExecutionResult.CreateError(error);

            return ToolExecutionResult.CreateSuccess(output);
        }
        catch (Exception ex)
        {
            return ToolExecutionResult.CreateError($"Error executing code: {ex.Message}");
        }
    }
}
