using Terminal.Gui;

namespace GrokCLI.UI;

public class ChatWindow
{
    private readonly Window _window;
    private readonly TextView _chatView;
    private readonly ChatTextField _inputView;
    private readonly Label _processingLabel;
    private StatusBar? _statusBar;
    private ChatViewController? _controller;

    public ChatWindow()
    {
        // Main window
        _window = new Window("Grok TUI - Agentic Mode (Ctrl+Q to exit)")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        // Conversation area (scrollable)
        _chatView = new TextView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(2),
            ReadOnly = true,
            WordWrap = true,
            CanFocus = false
        };

        // Input box (single-line) - using custom TextField
        _inputView = new ChatTextField()
        {
            X = 0,
            Y = Pos.Bottom(_chatView) + 1,
            Width = Dim.Fill(),
            Height = 1,
            CanFocus = true,
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                Focus = new Terminal.Gui.Attribute(Color.White, Color.Black),
                HotNormal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                HotFocus = new Terminal.Gui.Attribute(Color.White, Color.Black)
            }
        };

        _processingLabel = new Label("")
        {
            X = 0,
            Y = Pos.Bottom(_chatView),
            Width = Dim.Fill(),
            Height = 1,
            TextAlignment = TextAlignment.Left
        };
    }

    public void Initialize(Toplevel top, ChatViewController controller, bool isLocked)
    {
        _controller = controller;

        if (!isLocked)
        {
            _inputView.OnEnterPressed += async () =>
            {
                await _controller.SendMessageAsync();
            };

            _inputView.OnScrollRequested += direction => ScrollChat(direction);
        }
        else
        {
            _inputView.ReadOnly = true;
            _inputView.CanFocus = false;
        }

        // Status bar
        _statusBar = isLocked
            ? new StatusBar(new StatusItem[]
            {
                new StatusItem(Key.CtrlMask | Key.Q, "~^Q~ Exit", () => Application.RequestStop())
            })
            : new StatusBar(new StatusItem[]
            {
                new StatusItem(Key.CtrlMask | Key.Q, "~^Q~ Exit", () => Application.RequestStop()),
                new StatusItem(Key.CtrlMask | Key.L, "~^L~ Clear", () => _controller.ClearChat()),
                new StatusItem(Key.Null, "Scroll: Ctrl+↑ / Ctrl+↓", null),
                new StatusItem(Key.Null, "Model: grok-4-1-fast-reasoning", null)
            });

        _window.Add(_chatView, _processingLabel, _inputView);
        top.Add(_window, _statusBar);

        // Set initial focus on the input field when the window is ready
        if (!isLocked)
        {
            top.Ready += () =>
            {
                _inputView.SetFocus();
            };
        }
    }

    public TextView ChatView => _chatView;
    public TextField InputView => _inputView;
    public Label ProcessingLabel => _processingLabel;

    private void ScrollChat(int direction)
    {
        // direction: -1 (PageUp / Ctrl+Up), 1 (PageDown / Ctrl+Down)
        var delta = direction; // smooth scroll, one line at a time

        var totalLines = GetTotalLines();
        var maxTop = Math.Max(0, totalLines - _chatView.Frame.Height);

        var newTop = Math.Clamp(_chatView.TopRow + delta, 0, maxTop);
        _chatView.TopRow = newTop;
        _chatView.SetNeedsDisplay();
    }

    private int GetTotalLines()
    {
        var text = _chatView.Text?.ToString();
        if (string.IsNullOrEmpty(text))
            return 0;
        return text.Split('\n').Length;
    }
}
