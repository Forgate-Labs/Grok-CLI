using System;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GrokCLI.Models;
using GrokCLI.Services;
using OpenAI.Chat;
using Terminal.Gui;
using Microsoft.Extensions.DependencyInjection;

namespace GrokCLI.UI;

public class TerminalGuiChatViewController : IDisposable
{
    private IChatService _chatService = null!;
    private readonly List<ChatMessage> _conversation;
    private readonly IServiceProvider _services;
    private readonly Func<string, IChatService> _chatServiceFactory;
    private readonly Action<string?> _persistApiKey;
    private readonly ITerminalGuiLayoutFactory _layoutFactory;
    private readonly ToolSummaryBuilder _toolSummaryBuilder;
    private readonly Stopwatch _sessionStopwatch;
    private readonly string _version;
    private readonly string _configPath;
    private bool _hasApiKey;
    private ChatDisplayMode _displayMode;
    private string _model = "grok-4-1-fast-reasoning";
    private ChatTokenUsage? _lastUsage;
    private bool _welcomeShown;
    private bool _apiKeyPromptOpen;
    private CancellationTokenSource? _cts;
    private TerminalGuiLayout? _layout;
    private HistoryViewManager? _historyManager;
    private PlanPresenter? _planPresenter;
    private ThinkingStatusPresenter? _statusPresenter;
    private bool _assistantBlockOpen;
    private const int PlanExpandedHeight = 7;
    private const int PlanCollapsedHeight = 0;
    private const int InputHeight = 5;
    private const int InputFrameBorder = 2;
    private const int StatusHeight = 1;

    public TerminalGuiChatViewController(
        IChatService chatService,
        IServiceProvider services,
        bool hasApiKey,
        ChatDisplayMode displayMode,
        string version,
        string configPath,
        Func<string, IChatService> chatServiceFactory,
        Action<string?> persistApiKey,
        ITerminalGuiLayoutFactory? layoutFactory = null,
        ToolSummaryBuilder? toolSummaryBuilder = null)
    {
        _services = services;
        _chatServiceFactory = chatServiceFactory;
        _persistApiKey = persistApiKey;
        _conversation = new List<ChatMessage>();
        _hasApiKey = hasApiKey;
        _displayMode = displayMode;
        _version = version;
        _configPath = configPath;
        _layoutFactory = layoutFactory ?? new TerminalGuiLayoutFactory();
        _toolSummaryBuilder = toolSummaryBuilder ?? new ToolSummaryBuilder(() => _layout?.Window?.Frame.Width ?? 80);
        _sessionStopwatch = Stopwatch.StartNew();
        SetChatService(chatService, hasApiKey);
    }

    public void Run()
    {
        Application.Init();

        BuildUI();

        Application.Run(_layout!.Top);
    }

    public void Dispose()
    {
        DetachChatService();
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        _statusPresenter?.Dispose();
        Application.Shutdown();
    }

    private void SetChatService(IChatService chatService, bool hasApiKey)
    {
        DetachChatService();
        _chatService = chatService;
        _hasApiKey = hasApiKey;
        AttachChatService();
    }

    private void AttachChatService()
    {
        _chatService.OnTextReceived += OnTextReceived;
        _chatService.OnUsageReceived += OnUsageReceived;
        _chatService.OnToolCalled += OnToolCalled;
        _chatService.OnToolResult += OnToolResult;
    }

    private void DetachChatService()
    {
        if (_chatService == null)
            return;

        _chatService.OnTextReceived -= OnTextReceived;
        _chatService.OnUsageReceived -= OnUsageReceived;
        _chatService.OnToolCalled -= OnToolCalled;
        _chatService.OnToolResult -= OnToolResult;
    }

