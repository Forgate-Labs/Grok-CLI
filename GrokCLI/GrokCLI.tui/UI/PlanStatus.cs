namespace GrokCLI.UI;

public enum PlanStatus
{
    Pending,
    InProgress,
    Done
}

public record PlanEntry(string Title, PlanStatus Status);
