using Terminal.Gui;

namespace GrokCLI.UI;

public class Popover : Window
{
    public Popover(string title, int width, int height) : base(title)
    {
        Width = width;
        Height = height;
        X = Pos.Center();
        Y = Pos.Center();
        ColorScheme = Colors.Dialog;
    }
}
