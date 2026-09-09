using System;
using System.Threading;
using Avalonia.Threading;
using ClassIsland.Platforms.Abstraction;

namespace SystemTools.CrossPlatform.Actions;

/// <summary>
/// Linux/macOS 的“应用内关机计划”宿主（单一 cipx 三平台通用）。
/// 非 root 无法注册系统级定时关机（Windows 的 shutdown /t 无对应物），故
/// SystemPowerCommand.RunTimedShutdown 在非 Windows 上把计划落在这里：
/// 到点由本中心调用 SystemPowerCommand.RunImmediateShutdown()（systemctl poweroff /
/// System Events 关机）完成实际关机。
/// 语义：单槽计划（后计划替换前计划，与 Windows 单一定时器一致）；
/// 宿主进程退出/计划中心销毁即取消（进程内定时器随之消亡，与 Windows
/// “宿主退出即取消 OS 计划”的用户可见语义一致）。Windows 路径不经过本中心。
/// 线程模型：System.Threading.Timer（线程池回调），全部状态持锁；
/// 失败提示经 Dispatcher 编组到 UI 线程。
/// </summary>
internal static class ShutdownPlanCenter
{
    /// <summary>无活动计划时的取消结果码（与 Windows shutdown /a 的 1116 对齐，行动层文案复用）。</summary>
    private const int NoShutdownInProgressExitCode = 1116;

    private static readonly object Gate = new();
    private static DateTimeOffset? _deadlineUtc;
    private static int _totalSeconds;
    private static bool _firing;
    private static Timer? _timer;

    /// <summary>登记计划：seconds 秒后执行平台“立即关机”。返回 0。</summary>
    public static int Schedule(int seconds)
    {
        lock (Gate)
        {
            var safeSeconds = Math.Max(1, seconds);
            _deadlineUtc = DateTimeOffset.UtcNow.AddSeconds(safeSeconds);
            _totalSeconds = safeSeconds;
            _firing = false;
            EnsureTimerLocked();
            return 0;
        }
    }

    /// <summary>取消防火计划。已取消返回 0；本就无计划返回 1116（行动层按“无活动计划”文案处理）。</summary>
    public static int Cancel()
    {
        lock (Gate)
        {
            if (_deadlineUtc == null)
            {
                TryStopIfIdleLocked();
                return NoShutdownInProgressExitCode;
            }

            _deadlineUtc = null;
            _firing = false;
            TryStopIfIdleLocked();
            return 0;
        }
    }

    private static void EnsureTimerLocked()
    {
        if (_timer != null)
        {
            return;
        }

        _timer = new Timer(_ => OnTick(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    private static void TryStopIfIdleLocked()
    {
        if (_firing || _deadlineUtc != null || _timer == null)
        {
            return;
        }

        _timer.Dispose();
        _timer = null;
    }

    private static void OnTick()
    {
        lock (Gate)
        {
            if (_firing || _deadlineUtc == null || DateTimeOffset.UtcNow < _deadlineUtc.Value)
            {
                return;
            }

            // 到点：先消费计划再执行，避免执行窗口内重复触发（后续执行不在锁内）。
            _firing = true;
            _deadlineUtc = null;
        }

        var result = SystemPowerCommand.RunImmediateShutdown();

        lock (Gate)
        {
            _firing = false;
            TryStopIfIdleLocked();
        }

        if (result.ExitCode == 0)
        {
            return;
        }

        ReportFireFailure(result);
    }

    private static void ReportFireFailure(PowerCommandResult result)
    {
        var reason = result.ExitCode switch
        {
            PowerCommandResult.AccessDenied =>
                $"计划关机已到点，但执行被系统拒绝（{TrimDetail(result.Detail)}），请检查系统电源管理授权",
            PowerCommandResult.TimedOutInitiated =>
                "计划关机已到点，但系统未在限时内确认执行（若弹出授权提示请先允许后重试）",
            _ => $"计划关机已到点，但关机命令未被执行（{TrimDetail(result.Detail)}）"
        };

        try
        {
            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    await PlatformServices.DesktopToastService.ShowToastAsync("SystemTools - 计划关机", reason);
                }
                catch (Exception)
                {
                    // 提示失败不影响计划语义（计划已消费），仅静默
                }
            });
        }
        catch (Exception)
        {
            // 无 UI 线程环境（如宿主退出竞态）时静默
        }
    }

    private static string TrimDetail(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return "无详细原因";
        }

        return detail.Length <= 140 ? detail : detail[..140];
    }
}
