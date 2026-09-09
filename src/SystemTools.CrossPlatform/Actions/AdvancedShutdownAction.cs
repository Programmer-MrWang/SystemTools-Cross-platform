using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Platforms.Abstraction;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using SystemTools.CrossPlatform.Services;
using SystemTools.CrossPlatform.Settings;
using SystemTools.CrossPlatform.Views;

namespace SystemTools.CrossPlatform.Actions;

[ActionInfo("SystemTools.CrossPlatform.AdvancedShutdown", "高级计时关机", "\uE4D2", false)]
public class AdvancedShutdownAction : ActionBase<AdvancedShutdownSettings>
{
    private static ILogger? _sharedLogger;

    private readonly ILogger<AdvancedShutdownAction> _logger;

    private static readonly object StateLock = new();
    private static DateTimeOffset _shutdownAt = DateTimeOffset.MinValue;
    private static int _totalScheduledSeconds;
    private static AdvancedShutdownDialog? _activeDialog;
    private static Window? _floatingWindow;
    private static bool _allowMainDialogClose;
    private static bool _allowFloatingWindowClose;
    private static int _appStoppingHandled;

    public AdvancedShutdownAction(ILogger<AdvancedShutdownAction> logger)
    {
        _logger = logger;
        _sharedLogger = logger;
    }

    public static bool CancelPlanOnAppStopping(bool isSessionEnding)
    {
        if (Interlocked.Exchange(ref _appStoppingHandled, 1) != 0)
        {
            return false;
        }

        if (!isSessionEnding)
        {
            var abortResult = SystemPowerCommand.RunCancelScheduledShutdown();
            _sharedLogger?.LogInformation("宿主退出取消关机计划：exit={ExitCode}（无活动计划时属预期）。", abortResult.ExitCode);
        }

        return true;
    }

    protected override async Task OnInvoke()
    {
        _logger.LogDebug("AdvancedShutdownAction OnInvoke 开始");

        if (!SystemPowerCapability.IsActionSupported(SystemPowerCapability.AdvancedShutdownId))
        {
            _logger.LogWarning("高级计时关机预检未通过：当前环境不支持（平台能力门），跳过执行。");
            await NotifyDegradedAsync("高级计时关机", "高级计时关机在当前环境不可用，已跳过执行");
            await base.OnInvoke();
            return;
        }

        if (!IsPlanActive())
        {
            var configuredMinutes = Math.Max(1, Settings?.Minutes ?? 2);
            if (!ScheduleShutdown(configuredMinutes))
            {
                await NotifyDegradedAsync("高级计时关机", "计划关机命令未被执行");
                await base.OnInvoke();
                return;
            }
        }

        await ShowDialogAsync();
        await base.OnInvoke();
    }

    private static bool IsPlanActive()
    {
        lock (StateLock)
        {
            return _shutdownAt > DateTimeOffset.Now;
        }
    }

    private bool ScheduleShutdown(int minutes)
    {
        var safeMinutes = Math.Max(1, minutes);
        var seconds = safeMinutes * 60;

        var abortResult = SystemPowerCommand.RunCancelScheduledShutdown();
        _logger.LogDebug("计划前取消旧计划：exit={ExitCode}", abortResult.ExitCode);

        var scheduleResult = SystemPowerCommand.RunTimedShutdown(seconds);
        if (scheduleResult.ExitCode != PowerCommandResult.Ok)
        {
            _logger.LogError("计划关机命令未被执行（exit={ExitCode}，detail={Detail}）。秒数: {Seconds}",
                scheduleResult.ExitCode, scheduleResult.Detail, seconds);
            return false;
        }

        lock (StateLock)
        {
            _shutdownAt = DateTimeOffset.Now.AddMinutes(safeMinutes);
            _totalScheduledSeconds = seconds;
        }

        _logger.LogInformation("已计划 {Seconds} 秒后执行关机。", seconds);
        return true;
    }

