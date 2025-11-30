using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terminal.Gui;
using NStack;
using GuiAttribute = Terminal.Gui.Attribute;

namespace GrokCLI.UI;

public sealed class HistoryViewManager
{
    private readonly ScrollView _historyView;
    private readonly List<IHistoryItem> _blocks = new();
    private readonly HashSet<string> _reasoningLines = new();
    private int _activeBlockIndex = -1;
    private int _thinkingBlockIndex = -1;
    private readonly ColorScheme _defaultScheme;
    private readonly ColorScheme _userScheme;

    public HistoryViewManager(ScrollView historyView)
    {
        _historyView = historyView;
        _historyView.ShowVerticalScrollIndicator = true;
        _historyView.ShowHorizontalScrollIndicator = false;
        _historyView.ContentSize = new Size(0, 0);
        _historyView.LayoutComplete += OnLayoutComplete;
        _defaultScheme = _historyView.ColorScheme ?? Colors.Base;
        _userScheme = new ColorScheme
        {
            Normal = new GuiAttribute(Color.Black, Color.Gray),
            Focus = new GuiAttribute(Color.Black, Color.Gray),
            HotNormal = new GuiAttribute(Color.Black, Color.Gray),
            HotFocus = new GuiAttribute(Color.Black, Color.Gray),
            Disabled = new GuiAttribute(Color.Gray, Color.Black)
        };
    }

    public bool HasBlocks => _blocks.Count > 0;

    public void SetContent(string text)
    {
        Clear();
        AddBlockFromText(text);
    }

    public void AddBlock(string title, string body)
    {
        var block = new HistoryBlock(title, body);
        AttachBlock(block);
    }

    public void AddBlockFromText(string text)
    {
        var normalized = SummaryTextFormatter.Normalize(text);
        var lines = normalized.Split('\n');
        var firstLine = lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? "";
        if (IsWorkedLine(firstLine))
        {
            AddItem(new HistoryLabelItem(firstLine.Trim(), _defaultScheme, false));
            return;
        }

        if (IsUserLine(firstLine))
        {
            AddItem(new HistoryLabelItem(normalized, _userScheme, false));
            return;
        }

        var (title, body) = SplitTitleAndBody(lines, firstLine);
        AddBlock(title, body);
    }

    public void Append(string text)
    {
        if (_activeBlockIndex < 0 || _activeBlockIndex >= _blocks.Count || !_blocks[_activeBlockIndex].AcceptsAppend)
        {
            AddBlockFromText(text);
            return;
        }

        _blocks[_activeBlockIndex].Append(text);
        LayoutBlocks(true);
    }

    public void AppendToNewBlock(string text)
    {
        AddBlockFromText(text);
    }

    public void AppendReasoning(string text)
    {
        var normalized = SummaryTextFormatter.Normalize(text);
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        var lines = normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            return;

        if (_thinkingBlockIndex < 0)
        {
            _thinkingBlockIndex = _blocks.Count;
            AddBlock("Thinking", string.Empty);
        }

        foreach (var line in lines)
        {
            if (_reasoningLines.Contains(line))
                continue;

            _reasoningLines.Add(line);
            _blocks[_thinkingBlockIndex].Append(SummaryTextFormatter.BuildLine(line));
        }

        LayoutBlocks(true);
    }

    public void ResetReasoningBlock()
    {
        _thinkingBlockIndex = -1;
        _reasoningLines.Clear();
    }

    public void Clear()
    {
        foreach (var block in _blocks)
        {
            _historyView.Remove(block.View);
        }

        _blocks.Clear();
        _activeBlockIndex = -1;
        _thinkingBlockIndex = -1;
        _reasoningLines.Clear();
        _historyView.ContentSize = new Size(_historyView.Bounds.Width, 0);
    }

