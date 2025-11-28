using Terminal.Gui;

namespace GrokCLI.UI;

/// <summary>
/// Custom TextField that captures the Enter key
/// </summary>
public class ChatTextField : TextField
{
    public event Action? OnEnterPressed;
    public event Action<int>? OnScrollRequested;

    public override bool ProcessKey(KeyEvent kb)
    {
        if (kb.Key == Key.Enter)
        {
            OnEnterPressed?.Invoke();
            return true; // Mark as handled
        }

        if (kb.Key == Key.PageUp || kb.Key == (Key.CtrlMask | Key.CursorUp))
        {
            OnScrollRequested?.Invoke(-1);
            return true;
        }

        if (kb.Key == Key.PageDown || kb.Key == (Key.CtrlMask | Key.CursorDown))
        {
            OnScrollRequested?.Invoke(1);
            return true;
        }

        return base.ProcessKey(kb);
    }
}
