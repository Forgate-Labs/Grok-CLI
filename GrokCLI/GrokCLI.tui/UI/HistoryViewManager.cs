using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Terminal.Gui;
using NStack;

namespace GrokCLI.UI;

public sealed class HistoryViewManager
{
    private readonly ListView _historyView;
    private readonly HistoryListDataSource _dataSource;
    private readonly HashSet<string> _reasoningLines = new();
    private readonly int _previewLines;
    private int _activeBlockIndex = -1;
    private int _thinkingBlockIndex = -1;

    public HistoryViewManager(ListView historyView, int previewLines = 6)
    {
        _historyView = historyView;
        _previewLines = previewLines;
        _dataSource = new HistoryListDataSource(previewLines);
        _historyView.Source = _dataSource;
        _historyView.AllowsMarking = false;
        _historyView.OpenSelectedItem += OnOpenSelectedItem;
    }

    public bool HasBlocks => _dataSource.BlockCount > 0;

    public void SetContent(string text)
    {
        Clear();
        AddBlock(text);
    }

    public void AddBlock(string text)
    {
        var block = new HistoryBlock(text);
        _dataSource.AddBlock(block);
        _activeBlockIndex = _dataSource.BlockCount - 1;
        MoveSelectionToBlock(_activeBlockIndex);
        _historyView.SetNeedsDisplay();
    }

    public void Append(string text)
    {
        if (!HasBlocks)
            AddBlock(text);
        else
            AppendToBlock(_activeBlockIndex, text, true);
    }

    public void AppendToNewBlock(string text)
    {
        AddBlock(text);
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
            var header = SummaryTextFormatter.BuildHeader("Thinking").TrimEnd('\n');
            _thinkingBlockIndex = _dataSource.BlockCount;
            _dataSource.AddBlock(new HistoryBlock(header + "\n"));
        }

        foreach (var line in lines)
        {
            if (_reasoningLines.Contains(line))
                continue;

            _reasoningLines.Add(line);
            var formatted = SummaryTextFormatter.BuildLine(line);
            AppendToBlock(_thinkingBlockIndex, formatted, false);
        }

        MoveSelectionToBlock(_thinkingBlockIndex);
        _historyView.SetNeedsDisplay();
    }

    public void ResetReasoningBlock()
    {
        _thinkingBlockIndex = -1;
        _reasoningLines.Clear();
    }

    public void Clear()
    {
        _dataSource.Clear();
        _historyView.SelectedItem = 0;
        _activeBlockIndex = -1;
        _thinkingBlockIndex = -1;
        _reasoningLines.Clear();
        _historyView.SetNeedsDisplay();
    }

    public void MoveToLastBlock()
    {
        if (!HasBlocks)
            return;

        MoveSelectionToBlock(_dataSource.BlockCount - 1);
        _historyView.SetNeedsDisplay();
    }

    private void AppendToBlock(int blockIndex, string text, bool updateActive)
    {
        if (blockIndex < 0 || blockIndex >= _dataSource.BlockCount)
        {
            AddBlock(text);
            return;
        }

        _dataSource.AppendToBlock(blockIndex, text);
        if (updateActive)
            _activeBlockIndex = blockIndex;
        MoveSelectionToBlock(blockIndex);
        _historyView.SetNeedsDisplay();
    }

    private void MoveSelectionToBlock(int blockIndex)
    {
        var rowIndex = _dataSource.GetRowIndexForBlock(blockIndex);
        if (rowIndex < 0 || rowIndex >= _dataSource.Count)
            return;

        _historyView.SelectedItem = rowIndex;
        _historyView.EnsureSelectedItemVisible();
    }

    private void OnOpenSelectedItem(ListViewItemEventArgs args)
    {
        var block = _dataSource.GetBlockFromRow(args.Item);
        if (block == null)
            return;

        var width = Math.Min(Application.Driver.Cols - 4, 100);
        var height = Math.Min(Application.Driver.Rows - 4, 25);
        if (width < 40)
            width = 40;
        if (height < 10)
            height = 10;

        var popover = new Popover("Block Details", width, height);
        var content = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            ReadOnly = true,
            WordWrap = true,
            Text = block.Content
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
}

internal sealed class HistoryBlock
{
    private readonly StringBuilder _builder = new();

    public HistoryBlock(string text)
    {
        Append(text);
    }

    public string Content => _builder.ToString();

    public void Append(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        _builder.Append(text);
    }

