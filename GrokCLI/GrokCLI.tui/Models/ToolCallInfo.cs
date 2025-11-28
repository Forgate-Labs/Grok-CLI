using System.Text;

namespace GrokCLI.Models;

public class ToolCallInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public StringBuilder Arguments { get; set; } = new StringBuilder();

    public ToolCallInfo() { }

    public ToolCallInfo(string id, string name)
    {
        Id = id;
        Name = name;
    }
}
