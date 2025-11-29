namespace GrokCLI.Models;

public class AppConfig
{
    public string? XaiApiKey { get; set; }
    public string? PrePrompt { get; set; }
    public List<string> AllowedCommands { get; set; } = new();
    public List<string> BlockedCommands { get; set; } = new();
    public string? ConfigPath { get; set; }
}
