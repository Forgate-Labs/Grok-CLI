using Terminal.Gui;
using GuiAttribute = Terminal.Gui.Attribute;

namespace GrokCLI.UI;

public interface ITerminalGuiLayoutFactory
{
    TerminalGuiLayout Create(
        ChatDisplayMode displayMode,
        bool hasApiKey,
        string windowTitle,
        string model,
        TerminalGuiMenuActions menuActions,
        int planCollapsedHeight,
        int planExpandedHeight,
        int inputHeight,
        int inputFrameBorder,
        int statusHeight);
}

public sealed class TerminalGuiLayoutFactory : ITerminalGuiLayoutFactory
{
    public TerminalGuiLayout Create(
        ChatDisplayMode displayMode,
        bool hasApiKey,
        string windowTitle,
        string model,
        TerminalGuiMenuActions menuActions,
        int planCollapsedHeight,
        int planExpandedHeight,
        int inputHeight,
        int inputFrameBorder,
        int statusHeight)
    {
        var planHeight = planCollapsedHeight;
        var inputFrameHeight = inputHeight + inputFrameBorder;
        var reservedHeight = planHeight + inputHeight + inputFrameBorder + statusHeight;

        var baseNormal = new GuiAttribute(Color.White, Color.Black);
        var baseFocus = new GuiAttribute(Color.BrightYellow, Color.Black);
        var baseHotNormal = new GuiAttribute(Color.Cyan, Color.Black);
        var baseHotFocus = new GuiAttribute(Color.BrightCyan, Color.Black);
        var baseDisabled = new GuiAttribute(Color.Gray, Color.Black);
        var baseScheme = new ColorScheme
        {
            Normal = baseNormal,
            Focus = baseFocus,
            HotNormal = baseHotNormal,
            HotFocus = baseHotFocus,
            Disabled = baseDisabled
        };

        Colors.TopLevel = baseScheme;
        Colors.Base = baseScheme;
        Colors.Dialog = baseScheme;
        Colors.Menu = new ColorScheme
        {
            Normal = baseNormal,
            Focus = baseFocus,
            HotNormal = baseHotNormal,
            HotFocus = baseHotFocus,
            Disabled = baseDisabled
        };
        Colors.Error = new ColorScheme
        {
            Normal = new GuiAttribute(Color.BrightRed, Color.Black),
            Focus = new GuiAttribute(Color.BrightRed, Color.Black),
            HotNormal = new GuiAttribute(Color.BrightRed, Color.Black),
            HotFocus = new GuiAttribute(Color.BrightRed, Color.Black),
            Disabled = baseDisabled
        };

        var top = new Toplevel
        {
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        var window = new Window
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Title = windowTitle
        };
        window.ColorScheme = baseScheme;

        var historyView = new ScrollView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(reservedHeight),
            ColorScheme = baseScheme,
            ShowVerticalScrollIndicator = true,
            ShowHorizontalScrollIndicator = false,
            AutoHideScrollBars = true
        };

        var statusLabel = new Label
        {
            X = 0,
            Y = Pos.Bottom(historyView),
            Width = Dim.Fill(),
            Height = statusHeight,
            Text = hasApiKey ? "Ready" : "API key required",
            ColorScheme = baseScheme
        };

        var planFrame = new FrameView
        {
            X = 0,
            Y = Pos.Bottom(statusLabel),
            Width = Dim.Fill(),
            Height = planHeight,
            Title = "Plan",
            CanFocus = false,
            ColorScheme = baseScheme,
            Visible = false
        };

        var planTitleLabel = new Label
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
            Text = "Waiting for plan",
            CanFocus = false,
            ColorScheme = baseScheme
        };

        var planItemsView = new TextView
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            WordWrap = true,
            CanFocus = false,
            ColorScheme = baseScheme
        };

        planFrame.Add(planTitleLabel, planItemsView);

        var inputFrame = new FrameView
        {
            X = 0,
            Y = Pos.Bottom(planFrame),
            Width = Dim.Fill(),
            Height = inputFrameHeight,
            Title = "Input - Press Esc to clear input or cancel operation",
            CanFocus = false,
            ColorScheme = baseScheme
        };

        var inputView = new TextField
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = inputHeight,
            CanFocus = true,
            ReadOnly = false,
            ColorScheme = baseScheme
        };

        inputFrame.Add(inputView);

        var modeDebug = new MenuItem("Debug", "", () => menuActions.SetMode(ChatDisplayMode.Debug))
        {
            Checked = displayMode == ChatDisplayMode.Debug,
            CheckType = MenuItemCheckStyle.Radio
        };

        var modeNormal = new MenuItem("Normal", "", () => menuActions.SetMode(ChatDisplayMode.Normal))
        {
            Checked = displayMode == ChatDisplayMode.Normal,
            CheckType = MenuItemCheckStyle.Radio
        };

        var modelReasoning = new MenuItem("grok-4-1-fast-reasoning", "", () => menuActions.SetModel("grok-4-1-fast-reasoning"))
        {
            Checked = model == "grok-4-1-fast-reasoning",
            CheckType = MenuItemCheckStyle.Radio
        };

        var modelNonReasoning = new MenuItem("grok-4-1-fast-non-reasoning", "", () => menuActions.SetModel("grok-4-1-fast-non-reasoning"))
        {
            Checked = model == "grok-4-1-fast-non-reasoning",
            CheckType = MenuItemCheckStyle.Radio
        };

        var modelCodeFast = new MenuItem("grok-code-fast-1", "", () => menuActions.SetModel("grok-code-fast-1"))
        {
            Checked = model == "grok-code-fast-1",
            CheckType = MenuItemCheckStyle.Radio
        };

        var menu = new MenuBar
        {
            Menus = new[]
            {
                new MenuBarItem("_Grok", new[]
                {
                    new MenuItem("_New", "", menuActions.StartNewSession),
                    new MenuItem("_Logout", "", menuActions.Logout),
                    new MenuItem("_Quit", "", menuActions.Quit)
                }),
                new MenuBarItem("_Mode", new[]
                {
                    modeDebug,
                    modeNormal
                }),
                new MenuBarItem("_Models", new[]
                {
                    modelReasoning,
                    modelNonReasoning,
                    modelCodeFast
                })
            }
        };

        window.Add(historyView, statusLabel, planFrame, inputFrame);
        top.Add(menu, window);

        return new TerminalGuiLayout(
            top,
            window,
            historyView,
            statusLabel,
            planFrame,
            planTitleLabel,
            planItemsView,
            inputFrame,
            inputView,
            modeDebug,
            modeNormal,
            modelReasoning,
            modelNonReasoning,
            modelCodeFast);
    }
}

public sealed record TerminalGuiMenuActions(
    Action StartNewSession,
    Action Logout,
    Action Quit,
    Action<ChatDisplayMode> SetMode,
    Action<string> SetModel);
