using System.Collections.Generic;

namespace GrokCLI.UI;

public sealed record PlanPayload(string? Title, List<PlanEntry> Items);
