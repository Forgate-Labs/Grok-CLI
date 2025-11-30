using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Terminal.Gui;

namespace GrokCLI.UI;

public sealed class ThinkingStatusPresenter : IDisposable
{
    private readonly Label _statusLabel;
    private readonly Stopwatch _sessionStopwatch;
    private readonly Action<Action> _enqueueUi;
    private CancellationTokenSource? _thinkingAnimationCts;
    private int _thinkingDotCount;

    public ThinkingStatusPresenter(Label statusLabel, Stopwatch sessionStopwatch, Action<Action> enqueueUi)
    {
        _statusLabel = statusLabel;
        _sessionStopwatch = sessionStopwatch;
        _enqueueUi = enqueueUi;
    }

    public void SetStatus(string text)
    {
        _enqueueUi(() => _statusLabel.Text = $" {text}");
    }

    public void StartThinking()
    {
        StopThinking();
        _sessionStopwatch.Restart();
        _thinkingAnimationCts = new CancellationTokenSource();
        _thinkingDotCount = 0;
        var token = _thinkingAnimationCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                var dots = new string('.', (_thinkingDotCount % 4));
                _thinkingDotCount++;
                var elapsed = _sessionStopwatch.Elapsed;
                var text = $"Thinking{dots} ({FormatDuration((int)Math.Max(0, elapsed.TotalSeconds))} - esc to interrupt)";

                _enqueueUi(() => _statusLabel.Text = text);

                try
                {
                    await Task.Delay(500, token);
                }
                catch (TaskCanceledException)
                {
                }
            }
        });
    }

    public void StopThinking()
    {
        if (_thinkingAnimationCts != null)
        {
            _thinkingAnimationCts.Cancel();
            _thinkingAnimationCts.Dispose();
            _thinkingAnimationCts = null;
        }

        _thinkingDotCount = 0;
    }

    public string GetDurationText()
    {
        return FormatDuration((int)Math.Max(0, _sessionStopwatch.Elapsed.TotalSeconds));
    }

    public void Dispose()
    {
        StopThinking();
    }

    private static string FormatDuration(int seconds)
    {
        var minutes = seconds / 60;
        var remainingSeconds = seconds % 60;
        return $"{minutes:00}m {remainingSeconds:00}s";
    }
}