    private void AttachBlock(HistoryBlock block)
    {
        block.Frame.MouseClick += _ => ShowBlockDetails(block);
        _historyView.Add(block.Frame);
        _blocks.Add(block);
        _activeBlockIndex = _blocks.Count - 1;
        LayoutBlocks(true);
    }

    private void AddItem(IHistoryItem item)
    {
        _historyView.Add(item.View);
        _blocks.Add(item);
        _activeBlockIndex = _blocks.Count - 1;
        LayoutBlocks(true);
    }

    private void OnLayoutComplete(View.LayoutEventArgs _)
    {
        LayoutBlocks(false);
    }

    private void LayoutBlocks(bool scrollToEnd)
    {
        var width = Math.Max(1, _historyView.Frame.Width - (_historyView.ShowVerticalScrollIndicator ? 1 : 0));
        var innerWidth = Math.Max(1, width - 2);
        var y = 0;

        foreach (var block in _blocks)
        {
            var frameHeight = block.UpdateLayout(width, innerWidth);
            block.View.X = 0;
            block.View.Y = y;
            y += frameHeight;
        }

        _historyView.ContentSize = new Size(width, Math.Max(y, _historyView.Bounds.Height));

        if (scrollToEnd)
        {
            var offsetY = Math.Max(0, y - _historyView.Bounds.Height);
            _historyView.ContentOffset = new Point(0, offsetY);
        }
    }

    private void ShowBlockDetails(HistoryBlock block)
    {
        var width = Math.Min(Application.Driver.Cols - 4, 100);
        var height = Math.Min(Application.Driver.Rows - 4, 25);
        if (width < 40)
            width = 40;
        if (height < 10)
            height = 10;

        var popover = new Popover(block.Title, width, height);
        var content = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            ReadOnly = true,
            WordWrap = true,
            Text = block.Body
        };

        var closeButton = new Button("Close")
        {
            X = Pos.Center(),
            Y = Pos.Bottom(content)
        };

        closeButton.Clicked += () => Application.RequestStop();

        popover.Add(content, closeButton);
        Application.Run(popover);
        _historyView.SetFocus();
    }

    public void EnsureAssistantStream()
    {
        if (_activeBlockIndex >= 0 &&
            _activeBlockIndex < _blocks.Count &&
            _blocks[_activeBlockIndex] is HistoryLabelItem label &&
            label.AcceptsAppend &&
            label.IsAssistant)
            return;

        AddItem(new HistoryLabelItem(string.Empty, _defaultScheme, true, true));
    }

    private static bool IsWorkedLine(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("─ Worked for ", StringComparison.Ordinal);
    }

    private static bool IsUserLine(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("[You]:", StringComparison.Ordinal);
    }

    private static (string title, string body) SplitTitleAndBody(string[] lines, string firstLine)
    {
        var titleLine = string.IsNullOrWhiteSpace(firstLine)
            ? lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? "Message"
            : firstLine;
        titleLine = TrimPrefix(titleLine, "● ");
        titleLine = TrimPrefix(titleLine, "⎿ ");
        var bodyLines = lines.SkipWhile(l => string.IsNullOrWhiteSpace(l)).Skip(1)
            .Select(l => TrimPrefix(l, "⎿ "))
            .ToArray();
        var body = string.Join("\n", bodyLines).TrimEnd('\n');
        return (string.IsNullOrWhiteSpace(titleLine) ? "Message" : titleLine, body);
    }

    private static string TrimPrefix(string value, string prefix)
    {
        if (value.StartsWith(prefix, StringComparison.Ordinal))
            return value[prefix.Length..];
        return value;
    }
}

internal interface IHistoryItem
{
    View View { get; }
    bool AcceptsAppend { get; }
    bool IsAssistant { get; }
    void Append(string text);
    int UpdateLayout(int frameWidth, int innerWidth);
}

internal sealed class HistoryBlock : IHistoryItem
{
    private readonly StringBuilder _bodyBuilder = new();

