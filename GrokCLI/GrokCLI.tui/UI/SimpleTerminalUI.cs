using System.Text;

namespace GrokCLI.UI;

public class SimpleTerminalUI
{
    private readonly object _lock = new();
    private string _currentInput = "";
    private int _cursorPosition = 0;
    private string _processingStatus = "";
    private readonly string _workingDirectory;
    private bool _isRunning = true;
    private int _inputStartColumn = 0;
    private bool _inputLineActive = true;
    private int _inputStartLine = 0;

    public SimpleTerminalUI()
    {
        _workingDirectory = Directory.GetCurrentDirectory();
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        Console.CursorVisible = true;
    }

    public void WriteLine(string message)
    {
        lock (_lock)
        {
            if (_inputLineActive)
            {
                ClearCurrentInputLine();
            }

            Console.WriteLine(message);

            if (_inputLineActive)
            {
                DrawInputLine();
            }
        }
    }

    public void Write(string text)
    {
        lock (_lock)
        {
            Console.Write(text);
        }
    }

    public void SetProcessingStatus(string status)
    {
        lock (_lock)
        {
            _processingStatus = status;
            if (_inputLineActive)
            {
                UpdateInputLine();
            }
        }
    }

    public void UpdateInputLine()
    {
        lock (_lock)
        {
            if (_inputLineActive)
            {
                ClearCurrentInputLine();
                DrawInputLine();
            }
        }
    }

    private void ClearCurrentInputLine()
    {
        var lines = _currentInput.Split('\n');
        var lineCount = lines.Length;

        for (int i = 0; i < lineCount; i++)
        {
            Console.CursorTop = _inputStartLine + i;
            Console.CursorLeft = 0;
            Console.Write(new string(' ', Console.WindowWidth));
        }

        Console.CursorTop = _inputStartLine;
        Console.CursorLeft = 0;
    }

    private void DrawInputLine()
    {
        _inputStartLine = Console.CursorTop;

        var statusPart = string.IsNullOrEmpty(_processingStatus) ? "" : $"{_processingStatus} | ";
        var prompt = $"[GROK]{statusPart}{_workingDirectory} > ";

        Console.Write(prompt);
        _inputStartColumn = prompt.Length;

        var lines = _currentInput.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            if (i == 0)
            {
                Console.Write(lines[i]);
            }
            else
            {
                Console.WriteLine();
                Console.Write("  " + lines[i]);
            }
        }

        PositionCursor();
    }

    private void PositionCursor()
    {
        var textBeforeCursor = _currentInput.Substring(0, _cursorPosition);
        var lines = textBeforeCursor.Split('\n');
        var lineIndex = lines.Length - 1;
        var columnInLine = lines[lineIndex].Length;

        Console.CursorTop = _inputStartLine + lineIndex;

        if (lineIndex == 0)
        {
            Console.CursorLeft = _inputStartColumn + columnInLine;
        }
        else
        {
            Console.CursorLeft = 2 + columnInLine;
        }
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
            UpdateInputLine();
        }
    }

    public void InsertNewline()
    {
        lock (_lock)
        {
            _currentInput = _currentInput.Insert(_cursorPosition, "\n");
            _cursorPosition++;
            UpdateInputLine();
        }
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

                default:
                    if (!char.IsControl(keyInfo.KeyChar))
                    {
                        _currentInput = _currentInput.Insert(_cursorPosition, keyInfo.KeyChar.ToString());
                        _cursorPosition++;
                    }
                    break;
            }

            UpdateInputLine();
        }
    }

    public void SubmitInput()
    {
        lock (_lock)
        {
            Console.WriteLine();
        }
    }

    public void HideInputLine()
    {
        lock (_lock)
        {
            if (_inputLineActive)
            {
                ClearCurrentInputLine();
                _inputLineActive = false;
            }
        }
    }

    public void ShowInputLine()
    {
        lock (_lock)
        {
            if (!_inputLineActive)
            {
                _inputLineActive = true;
                DrawInputLine();
            }
        }
    }

    public void Stop()
    {
        _isRunning = false;
    }

    public bool IsRunning => _isRunning;
}