    private void BuildUI()
    {
        var menuActions = new TerminalGuiMenuActions(
            StartNewSession,
            ShowLogoutPopover,
            () => Application.RequestStop(),
            SetMode,
            SetModel);

        _layout = _layoutFactory.Create(
            _displayMode,
            _hasApiKey,
            BuildWindowTitle(),
            _model,
            menuActions,
            PlanCollapsedHeight,
            PlanExpandedHeight,
            InputHeight,
            InputFrameBorder,
            StatusHeight);

        _historyManager = new HistoryViewManager(_layout.HistoryView);
        _planPresenter = new PlanPresenter(
            _layout.PlanFrame,
            _layout.PlanTitleLabel,
            _layout.PlanItemsView,
            _layout.HistoryView,
            _layout.Window,
            PlanExpandedHeight,
            PlanCollapsedHeight,
            InputHeight,
            InputFrameBorder,
            StatusHeight);
        _statusPresenter = new ThinkingStatusPresenter(_layout.StatusLabel, _sessionStopwatch, EnqueueUi);

        _layout.InputView.KeyDown += InputViewOnKeyDown;

        Application.MainLoop?.AddIdle(() =>
        {
            if (!_welcomeShown)
            {
                _welcomeShown = true;
                AppendWelcomeMessage();
            }

            if (!_hasApiKey && !_apiKeyPromptOpen)
            {
                ShowApiKeyPopover();
            }

            return false;
        });

        _layout.InputView.SetFocus();
    }

    private void AppendWelcomeMessage()
    {
        if (_historyManager == null)
            return;

        var builder = new StringBuilder();
        builder.AppendLine("Commands: Ctrl+J (newline) | cmd <command> or /cmd <command> (run shell)");
        builder.AppendLine($"Config: {_configPath}");
        builder.AppendLine();

        _historyManager.SetContent(builder.ToString());
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
            else
            {
                _layout?.InputView.Text = "";
            }
        }

