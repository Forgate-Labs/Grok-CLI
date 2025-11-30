using Terminal.Gui;

namespace GrokCLI.UI;

public sealed class PlanPresenter
{
    private readonly FrameView _planFrame;
    private readonly Label _planTitleLabel;
    private readonly TextView _planItemsView;
    private readonly View _historyView;
    private readonly Window _window;
    private readonly int _planExpandedHeight;
    private readonly int _planCollapsedHeight;
    private readonly int _inputHeight;
    private readonly int _inputFrameBorder;
    private readonly int _statusHeight;

    public PlanPresenter(
        FrameView planFrame,
        Label planTitleLabel,
        TextView planItemsView,
        View historyView,
        Window window,
        int planExpandedHeight,
        int planCollapsedHeight,
        int inputHeight,
        int inputFrameBorder,
        int statusHeight)
    {
        _planFrame = planFrame;
        _planTitleLabel = planTitleLabel;
        _planItemsView = planItemsView;
        _historyView = historyView;
        _window = window;
        _planExpandedHeight = planExpandedHeight;
        _planCollapsedHeight = planCollapsedHeight;
        _inputHeight = inputHeight;
        _inputFrameBorder = inputFrameBorder;
        _statusHeight = statusHeight;
    }

    public void Render(PlanPayload payload)
    {
        if (payload.Items.Count == 0)
        {
            Clear();
            return;
        }

        _planFrame.Title = string.IsNullOrWhiteSpace(payload.Title) ? "Plan" : payload.Title;
        _planTitleLabel.Text = "";
        var lines = new List<string>();

        foreach (var item in payload.Items)
        {
            var symbol = item.Status switch
            {
                PlanStatus.Done => "✔",
                PlanStatus.InProgress => "…",
                _ => "□"
            };

            lines.Add($"{symbol} {item.Title}");
        }

        _planItemsView.Text = string.Join("\n", lines);
        SetVisibility(true);
    }

    public void Clear()
    {
        _planFrame.Title = "Plan";
        _planTitleLabel.Text = "Waiting for plan";
        _planItemsView.Text = "";
        SetVisibility(false);
    }

    private void SetVisibility(bool visible)
    {
        var planHeight = visible ? _planExpandedHeight : _planCollapsedHeight;
        var reservedHeight = CalculateReservedHeight(planHeight);

        _planFrame.Visible = visible;
        _planFrame.Height = planHeight;
        _historyView.Height = Dim.Fill(reservedHeight);

        _window.SetNeedsDisplay();
    }

    private int CalculateReservedHeight(int planHeight)
    {
        return planHeight + _inputHeight + _inputFrameBorder + _statusHeight;
    }
}
