using System.Text.Json;

namespace GrokCLI.UI;

public static class PlanParser
{
    public static PlanPayload? Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var title = root.TryGetProperty("title", out var titleProp)
                ? titleProp.GetString()
                : null;

            if (!root.TryGetProperty("items", out var itemsProp) || itemsProp.ValueKind != JsonValueKind.Array)
                return null;

            var items = new List<PlanEntry>();

            foreach (var item in itemsProp.EnumerateArray())
            {
                if (!item.TryGetProperty("title", out var tProp))
                    continue;

                var itemTitle = tProp.GetString();
                if (string.IsNullOrWhiteSpace(itemTitle))
                    continue;

                var status = item.TryGetProperty("status", out var sProp)
                    ? sProp.GetString()
                    : null;

                items.Add(new PlanEntry(itemTitle, NormalizeStatus(status)));
            }

            return new PlanPayload(title, items);
        }
        catch
        {
            return null;
        }
    }

    private static PlanStatus NormalizeStatus(string? value)
    {
        var normalized = value?.Replace("-", "_").ToLowerInvariant();

        return normalized switch
        {
            "done" or "complete" or "completed" or "finished" or "ok" or "success" => PlanStatus.Done,
            "in_progress" or "progress" or "doing" or "active" or "working" => PlanStatus.InProgress,
            _ => PlanStatus.Pending
        };
    }
}
