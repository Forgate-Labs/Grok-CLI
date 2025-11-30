using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using GrokCLI.Models;
using GrokCLI.Services;
using OpenAI.Chat;
using Terminal.Gui;
using Microsoft.Extensions.DependencyInjection;
using GuiAttribute = Terminal.Gui.Attribute;

namespace GrokCLI.UI;

public class TerminalGuiChatViewController : IDisposable
{
    private readonly IChatService _chatService;
    private readonly List<ChatMessage> _conversation;
    private readonly IServiceProvider _services;
    private readonly bool _isEnabled;
    private ChatDisplayMode _displayMode;
    private readonly string _version;
    private readonly string _configPath;
    private readonly Stopwatch _sessionStopwatch;
    private const int PlanExpandedHeight = 7;
    private const int PlanCollapsedHeight = 0;
    private const int InputHeight = 2;
    private const int StatusHeight = 1;
    private bool _welcomeShown;
    private Toplevel? _top;
    private Window? _window;
    private TextView? _historyView;
    private TextField? _inputView;
    private Label? _statusLabel;
    private FrameView? _planFrame;
    private Label? _planTitleLabel;
    private TextView? _planItemsView;
    private CancellationTokenSource? _cts;

    public TerminalGuiChatViewController(
        IChatService chatService,
        IServiceProvider services,
        bool isEnabled,
        ChatDisplayMode displayMode,
        string version,
        string configPath)
    {
        _chatService = chatService;
        _services = services;
        _conversation = new List<ChatMessage>();
        _isEnabled = isEnabled;
        _displayMode = displayMode;
        _version = version;
        _configPath = configPath;
        _sessionStopwatch = Stopwatch.StartNew();

        _chatService.OnTextReceived += OnTextReceived;
        _chatService.OnToolCalled += OnToolCalled;
        _chatService.OnToolResult += OnToolResult;
    }

    public void Run()
    {
        if (!_isEnabled)
            return;

        Application.Init();

        BuildUI();

        Application.Run(_top!);
    }

