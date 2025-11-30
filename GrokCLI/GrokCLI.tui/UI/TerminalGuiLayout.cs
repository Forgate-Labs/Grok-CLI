using Terminal.Gui;

namespace GrokCLI.UI;

public sealed class TerminalGuiLayout
{
    public TerminalGuiLayout(
        Toplevel top,
        Window window,
        ScrollView historyView,
        Label statusLabel,
        FrameView planFrame,
        Label planTitleLabel,
        TextView planItemsView,
        FrameView inputFrame,
        TextField inputView,
        MenuItem modeDebug,
        MenuItem modeNormal,
        MenuItem modelReasoning,
        MenuItem modelNonReasoning,
        MenuItem modelCodeFast)
    {
        Top = top;
        Window = window;
        HistoryView = historyView;
        StatusLabel = statusLabel;
        PlanFrame = planFrame;
        PlanTitleLabel = planTitleLabel;
        PlanItemsView = planItemsView;
        InputFrame = inputFrame;
        InputView = inputView;
        ModeDebug = modeDebug;
        ModeNormal = modeNormal;
        ModelReasoning = modelReasoning;
        ModelNonReasoning = modelNonReasoning;
        ModelCodeFast = modelCodeFast;
    }

    public Toplevel Top { get; }
    public Window Window { get; }
    public ScrollView HistoryView { get; }
    public Label StatusLabel { get; }
    public FrameView PlanFrame { get; }
    public Label PlanTitleLabel { get; }
    public TextView PlanItemsView { get; }
    public FrameView InputFrame { get; }
    public TextField InputView { get; }
    public MenuItem ModeDebug { get; }
    public MenuItem ModeNormal { get; }
    public MenuItem ModelReasoning { get; }
    public MenuItem ModelNonReasoning { get; }
    public MenuItem ModelCodeFast { get; }
}