        if (key == Key.Enter)
        {
            _ = ProcessInputAsync();
            if (_layout != null)
                _layout.InputView.Text = "";
        }
    }

    private async Task ProcessInputAsync()
    {
        if (_cts != null)
            return;

        if (!_hasApiKey)
        {
            ShowApiKeyPopover();
            return;
        }

        var text = _layout?.InputView.Text?.ToString() ?? string.Empty;
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

        _lastUsage = null;
        _assistantBlockOpen = false;
        StartThinkingAnimation();
        AppendHistoryBlock($"[You]: {userText}\n");
        SetStatus("thinking...");

        _layout!.InputView.Text = "";

        try
        {
            await _chatService.SendMessageAsync(userText, _conversation, token);
        }
        catch (OperationCanceledException)
        {
            AppendHistoryBlock("[canceled]\n");
        }
        catch (Exception ex)
        {
            AppendHistoryBlock($"[error] {ex.Message}\n");
        }
        finally
        {
            if (_cts != null)
            {
                _cts.Dispose();
                _cts = null;
            }

            StopThinkingAnimation();
            SetStatus("Ready");
            _assistantBlockOpen = false;
        }
    }

    private bool HandleLocalCommands(string userText)
    {
        if (string.Equals(userText, "clear", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(userText, "/clear", StringComparison.OrdinalIgnoreCase))
        {
            _historyManager?.Clear();
            _planPresenter?.Clear();
            _assistantBlockOpen = false;
            return true;
        }

        if (string.Equals(userText, "logout", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(userText, "/logout", StringComparison.OrdinalIgnoreCase))
        {
            ShowLogoutPopover();
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
        EnqueueUi(() =>
        {
            EnsureAssistantBlock();
            AppendHistory(text);
        });
    }

    private void OnUsageReceived(ChatTokenUsage usage)
    {
        _lastUsage = usage;
    }

    private void OnToolCalled(ToolCallEvent toolEvent)
    {
        if (_displayMode == ChatDisplayMode.Normal)
            return;

        EnqueueUi(() =>
        {
            if (_displayMode == ChatDisplayMode.Debug)
            {
                var args = string.IsNullOrWhiteSpace(toolEvent.ArgumentsJson)
                    ? ""
                    : toolEvent.ArgumentsJson;
                AppendHistoryBlock($"[tool] {toolEvent.ToolName} {args}\n");
            }
            else
            {
                AppendHistoryBlock(_toolSummaryBuilder.BuildToolCall(toolEvent));
            }
            _assistantBlockOpen = false;
        });
    }

    private void OnToolResult(ToolResultEvent toolEvent)
    {
        if (string.Equals(toolEvent.ToolName, "set_plan", StringComparison.OrdinalIgnoreCase))
        {
            HandlePlan(toolEvent);
            if (_displayMode == ChatDisplayMode.Normal)
                return;
            _assistantBlockOpen = false;
            return;
        }

        if (string.Equals(toolEvent.ToolName, "workflow_done", StringComparison.OrdinalIgnoreCase))
        {
            StopThinkingAnimation();
            SetStatus("Ready");
            EnqueueUi(() =>
            {
                AppendHistoryBlock(_toolSummaryBuilder.BuildToolSummary(toolEvent, _lastUsage, GetDurationText));
                _planPresenter?.Clear();
                _sessionStopwatch.Restart();
                _assistantBlockOpen = false;
            });
            return;
        }

        EnqueueUi(() =>
        {
            if (_displayMode == ChatDisplayMode.Debug)
            {
                var output = toolEvent.Result.Success
                    ? toolEvent.Result.Output
                    : toolEvent.Result.Error;
                AppendHistoryBlock($"[result] {toolEvent.ToolName}: {output}\n");
            }
            else
            {
                AppendHistoryBlock(_toolSummaryBuilder.BuildToolSummary(toolEvent, _lastUsage, GetDurationText));
            }
            _assistantBlockOpen = false;
        });
    }

    private void HandlePlan(ToolResultEvent toolEvent)
    {
        var payload = PlanParser.Parse(toolEvent.ArgumentsJson);

        if (payload == null || payload.Items.Count == 0)
        {
            EnqueueUi(() => _planPresenter?.Clear());
            return;
        }

        EnqueueUi(() =>
        {
            _planPresenter?.Render(payload);
        });
    }

    private void SetModel(string model)
    {
        if (string.Equals(_model, model, StringComparison.Ordinal))
            return;

        _chatService.SetModel(model);
        _model = model;
        AppendHistoryBlock($"[model] Switched to {model}\n");

        if (_layout == null)
            return;

        _layout.ModelReasoning.Checked = model == "grok-4-1-fast-reasoning";
        _layout.ModelNonReasoning.Checked = model == "grok-4-1-fast-non-reasoning";
        _layout.ModelCodeFast.Checked = model == "grok-code-fast-1";
    }

    private void SetMode(ChatDisplayMode mode)
    {
        if (_layout == null)
            return;

        var modeLabel = mode == ChatDisplayMode.Debug ? "Debug" : "Normal";
        _displayMode = mode;
        _layout.Window.Title = BuildWindowTitle();
        _layout.ModeDebug.Checked = mode == ChatDisplayMode.Debug;
        _layout.ModeNormal.Checked = mode == ChatDisplayMode.Normal;
        AppendHistoryBlock($"[mode] Switched to {modeLabel} mode\n");
    }

    private void AppendHistory(string text)
    {
        _historyManager?.Append(text);
    }

    private void EnsureAssistantBlock()
    {
        if (_assistantBlockOpen)
            return;

        _historyManager?.EnsureAssistantStream();
        _assistantBlockOpen = true;
    }

    private void AppendHistoryBlock(string text)
    {
        _historyManager?.AppendToNewBlock(text);
        _assistantBlockOpen = false;
    }

    private void AppendCommandOutput(string text)
    {
        AppendHistoryBlock(text);
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

    private string BuildWindowTitle()
    {
        var modeLabel = _displayMode == ChatDisplayMode.Debug ? "Debug" : "Normal";
        return $"Grok CLI {_version} - {modeLabel} mode";
    }

    private string GetDurationText()
    {
        if (_statusPresenter != null)
            return _statusPresenter.GetDurationText();

        var seconds = (int)Math.Max(0, _sessionStopwatch.Elapsed.TotalSeconds);
        return FormatDuration(seconds);
    }

    private void StartNewSession()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        _conversation.Clear();
        _lastUsage = null;
        _sessionStopwatch.Restart();
        _assistantBlockOpen = false;

        _historyManager?.Clear();
        _planPresenter?.Clear();
        AppendWelcomeMessage();
        SetStatus(_hasApiKey ? "Ready" : "API key required");
    }

    private void PerformLogout()
    {
        _persistApiKey(null);
        SetChatService(new DisabledChatService(), false);
        StartNewSession();
        AppendHistoryBlock("[system] Logged out\n");
        ShowApiKeyPopover();
    }

    private void ShowLogoutPopover()
    {
        var popover = new Popover("Logout", 40, 9);
        var message = new Label("Are you sure you want to log out?")
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill() - 2
        };

        var confirmButton = new Button("Yes")
        {
            IsDefault = true,
            X = 1,
            Y = Pos.Bottom(message) + 1
        };

        var cancelButton = new Button("Cancel")
        {
            X = Pos.Right(confirmButton) + 2,
            Y = Pos.Top(confirmButton)
        };

        confirmButton.Clicked += () =>
        {
            PerformLogout();
            Application.RequestStop();
        };

        cancelButton.Clicked += () => Application.RequestStop();

        popover.Add(message, confirmButton, cancelButton);
        Application.Run(popover);
    }

    private void CompleteLogin(string apiKey)
    {
        _persistApiKey(apiKey);
        SetChatService(_chatServiceFactory(apiKey), true);
        _chatService.SetModel(_model);
        StartNewSession();
    }

    private void ShowApiKeyPopover()
    {
        if (_apiKeyPromptOpen)
            return;

        _apiKeyPromptOpen = true;

        var popover = new Popover("API Key Required", 60, 12);
        var message = new Label("Enter XAI_API_KEY to continue")
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill() - 2
        };

        var input = new TextField("")
        {
            X = 1,
            Y = Pos.Bottom(message) + 1,
            Width = Dim.Fill() - 2
        };

        var errorLabel = new Label("")
        {
            X = 1,
            Y = Pos.Bottom(input) + 1,
            Width = Dim.Fill() - 2,
            ColorScheme = Colors.Error
        };

        var saveButton = new Button("Save")
        {
            IsDefault = true,
            X = 1,
            Y = Pos.Bottom(errorLabel) + 1
        };

        var cancelButton = new Button("Cancel")
        {
            X = Pos.Right(saveButton) + 2,
            Y = Pos.Top(saveButton)
        };

        saveButton.Clicked += () =>
        {
            var value = input.Text?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                errorLabel.Text = "API key is required";
                return;
            }

            CompleteLogin(value);
            Application.RequestStop();
        };

        cancelButton.Clicked += () => Application.RequestStop();

        popover.Add(message, input, errorLabel, saveButton, cancelButton);
        try
        {
            Application.Run(popover);
        }
        finally
        {
            _apiKeyPromptOpen = false;
        }

        _layout?.InputView.SetFocus();
        if (!_hasApiKey)
            SetStatus("API key required");
    }

    private void StartThinkingAnimation()
    {
        _statusPresenter?.StartThinking();
    }

    private void StopThinkingAnimation()
    {
        _statusPresenter?.StopThinking();
    }

    private void SetStatus(string text)
    {
        _statusPresenter?.SetStatus(text);
    }

    private static string FormatDuration(int seconds)
    {
        var minutes = seconds / 60;
        var remainingSeconds = seconds % 60;
        return $"{minutes:00}m {remainingSeconds:00}s";
    }

    private static string? TryGetString(string? json, string property)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty(property, out var prop))
            {
                return prop.GetString();
            }
        }
        catch
        {
        }

        return null;
    }
}