    public IReadOnlyList<string> GetPreviewLines(int maxLines)
    {
        var normalized = SummaryTextFormatter.Normalize(Content);
        var lines = normalized.Split('\n');
        var preview = new List<string>(maxLines);
        for (var i = 0; i < maxLines; i++)
        {
            if (i < lines.Length)
            {
                preview.Add(lines[i]);
            }
            else
            {
                preview.Add(string.Empty);
            }
        }

        if (lines.Length > maxLines)
        {
            var remaining = lines.Length - maxLines;
            preview[^1] = $"{preview[^1]} ... (+{remaining} lines)";
        }

        return preview;
    }
}

internal sealed class HistoryListDataSource : IListDataSource
{
    private readonly List<HistoryBlock> _blocks = new();
    private readonly int _previewLines;
    private BitArray _marks = new(0);
    private int _maxLength;

    public HistoryListDataSource(int previewLines)
    {
        _previewLines = previewLines;
    }

    public int BlockCount => _blocks.Count;

    public int Count => _blocks.Count * _previewLines;

    public int Length => _maxLength;

    public void AddBlock(HistoryBlock block)
    {
        _blocks.Add(block);
        ResizeMarks();
        RecalculateMaxLength();
    }

    public void AppendToBlock(int blockIndex, string text)
    {
        if (blockIndex < 0 || blockIndex >= _blocks.Count)
            return;

        _blocks[blockIndex].Append(text);
        RecalculateMaxLength();
    }

    public HistoryBlock? GetBlockFromRow(int row)
    {
        if (row < 0)
            return null;

        var blockIndex = row / _previewLines;
        return blockIndex >= 0 && blockIndex < _blocks.Count ? _blocks[blockIndex] : null;
    }

    public int GetRowIndexForBlock(int blockIndex)
    {
        if (blockIndex < 0 || blockIndex >= _blocks.Count)
            return 0;

        return blockIndex * _previewLines;
    }

    public bool IsMarked(int item)
    {
        if (item < 0 || item >= _marks.Length)
            return false;

        return _marks[item];
    }

    public void SetMark(int item, bool value)
    {
        if (item < 0 || item >= _marks.Length)
            return;

        _marks[item] = value;
    }

    public IList ToList()
    {
        var rows = new List<object>(Count);
        for (var i = 0; i < Count; i++)
        {
            rows.Add(GetPreviewLine(i));
        }

        return rows;
    }

    public void Render(ListView container, ConsoleDriver driver, bool selected, int item, int col, int line, int width, int start = 0)
    {
        var savedClip = container.ClipToBounds();
        container.Move(Math.Max(col - start, 0), line);
        var text = GetPreviewLine(item);
        RenderLine(driver, text, width, start);
        driver.Clip = savedClip;
    }

    public void Clear()
    {
        _blocks.Clear();
        _marks = new BitArray(0);
        _maxLength = 0;
    }

    private string GetPreviewLine(int rowIndex)
    {
        var blockIndex = rowIndex / _previewLines;
        var lineIndex = rowIndex % _previewLines;
        if (blockIndex < 0 || blockIndex >= _blocks.Count)
            return string.Empty;

        var lines = _blocks[blockIndex].GetPreviewLines(_previewLines);
        return lineIndex >= 0 && lineIndex < lines.Count ? lines[lineIndex] : string.Empty;
    }

    private void ResizeMarks()
    {
        var newSize = Math.Max(0, _blocks.Count * _previewLines);
        var updated = new BitArray(newSize);
        var limit = Math.Min(_marks.Length, updated.Length);
        for (var i = 0; i < limit; i++)
        {
            updated[i] = _marks[i];
        }

        _marks = updated;
    }

    private void RecalculateMaxLength()
    {
        var max = 0;
        foreach (var block in _blocks)
        {
            var lines = block.GetPreviewLines(_previewLines);
            foreach (var line in lines)
            {
                var width = TextFormatter.GetTextWidth(ustring.Make(line));
                if (width > max)
                    max = width;
            }
        }

        _maxLength = max;
    }

    private static void RenderLine(ConsoleDriver driver, string text, int width, int start)
    {
        var u = ustring.Make(text ?? string.Empty);
        var runes = u.ToRunes().ToList();
        if (runes.Count == 0)
        {
            AddEmpty(driver, width, start);
            return;
        }

        if (start > runes.Count - 1)
        {
            AddEmpty(driver, width, start);
            return;
        }

        var clipped = TextFormatter.ClipAndJustify(u.Substring(start), width, TextAlignment.Left);
        driver.AddStr(clipped);
        var remaining = width - TextFormatter.GetTextWidth(clipped);
        while (remaining-- + start > 0)
        {
            driver.AddRune(' ');
        }
    }

    private static void AddEmpty(ConsoleDriver driver, int width, int start)
    {
        var count = width + Math.Max(start, 0);
        for (var i = 0; i < count; i++)
        {
            driver.AddRune(' ');
        }
    }
}