    void AssignProgressAnimator(ProgressBar bar, TimeSpan targetTime, TimeSpan totalTime)
    {
        new Animation
        {
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters =
                    {
                        new Setter(RangeBase.ValueProperty, 3000.0 * targetTime.TotalMilliseconds / totalTime.TotalMilliseconds)
                    }
                },
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters =
                    {
                        new Setter(RangeBase.ValueProperty, 0.0),
                    }
                }
            },
            Duration = targetTime,
            FillMode = FillMode.Forward
        }.RunAsync(bar);
    }

    private bool ExtendShutdown(int extendMinutes)
    {
        var safeExtendMinutes = Math.Max(1, extendMinutes);
        DateTimeOffset previousTargetTime;
        int previousTotalSeconds;
        DateTimeOffset targetTime;

        lock (StateLock)
        {
            previousTargetTime = _shutdownAt;
            previousTotalSeconds = _totalScheduledSeconds;
            var baseline = _shutdownAt > DateTimeOffset.Now ? _shutdownAt : DateTimeOffset.Now;
            _shutdownAt = baseline.AddMinutes(safeExtendMinutes);
            targetTime = _shutdownAt;
            _totalScheduledSeconds = (int)Math.Ceiling((targetTime - DateTimeOffset.Now).TotalSeconds);
        }

        var totalSeconds = (int)Math.Ceiling((targetTime - DateTimeOffset.Now).TotalSeconds);
        totalSeconds = Math.Max(60, totalSeconds);

        var abortResult = SystemPowerCommand.RunCancelScheduledShutdown();
        _logger.LogDebug("延长重计划前取消旧计划：exit={ExitCode}", abortResult.ExitCode);

        var scheduleResult = SystemPowerCommand.RunTimedShutdown(totalSeconds);
        if (scheduleResult.ExitCode != PowerCommandResult.Ok)
        {
            _logger.LogError("延长后的计划关机命令未被执行（exit={ExitCode}，detail={Detail}），总秒数: {Seconds}",
                scheduleResult.ExitCode, scheduleResult.Detail, totalSeconds);
            return false;
        }

        _logger.LogInformation("已延长关机计划，目标时间 {TargetTime:HH:mm:ss}（{Seconds} 秒）。", targetTime, totalSeconds);
        return true;
    }

    private void CancelShutdownPlan()
    {
        var hadPlan = IsPlanActive();
        var abortResult = StopAllStates();
        if (!hadPlan)
        {
            return;
        }

        if (abortResult.ExitCode == PowerCommandResult.Ok)
        {
            _logger.LogInformation("关机计划已取消。");
        }
        else if (abortResult.ExitCode == PowerCommandResult.NoShutdownInProgress)
        {
            _logger.LogInformation("取消时已无活动关机计划（exit={ExitCode}，可能已被外部取消）。", abortResult.ExitCode);
        }
        else
        {
            _logger.LogWarning("取消关机计划未生效（exit={ExitCode}，detail={Detail}）。", abortResult.ExitCode, abortResult.Detail);
            _ = NotifyDegradedAsync("取消关机计划", "取消关机计划未生效，请检查系统关机计划状态");
        }
    }

    private PowerCommandResult StopAllStates()
    {
        var abortResult = SystemPowerCommand.RunCancelScheduledShutdown();
        if (abortResult.ExitCode == PowerCommandResult.Ok)
        {
            _logger.LogInformation("已取消系统关机计划。");
        }
        else
        {
            _logger.LogInformation("取消系统关机计划返回非零（exit={ExitCode}；无活动计划时属预期）。", abortResult.ExitCode);
        }

        lock (StateLock)
        {
            _shutdownAt = DateTimeOffset.MinValue;
            _totalScheduledSeconds = 0;
        }

        Dispatcher.UIThread.Post(() =>
        {
            CloseMainDialogProgrammatically();
            CloseFloatingWindowProgrammatically();
        });

        return abortResult;
    }

    private static int GetRemainingSeconds()
    {
        lock (StateLock)
        {
            var remainingSeconds = (int)Math.Ceiling((_shutdownAt - DateTimeOffset.Now).TotalSeconds);
            return Math.Max(0, remainingSeconds);
        }
    }

    private static double BuildCountdownProgress()
    {
        int remaining;
        int total;
        lock (StateLock)
        {
            remaining = Math.Max(0, (int)Math.Ceiling((_shutdownAt - DateTimeOffset.Now).TotalSeconds));
            total = _totalScheduledSeconds;
        }

        if (total <= 0)
        {
            return 0;
        }

        return Math.Clamp(remaining * 100.0 / total, 0, 100);
    }

    private static string BuildCountdownText()
    {
        var remainingSeconds = GetRemainingSeconds();
        var minutes = remainingSeconds / 60;
        var seconds = remainingSeconds % 60;
        return $"距离关机还有{minutes}分{seconds:00}秒";
    }

    private async Task ShowDialogAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            CloseFloatingWindowProgrammatically();
            await ShowStyledDialogAsync();
        });
    }

    private async Task ShowStyledDialogAsync()
    {
        if (_activeDialog is { IsVisible: true })
        {
            _activeDialog.Activate();
            return;
        }

        var dialog = new AdvancedShutdownDialog
        {
            CanResize = false
        };
        _activeDialog = dialog;

        dialog.Closing += (_, e) =>
        {
            if (!_allowMainDialogClose && IsPlanActive())
            {
                e.Cancel = true;
            }
        };

        var textBlock = dialog.CountdownTextBlock ?? throw new InvalidOperationException("CountdownTextBlockElement 未找到");
        var progressBar = dialog.CountdownProgressBar ?? throw new InvalidOperationException("CountdownProgressBarElement 未找到");
        var immediateShutdownButton = dialog.ImmediateShutdownButton ?? throw new InvalidOperationException("ImmediateShutdownButtonElement 未找到");
        var readButton = dialog.ReadButton ?? throw new InvalidOperationException("ReadButtonElement 未找到");
        var cancelPlanButton = dialog.CancelPlanButton ?? throw new InvalidOperationException("CancelPlanButtonElement 未找到");
        var extendButton = dialog.ExtendButton ?? throw new InvalidOperationException("ExtendButtonElement 未找到");

        textBlock.Text = BuildCountdownText();
        progressBar.Value = BuildCountdownProgress();

        var countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        countdownTimer.Tick += (_, _) =>
        {
            textBlock.Text = BuildCountdownText();

            if (!IsPlanActive())
            {
                CloseMainDialogProgrammatically();
            }
        };
        AssignProgressAnimator(progressBar, _shutdownAt - DateTimeOffset.Now, TimeSpan.FromSeconds(_totalScheduledSeconds));
        countdownTimer.Start();

        dialog.Closed += (_, _) =>
        {
            countdownTimer.Stop();
            if (ReferenceEquals(_activeDialog, dialog))
            {
                _activeDialog = null;
            }

            if (IsPlanActive())
            {
                ShowOrUpdateFloatingWindow();
            }
        };

        readButton.Click += (_, _) =>
        {
            CloseMainDialogProgrammatically();
            ShowOrUpdateFloatingWindow();
        };

        immediateShutdownButton.Click += (_, _) =>
        {
            StopAllStates();
            var result = SystemPowerCommand.RunImmediateShutdown();
            if (result.ExitCode != PowerCommandResult.Ok)
            {
                _logger.LogError("执行立即关机失败（exit={ExitCode}，detail={Detail}）。", result.ExitCode, result.Detail);
                _ = NotifyDegradedAsync("立即关机",
                    result.ExitCode == PowerCommandResult.AccessDenied
                        ? "立即关机未执行：系统拒绝了请求（可能需要系统授权）"
                        : "立即关机命令未被执行");
            }
        };

        cancelPlanButton.Click += (_, _) => CancelShutdownPlan();

        extendButton.Click += async (_, _) =>
        {
            var extendMinutes = await ShowExtendInputDialogAsync(dialog);
            if (extendMinutes.HasValue)
            {
                if (ExtendShutdown(extendMinutes.Value))
                {
                    CloseMainDialogProgrammatically();
                    ShowOrUpdateFloatingWindow();
                }
                else
                {
                    StopAllStates();
                    await NotifyDegradedAsync("高级计时关机", "延长关机未生效，已取消本次关机计划");
                }
            }
        };

        dialog.Show();
        dialog.Activate();
        await Task.CompletedTask;
    }

    private void ShowOrUpdateFloatingWindow()
    {
        if (!IsPlanActive())
        {
            CloseFloatingWindowProgrammatically();
            return;
        }

        if (_floatingWindow is { IsVisible: true })
        {
            return;
        }

        var tipButton = new Button
        {
            Content = BuildCountdownText() + "  点此返回设置",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = Avalonia.Media.Brushes.Transparent,
            Foreground = Avalonia.Media.Brushes.White
        };

        tipButton.Click += async (_, _) => await ShowDialogAsync();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) =>
        {
            if (!IsPlanActive())
            {
                CloseFloatingWindowProgrammatically();
                return;
            }

            tipButton.Content = BuildCountdownText() + "  点此返回设置";
        };

        var floatWindow = new Window
        {
            Width = 320,
            Height = 56,
            CanResize = false,
            Topmost = true,
            ShowInTaskbar = false,
            WindowDecorations = WindowDecorations.None,
            Background = Brushes.Transparent,
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent],
            Content = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 6),
                Background = new SolidColorBrush(Color.FromArgb(210, 20, 20, 20)),
                Opacity = 0.88,
                Child = tipButton
            }
        };

        floatWindow.Opened += (_, _) => PinFloatingWindowTopRight(floatWindow);
        floatWindow.PositionChanged += (_, _) => PinFloatingWindowTopRight(floatWindow);
        floatWindow.Closing += (_, e) =>
        {
            if (!_allowFloatingWindowClose && IsPlanActive())
            {
                e.Cancel = true;
                PinFloatingWindowTopRight(floatWindow);
            }
        };
        floatWindow.Closed += (_, _) =>
        {
            timer.Stop();
            if (ReferenceEquals(_floatingWindow, floatWindow))
            {
                _floatingWindow = null;
            }
        };

        _floatingWindow = floatWindow;
        floatWindow.Show();
        timer.Start();
    }

    private static void PinFloatingWindowTopRight(Window window)
    {
        var screen = window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary;
        if (screen is null)
        {
            return;
        }

        var area = screen.WorkingArea;
        var scaling = Math.Max(0.5, window.RenderScaling);
        var widthDip = window.Bounds.Width > 0 ? window.Bounds.Width : window.Width;
        var marginPx = (int)Math.Round(12 * scaling);
        var widthPx = (int)Math.Round(widthDip * scaling);

        var x = area.X + area.Width - widthPx - marginPx;
        var y = area.Y + marginPx;
        var target = new PixelPoint(Math.Max(area.X, x), Math.Max(area.Y, y));

        if (window.Position != target)
        {
            window.Position = target;
        }
    }

    private void CloseMainDialogProgrammatically()
    {
        if (_activeDialog is not { } dialog)
        {
            return;
        }

        _allowMainDialogClose = true;
        dialog.Close();
        _allowMainDialogClose = false;
        _activeDialog = null;
    }

    private void CloseFloatingWindowProgrammatically()
    {
        if (_floatingWindow is not { } window)
        {
            return;
        }

        _allowFloatingWindowClose = true;
        window.Close();
        _allowFloatingWindowClose = false;
        _floatingWindow = null;
    }

    private static async Task<int?> ShowExtendInputDialogAsync(Window owner)
    {
        var dialog = new ExtendShutdownDialog
        {
            Topmost = true,
            ShowInTaskbar = false
        };

        var previousTopmost = owner.Topmost;
        owner.Topmost = false;
        try
        {
            await dialog.ShowDialog(owner);
            return dialog.ResultMinutes;
        }
        finally
        {
            owner.Topmost = previousTopmost;
            owner.Activate();
        }
    }

    private async Task NotifyDegradedAsync(string title, string reason)
    {
        try
        {
            await PlatformServices.DesktopToastService.ShowToastAsync($"SystemTools - {title}", reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "降级提示发送失败：{Title} - {Reason}", title, reason);
        }
    }
}