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
            // Clear current input line if there's one being displayed
            if (_inputLineActive)
            {
                ClearCurrentInputLine();
            }

            // Write the message
            Console.WriteLine(message);

            // Redraw input line only if active
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
            // Just write the text without redrawing input
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
        // Save cursor position
        var currentLine = Console.CursorTop;
        var currentCol = Console.CursorLeft;

        // Move to beginning of line
        Console.CursorLeft = 0;

        // Clear the line
        Console.Write(new string(' ', Console.WindowWidth));

        // Move back to beginning
        Console.CursorLeft = 0;
    }

    private void DrawInputLine()
    {
        var statusPart = string.IsNullOrEmpty(_processingStatus) ? "" : $"{_processingStatus} | ";
        var prompt = $"{statusPart}{_workingDirectory} > ";

        Console.Write(prompt);
        _inputStartColumn = prompt.Length;

        Console.Write(_currentInput);

        // Position cursor
        Console.CursorLeft = _inputStartColumn + _cursorPosition;
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
            // Move to new line after submitting
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