    public HistoryBlock(string title, string body)
    {
        Title = string.IsNullOrWhiteSpace(title) ? "Message" : title;
        Frame = new FrameView(Title)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 3,
            CanFocus = true
        };
        BodyView = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = false,
            TextAlignment = TextAlignment.Left
        };
        Frame.Add(BodyView);
        SetBody(body);
    }

    public string Title { get; }
    public FrameView Frame { get; }
    public Label BodyView { get; }
    public View View => Frame;
    public bool AcceptsAppend => true;
    public bool IsAssistant => false;
    public string Body => _bodyBuilder.ToString();

    public void Append(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        _bodyBuilder.Append(text);
    }

    public void SetBody(string text)
    {
        _bodyBuilder.Clear();
        if (!string.IsNullOrEmpty(text))
            _bodyBuilder.Append(text);
    }

    public int UpdateLayout(int frameWidth, int innerWidth)
    {
        var body = SummaryTextFormatter.Normalize(Body);
        var wrapped = Wrap(body, innerWidth).ToList();
        if (wrapped.Count == 0)
            wrapped.Add(ustring.Make(string.Empty));

        Frame.Width = frameWidth;
        BodyView.Width = innerWidth;
        BodyView.Text = string.Join("\n", wrapped.Select(u => u.ToString()));

        var bodyHeight = wrapped.Count;
        BodyView.Height = bodyHeight;
        var frameHeight = Math.Max(3, bodyHeight + 2);
        Frame.Height = frameHeight;
        return frameHeight;
    }

    private static IEnumerable<ustring> Wrap(string text, int width)
    {
        if (width <= 0)
            return new[] { ustring.Make(string.Empty) };

        var normalized = SummaryTextFormatter.Normalize(text);
        var u = ustring.Make(normalized);
        return TextFormatter.WordWrap(u, width, true, 4, TextDirection.LeftRight_TopBottom);
    }
}

internal sealed class HistoryLabelItem : IHistoryItem
{
    private readonly Label _label;
    private readonly bool _acceptsAppend;
    private readonly bool _isAssistant;
    private readonly StringBuilder _content = new();

    public HistoryLabelItem(string text, ColorScheme? scheme, bool acceptsAppend, bool isAssistant = false)
    {
        _acceptsAppend = acceptsAppend;
        _isAssistant = isAssistant;
        _content.Append(text ?? string.Empty);
        _label = new Label(_content.ToString())
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
            TextAlignment = TextAlignment.Left,
            CanFocus = true
        };
        if (scheme != null)
            _label.ColorScheme = scheme;
    }

    public View View => _label;
    public bool AcceptsAppend => _acceptsAppend;
    public bool IsAssistant => _isAssistant;

    public void Append(string text)
    {
        if (!_acceptsAppend || string.IsNullOrEmpty(text))
            return;

        _content.Append(text);
    }

    public int UpdateLayout(int frameWidth, int innerWidth)
    {
        var wrapped = Wrap(_content.ToString(), frameWidth).ToList();
        if (wrapped.Count == 0)
            wrapped.Add(ustring.Make(string.Empty));

        _label.Width = frameWidth;
        _label.Height = wrapped.Count;
        _label.Text = string.Join("\n", wrapped.Select(u => u.ToString()));
        return wrapped.Count;
    }

    private static IEnumerable<ustring> Wrap(string text, int width)
    {
        if (width <= 0)
            return new[] { ustring.Make(string.Empty) };

        var normalized = (text ?? string.Empty).ReplaceLineEndings("\n");
        var lines = normalized.Split('\n', StringSplitOptions.None);
        var wrapped = new List<ustring>();
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                wrapped.Add(ustring.Make(string.Empty));
                continue;
            }

            var u = ustring.Make(line);
            wrapped.AddRange(TextFormatter.WordWrap(u, width, true, 4, TextDirection.LeftRight_TopBottom));
        }

        return wrapped;
    }
}