    public void Dispose()
    {
        _chatService.OnTextReceived -= OnTextReceived;
        _chatService.OnToolCalled -= OnToolCalled;
        _chatService.OnToolResult -= OnToolResult;
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
        }
        Application.Shutdown();
    }

    private void BuildUI()
    {
        var planHeight = PlanCollapsedHeight;
        var reservedHeight = planHeight + InputHeight + StatusHeight;

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

        _top = new Toplevel
        {
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        _window = new Window
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Title = $"Grok CLI {_version} - {(_displayMode == ChatDisplayMode.Debug ? "Debug" : "Normal")}"
        };
        _window.ColorScheme = baseScheme;

        _historyView = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(reservedHeight),
            ReadOnly = true,
            WordWrap = true,
            ColorScheme = baseScheme
        };

        _statusLabel = new Label
        {
            X = 0,
            Y = Pos.Bottom(_historyView),
            Width = Dim.Fill(),
            Height = StatusHeight,
            Text = "Ready",
            ColorScheme = baseScheme
        };

        _planFrame = new FrameView
        {
            X = 0,
            Y = Pos.Bottom(_statusLabel),
            Width = Dim.Fill(),
            Height = planHeight,
            Title = "Plan",
            CanFocus = false,
            ColorScheme = baseScheme
        };

        _planTitleLabel = new Label
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
            Text = "Waiting for plan",
            CanFocus = false,
            ColorScheme = baseScheme
        };

        _planItemsView = new TextView
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

        _planFrame.Add(_planTitleLabel, _planItemsView);

        _inputView = new TextField
        {
            X = 0,
            Y = Pos.Bottom(_planFrame),
            Width = Dim.Fill(),
            Height = InputHeight,
            CanFocus = true,
            ReadOnly = false
        };
        var inputColors = new GuiAttribute(Color.White, Color.Green);
        _inputView.ColorScheme = new ColorScheme
        {
            Normal = inputColors,
            Focus = inputColors,
            HotNormal = inputColors,
            HotFocus = inputColors,
            Disabled = inputColors
        };

        _inputView.KeyDown += InputViewOnKeyDown;

        var menu = new MenuBar
        {
            Menus = new[]
            {
                new MenuBarItem("_File", new[]
                {
                    new MenuItem("_Quit", "", () => Application.RequestStop())
                }),
                new MenuBarItem("_Mode", new[]
                {
                    new MenuItem("Debug", "", () => SetMode(ChatDisplayMode.Debug)),
                    new MenuItem("Normal", "", () => SetMode(ChatDisplayMode.Normal))
                })
            }
        };

        _window!.Add(_historyView, _statusLabel, _planFrame, _inputView);
        _top!.Add(menu, _window);
        Application.MainLoop?.AddIdle(() =>
        {
            if (_welcomeShown)
                return false;
            _welcomeShown = true;
            AppendWelcomeMessage();
            return false;
        });
        _inputView.SetFocus();
    }

    private void AppendWelcomeMessage()
    {
        if (_historyView == null)
            return;

        var builder = new StringBuilder();
        builder.AppendLine($"Grok CLI {_version} - Agentic Mode");
        builder.AppendLine($"Mode: {_displayMode} (type \"debug\" or \"normal\" to switch)");
        builder.AppendLine("Commands: Enter (send) | Ctrl+J (newline) | Esc (clear input or cancel run) | debug/normal (switch mode) | cmd <command> or /cmd <command> (run shell) | clear or /clear (clear screen) | logout (clear API key) | Ctrl+C (exit)");
        builder.AppendLine($"Config: {_configPath}");
        builder.AppendLine("Model: grok-4-1-fast-reasoning");
        builder.AppendLine();

        _historyView.Text = builder.ToString();
        _historyView.MoveEnd();
    }

    private void InputViewOnKeyDown(View.KeyEventEventArgs args)
    {
        var key = args.KeyEvent.Key;

        if (key == Key.Esc)
        {
            if (_cts != null)
            {
                _cts.Cancel();
            }
            else if (_inputView != null)
            {
                _inputView.Text = "";
            }
        }

        if (key == Key.Enter)
        {
            _ = ProcessInputAsync();
            if (_inputView != null)
                _inputView.Text = "";
        }
    }

    private async Task ProcessInputAsync()
    {
        if (_cts != null)
            return;

        var text = _inputView?.Text?.ToString() ?? string.Empty;
        var userText = text.Trim();

        if (string.IsNullOrWhiteSpace(userText))
            return;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        if (HandleLocalCommands(userText))
        {
            _cts.Dispose();
            _cts = null;
            SetStatus("Ready");
            return;
        }

        AppendHistory($"\n[You]: {userText}\n");
        SetStatus("thinking...");

        _inputView!.Text = "";

        try
        {
            await _chatService.SendMessageAsync(userText, _conversation, token);
            AppendHistory("\n");
        }
        catch (OperationCanceledException)
        {
            AppendHistory("\n[canceled]\n");
        }
        catch (Exception ex)
        {
            AppendHistory($"\n[error] {ex.Message}\n");
        }
        finally
        {
            if (_cts != null)
            {
                _cts.Dispose();
                _cts = null;
            }
            SetStatus("Ready");
        }
    }

    private bool HandleLocalCommands(string userText)
    {
        if (string.Equals(userText, "clear", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(userText, "/clear", StringComparison.OrdinalIgnoreCase))
        {
            if (_historyView != null)
                _historyView.Text = "";
            ClearPlan();
            return true;
        }

        if (string.Equals(userText, "debug", StringComparison.OrdinalIgnoreCase))
        {
            SetMode(ChatDisplayMode.Debug);
            return true;
        }

        if (string.Equals(userText, "normal", StringComparison.OrdinalIgnoreCase))
        {
            SetMode(ChatDisplayMode.Normal);
            return true;
        }

        if (userText.StartsWith("/cmd ", StringComparison.OrdinalIgnoreCase) ||
            userText.StartsWith("cmd ", StringComparison.OrdinalIgnoreCase))
        {
            var command = userText.StartsWith("/cmd ", StringComparison.OrdinalIgnoreCase)
                ? userText.Substring(5).Trim()
                : userText.Substring(4).Trim();

            if (!string.IsNullOrWhiteSpace(command))
            {
                _ = Task.Run(async () =>
                {
                    SetStatus("executing...");
                    await RunShellCommand(command);
                    SetStatus("Ready");
                });
            }

            return true;
        }

        return false;
    }

    private async Task RunShellCommand(string command)
    {
        try
        {
            AppendCommandOutput($"\n● Run({command})\n");

            var shellExecutor = _services.GetRequiredService<IShellExecutor>();
            var result = await shellExecutor.ExecuteAsync(command, 300);

            var success = result.ExitCode == 0;
            var body = success
                ? (!string.IsNullOrWhiteSpace(result.Output)
                    ? result.Output
                    : "Completed with no output")
                : (!string.IsNullOrWhiteSpace(result.Error)
                    ? result.Error
                    : (!string.IsNullOrWhiteSpace(result.Output)
                        ? result.Output
                        : "Command failed"));

            AppendCommandOutput($"{body}\n");
        }
        catch (Exception ex)
        {
            AppendCommandOutput($"Command error: {ex.Message}\n");
        }
    }

    private void OnTextReceived(string text)
    {
        EnqueueUi(() => AppendHistory(text));
    }

    private void OnToolCalled(ToolCallEvent toolEvent)
    {
        if (_displayMode != ChatDisplayMode.Debug)
            return;

        EnqueueUi(() =>
        {
            var args = string.IsNullOrWhiteSpace(toolEvent.ArgumentsJson)
                ? ""
                : toolEvent.ArgumentsJson;
            AppendHistory($"\n[tool] {toolEvent.ToolName} {args}\n");
        });
    }

    private void OnToolResult(ToolResultEvent toolEvent)
    {
        if (string.Equals(toolEvent.ToolName, "set_plan", StringComparison.OrdinalIgnoreCase))
        {
            HandlePlan(toolEvent);
            return;
        }

        if (string.Equals(toolEvent.ToolName, "workflow_done", StringComparison.OrdinalIgnoreCase))
        {
            EnqueueUi(ClearPlan);
        }

        if (_displayMode == ChatDisplayMode.Debug)
        {
            EnqueueUi(() =>
            {
                var output = toolEvent.Result.Success
                    ? toolEvent.Result.Output
                    : toolEvent.Result.Error;
                AppendHistory($"\n[result] {toolEvent.ToolName}: {output}\n");
            });
        }
    }

    private void HandlePlan(ToolResultEvent toolEvent)
    {
        var payload = ParsePlan(toolEvent.ArgumentsJson);

        if (payload == null || payload.Items.Count == 0)
        {
            EnqueueUi(ClearPlan);
            return;
        }

        EnqueueUi(() =>
        {
            SetPlanVisibility(true);
            RenderPlan(payload);
        });
    }

    private PlanPayload? ParsePlan(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var title = root.TryGetProperty("title", out var titleProp)
                ? titleProp.GetString()
                : null;

            if (!root.TryGetProperty("items", out var itemsProp) || itemsProp.ValueKind != JsonValueKind.Array)
                return null;

            var items = new List<PlanEntry>();

            foreach (var item in itemsProp.EnumerateArray())
            {
                if (!item.TryGetProperty("title", out var tProp))
                    continue;

                var itemTitle = tProp.GetString();
                if (string.IsNullOrWhiteSpace(itemTitle))
                    continue;

                var status = item.TryGetProperty("status", out var sProp)
                    ? sProp.GetString()
                    : null;

                items.Add(new PlanEntry(itemTitle, NormalizeStatus(status)));
            }

            return new PlanPayload(title, items);
        }
        catch
        {
            return null;
        }
    }

    private PlanStatus NormalizeStatus(string? value)
    {
        var normalized = value?.Replace("-", "_").ToLowerInvariant();

        return normalized switch
        {
            "done" or "complete" or "completed" or "finished" or "ok" or "success" => PlanStatus.Done,
            "in_progress" or "progress" or "doing" or "active" or "working" => PlanStatus.InProgress,
            _ => PlanStatus.Pending
        };
    }

    private void RenderPlan(PlanPayload payload)
    {
        if (_planFrame == null || _planTitleLabel == null || _planItemsView == null)
            return;

        _planFrame.Title = string.IsNullOrWhiteSpace(payload.Title) ? "Plan" : payload.Title;

        _planTitleLabel.Text = "";
        var lines = new List<string>();

        foreach (var item in payload.Items)
        {
            var symbol = item.Status switch
            {
                PlanStatus.Done => "✔",
                PlanStatus.InProgress => "…",
                _ => "□"
            };

            lines.Add($"{symbol} {item.Title}");
        }

        _planItemsView.Text = string.Join("\n", lines);
    }

    private void ClearPlan()
    {
        if (_planFrame == null || _planItemsView == null || _planTitleLabel == null)
            return;

        _planFrame.Title = "Plan";
        _planTitleLabel.Text = "Waiting for plan";
        _planItemsView.Text = "";
        SetPlanVisibility(false);
    }

    private void SetStatus(string text)
    {
        EnqueueUi(() =>
        {
            if (_statusLabel != null)
                _statusLabel.Text = text;
        });
    }

    private void SetMode(ChatDisplayMode mode)
    {
        if (_window == null)
            return;

        _displayMode = mode;
        _window.Title = $"Grok CLI {_version} - {(mode == ChatDisplayMode.Debug ? "Debug" : "Normal")}";
    }

    private void AppendHistory(string text)
    {
        if (_historyView == null)
            return;

        var current = _historyView.Text?.ToString() ?? "";
        _historyView.Text = current + text;
        _historyView.MoveEnd();
    }

    private void AppendCommandOutput(string text)
    {
        AppendHistory(text);
    }

    private void EnqueueUi(Action action)
    {
        if (Application.MainLoop != null)
        {
            Application.MainLoop.Invoke(action);
        }
        else
        {
            action();
        }
    }

    private void SetPlanVisibility(bool visible)
    {
        if (_planFrame == null || _historyView == null || _window == null)
            return;

        var planHeight = visible ? PlanExpandedHeight : PlanCollapsedHeight;
        var reservedHeight = planHeight + InputHeight + StatusHeight;

        _planFrame.Visible = visible;
        _planFrame.Height = planHeight;
        _historyView.Height = Dim.Fill(reservedHeight);

        _window.SetNeedsDisplay();
    }

    private sealed record PlanPayload(string? Title, List<PlanEntry> Items);
}
