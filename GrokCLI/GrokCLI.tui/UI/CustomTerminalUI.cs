using System.Text;

namespace GrokCLI.UI;

public class CustomTerminalUI
{
    private readonly List<string> _chatMessages = new();
    private readonly object _lock = new();
    private int _scrollOffset = 0;
    private string _currentInput = "";
    private int _cursorPosition = 0;
    private string _processingStatus = "";
    private readonly string _workingDirectory;
    private bool _isRunning = true;
    private int _lastHeight;
    private int _lastWidth;

    public CustomTerminalUI()
    {
        _workingDirectory = Directory.GetCurrentDirectory();
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        Console.CursorVisible = true;
    }

    public void AddChatMessage(string message)
    {
        lock (_lock)
        {
            _chatMessages.Add(message);
            _scrollOffset = 0;
        }
        Render();
    }

    public void AppendToLastMessage(string text)
    {
        lock (_lock)
        {
            if (_chatMessages.Count > 0)
            {
                _chatMessages[^1] += text;
            }
            else
            {
                _chatMessages.Add(text);
            }
            _scrollOffset = 0;
        }
        Render();
    }

    public void SetProcessingStatus(string status)
    {
        lock (_lock)
        {
            _processingStatus = status;
        }
        Render();
    }

    public void ClearChat()
    {
        lock (_lock)
        {
            _chatMessages.Clear();
            _scrollOffset = 0;
        }
        Render();
    }

    public void Render()
    {
        lock (_lock)
        {
            try
            {
                var height = Console.WindowHeight;
                var width = Console.WindowWidth;

                if (height != _lastHeight || width != _lastWidth)
                {
                    Console.Clear();
                    _lastHeight = height;
                    _lastWidth = width;
                }

                var statusHeight = 1;
                var inputHeight = 1;
                var separatorHeight = 1;
                var chatHeight = height - statusHeight - inputHeight - separatorHeight - 1;

                RenderChatArea(chatHeight, width);

                var separatorLine = chatHeight;
                Console.SetCursorPosition(0, separatorLine);
                Console.Write(new string('─', width));

                var statusLine = separatorLine + 1;
                RenderStatusArea(statusLine, width);

                var inputLine = statusLine + 1;
                RenderInputArea(inputLine, width);
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }
    }

    private void RenderChatArea(int height, int width)
    {
        var displayLines = new List<string>();

        foreach (var message in _chatMessages)
        {
            var lines = WrapText(message, width);
            displayLines.AddRange(lines);
        }

        var totalLines = displayLines.Count;
        var maxOffset = Math.Max(0, totalLines - height);
        _scrollOffset = Math.Clamp(_scrollOffset, 0, maxOffset);

        var startLine = totalLines - height - _scrollOffset;
        if (startLine < 0) startLine = 0;

        var endLine = Math.Min(totalLines, startLine + height);

        for (int i = 0; i < height; i++)
        {
            Console.SetCursorPosition(0, i);
            var lineIndex = startLine + i;
            if (lineIndex < endLine)
            {
                var line = displayLines[lineIndex];
                Console.Write(line.PadRight(width));
            }
            else
            {
                Console.Write(new string(' ', width));
            }
        }
    }

    private void RenderStatusArea(int line, int width)
    {
        Console.SetCursorPosition(0, line);

        var leftPart = _processingStatus;

        var rightPart = _workingDirectory;

        var availableWidth = width - leftPart.Length - rightPart.Length;
        var spacing = availableWidth > 0 ? new string(' ', availableWidth) : "";

        var statusDisplay = leftPart + spacing + rightPart;
        if (statusDisplay.Length > width)
        {
            statusDisplay = statusDisplay.Substring(0, width);
        }
        else
        {
            statusDisplay = statusDisplay.PadRight(width);
        }

        Console.Write(statusDisplay);
    }

    private void RenderInputArea(int line, int width)
    {
        Console.SetCursorPosition(0, line);

        var display = "> " + _currentInput;

        if (display.Length > width)
        {
            var start = Math.Max(0, display.Length - width);
            display = display.Substring(start);
        }

        Console.Write(display.PadRight(width));

        var cursorX = Math.Min(2 + _cursorPosition, width - 1);
        Console.SetCursorPosition(cursorX, line);
    }

    private List<string> WrapText(string text, int width)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            result.Add("");
            return result;
        }

        var lines = text.Split('\n');
        foreach (var line in lines)
        {
            if (line.Length <= width)
            {
                result.Add(line);
            }
            else
            {
                var currentLine = "";
                var words = line.Split(' ');

                foreach (var word in words)
                {
                    if (currentLine.Length + word.Length + 1 <= width)
                    {
                        if (currentLine.Length > 0)
                            currentLine += " ";
                        currentLine += word;
                    }
                    else
                    {
                        if (currentLine.Length > 0)
                            result.Add(currentLine);
                        currentLine = word;
                    }
                }

                if (currentLine.Length > 0)
                    result.Add(currentLine);
            }
        }

        return result;
    }

    public void ScrollUp()
    {
        lock (_lock)
        {
            _scrollOffset = Math.Min(_scrollOffset + 1, GetMaxScrollOffset());
        }
        Render();
    }

    public void ScrollDown()
    {
        lock (_lock)
        {
            _scrollOffset = Math.Max(_scrollOffset - 1, 0);
        }
        Render();
    }

    private int GetMaxScrollOffset()
    {
        var height = Console.WindowHeight;
        var chatHeight = height - 3;
        var totalLines = 0;

        foreach (var message in _chatMessages)
        {
            totalLines += WrapText(message, Console.WindowWidth).Count;
        }

        return Math.Max(0, totalLines - chatHeight);
    }

    public string GetCurrentInput()
    {
        lock (_lock)
        {
            return _currentInput;
        }
    }

    public void ClearInput()
    {
        lock (_lock)
        {
            _currentInput = "";
            _cursorPosition = 0;
        }
        Render();
    }

    public void HandleInput(ConsoleKeyInfo keyInfo)
    {
        lock (_lock)
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.Backspace:
                    if (_cursorPosition > 0)
                    {
                        _currentInput = _currentInput.Remove(_cursorPosition - 1, 1);
                        _cursorPosition--;
                    }
                    break;

                case ConsoleKey.Delete:
                    if (_cursorPosition < _currentInput.Length)
                    {
                        _currentInput = _currentInput.Remove(_cursorPosition, 1);
                    }
                    break;

                case ConsoleKey.LeftArrow:
                    _cursorPosition = Math.Max(0, _cursorPosition - 1);
                    break;

                case ConsoleKey.RightArrow:
                    _cursorPosition = Math.Min(_currentInput.Length, _cursorPosition + 1);
                    break;

                case ConsoleKey.Home:
                    _cursorPosition = 0;
                    break;

                case ConsoleKey.End:
                    _cursorPosition = _currentInput.Length;
                    break;

                case ConsoleKey.UpArrow:
                    if ((keyInfo.Modifiers & ConsoleModifiers.Control) != 0)
                    {
                        ScrollUp();
                        return;
                    }
                    break;

                case ConsoleKey.DownArrow:
                    if ((keyInfo.Modifiers & ConsoleModifiers.Control) != 0)
                    {
                        ScrollDown();
                        return;
                    }
                    break;

                default:
                    if (!char.IsControl(keyInfo.KeyChar))
                    {
                        _currentInput = _currentInput.Insert(_cursorPosition, keyInfo.KeyChar.ToString());
                        _cursorPosition++;
                    }
                    break;
            }
        }
        Render();
    }

    public void Stop()
    {
        _isRunning = false;
    }

    public bool IsRunning => _isRunning;
}
