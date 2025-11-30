using Terminal.Gui;

namespace GrokCLI.UI;

public sealed class HistoryViewManager
{
    private readonly TextView _historyView;
    private readonly ScrollBarView _scrollBar;
    private bool _thinkingBlockOpen;
    private readonly HashSet<string> _reasoningLines = new();

    public HistoryViewManager(TextView historyView)
    {
        _historyView = historyView;
        _scrollBar = new ScrollBarView(_historyView, true, false);
        _scrollBar.ChangedPosition += OnScrollBarChanged;
        _scrollBar.VisibleChanged += OnScrollBarVisibilityChanged;
        _historyView.DrawContent += OnHistoryViewDrawContent;
    }

    public void Append(string text)
    {
        var current = _historyView.Text?.ToString() ?? "";
        _historyView.Text = current + text;
        _historyView.MoveEnd();
    }

    public void AppendCommandOutput(string text)
    {
        Append(text);
    }

    public void AppendReasoning(string text)
    {
        var normalized = SummaryTextFormatter.Normalize(text);
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        var lines = normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            return;

        if (!_thinkingBlockOpen)
        {
            Append("\n● Thinking\n");
            _thinkingBlockOpen = true;
        }

        foreach (var line in lines)
        {
            if (_reasoningLines.Contains(line))
                continue;

            _reasoningLines.Add(line);
            Append(SummaryTextFormatter.BuildLine(line));
        }
    }

    public void ResetReasoningBlock()
    {
        _thinkingBlockOpen = false;
        _reasoningLines.Clear();
    }

    public void Clear()
    {
        _historyView.Text = "";
        ResetReasoningBlock();
    }

    public void SetContent(string text)
    {
        _historyView.Text = text;
        _historyView.MoveEnd();
    }

    private void OnScrollBarChanged()
    {
        _historyView.TopRow = _scrollBar.Position;
        if (_scrollBar.Position != _historyView.TopRow)
            _scrollBar.Position = _historyView.TopRow;
        _historyView.SetNeedsDisplay();
    }

    private void OnScrollBarVisibilityChanged()
    {
        _historyView.RightOffset = _scrollBar.Visible ? 1 : 0;
    }

    private void OnHistoryViewDrawContent(Rect _)
    {
        _scrollBar.Size = _historyView.Lines;
        _scrollBar.Position = _historyView.TopRow;
        _scrollBar.Refresh();
    }
}
