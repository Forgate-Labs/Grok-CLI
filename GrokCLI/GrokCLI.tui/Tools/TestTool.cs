using GrokCLI.Models;
using GrokCLI.Tools;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace GrokCLI.tui.Tools
{
    public class TestTool : ITool
    {
        public string Name => "test";

        public string Description => "Executes a test command.";

        public Task<ToolExecutionResult> ExecuteAsync(string argumentsJson)
        {
            var jsonDoc = JsonDocument.Parse(argumentsJson);
            var code = "TESTE!!!!!";

            return Task.FromResult(ToolExecutionResult.CreateSuccess(code));
        }

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
                        ""description"": ""The test string to test""
                    }
                },
                ""required"": [""code""]
            }")
        );
        }
    }
}
