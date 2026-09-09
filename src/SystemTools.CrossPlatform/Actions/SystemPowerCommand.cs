using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SystemTools.CrossPlatform.Actions;

/// <summary>电源原语执行结果：退出码（见常量）+ 失败详情（非 Windows 捕获的系统输出尾部）。</summary>
internal readonly record struct PowerCommandResult(int ExitCode, string? Detail = null)
{
    public const int Ok = 0;
    /// <summary>Windows shutdown /a 无活动计划；Linux/macOS 无应用内计划（行动层文案复用 1116 分支）。</summary>
    public const int NoShutdownInProgress = 1116;
    public const int FailedToStart = -1;
    /// <summary>有界等待超时：命令已发起、结果未确认（睡眠、系统即将断电等）。</summary>
    public const int TimedOutInitiated = -2;
    /// <summary>系统/授权策略拒绝（polkit、macOS 自动化 TCC、权限不足）。</summary>
    public const int AccessDenied = -3;
    /// <summary>当前平台/环境不支持该原语（注册期已拦截，纯防御）。</summary>
    public const int NotSupported = -4;
}

/// <summary>
/// 电源族系统原语（单一 cipx 三平台通用，运行期按平台分派）。
/// - Windows：shutdown.exe / rundll32.exe（沿用原版命令形态与退出码语义）；
/// - Linux：systemd 电源动词（poweroff/reboot/suspend）归属 systemctl——loginctl 自始不提供
///   电源动词（曾误用致 “Unknown command verb”）；锁屏走 loginctl lock-session；elogind 系
///   （无 systemctl）回退 loginctl 电源动词。非 root 经 polkit 授权，失败如实返回
///   （AccessDenied 带系统输出详情）；
/// - macOS：osascript → System Events（关机/重启；首次需“自动化”授权，拒绝时返回
///   AccessDenied）、pmset sleepnow（睡眠）；定时关机族在 Linux/macOS 为应用内计划
///   （ShutdownPlanCenter）——非 root 无系统级定时关机原语。
/// 全部命令走有界等待；超时按“已发起、未确认”处理，不伪造成功。
/// </summary>
internal static class SystemPowerCommand
{
    private const int DefaultWaitMilliseconds = 3000;
    private const int SleepWaitMilliseconds = 1500;

    private const string MacShutDownScript = "tell application \"System Events\" to shut down";
    private const string MacRestartScript = "tell application \"System Events\" to restart";

    // ---------- 对外原语 ----------

    /// <summary>定时关机：Windows 走 OS 级 shutdown /t；Linux/macOS 登记应用内计划（到点调立即关机）。</summary>
    internal static PowerCommandResult RunTimedShutdown(int seconds)
    {
        var safeSeconds = Math.Max(0, seconds);
        if (OperatingSystem.IsWindows())
        {
            return RunWindows("shutdown.exe", $"/s /t {safeSeconds}", DefaultWaitMilliseconds);
        }

        return new PowerCommandResult(ShutdownPlanCenter.Schedule(safeSeconds));
    }

    internal static PowerCommandResult RunImmediateShutdown()
    {
        if (OperatingSystem.IsWindows())
        {
            return RunWindows("shutdown.exe", "/s /t 0", DefaultWaitMilliseconds);
        }

        if (OperatingSystem.IsLinux())
        {
            return RunLinuxPowerVerb("poweroff");
        }

        if (OperatingSystem.IsMacOS())
        {
            return RunOsascript(MacShutDownScript);
        }

        return new PowerCommandResult(PowerCommandResult.NotSupported, "当前平台不支持关机操作");
    }

    internal static PowerCommandResult RunImmediateRestart()
    {
        if (OperatingSystem.IsWindows())
        {
            return RunWindows("shutdown.exe", "/g /t 0", DefaultWaitMilliseconds);
        }

        if (OperatingSystem.IsLinux())
        {
            return RunLinuxPowerVerb("reboot");
        }

        if (OperatingSystem.IsMacOS())
        {
            return RunOsascript(MacRestartScript);
        }

        return new PowerCommandResult(PowerCommandResult.NotSupported, "当前平台不支持重启操作");
    }

    /// <summary>
    /// 取消关机计划：Windows 执行 shutdown /a（0 成功 / 1116 无计划）；
    /// Linux/macOS 取消应用内计划（0 已取消 / 1116 本就无计划）。
    /// </summary>
    internal static PowerCommandResult RunCancelScheduledShutdown()
    {
        if (OperatingSystem.IsWindows())
        {
            return RunWindows("shutdown.exe", "/a", DefaultWaitMilliseconds);
        }

        return new PowerCommandResult(ShutdownPlanCenter.Cancel());
    }

    internal static PowerCommandResult RunLockWorkstation()
    {
        if (OperatingSystem.IsWindows())
        {
            return RunWindows("rundll32.exe", "user32.dll,LockWorkStation", DefaultWaitMilliseconds);
        }

        if (OperatingSystem.IsLinux())
        {
            return RunLoginctl("lock-session", useCallerSession: true);
        }

        if (OperatingSystem.IsMacOS())
        {
            // 非 root 无可靠公共锁屏途径（CGSession 需 root；模拟快捷键需辅助功能授权），
            // 注册期已按能力门排除，此处为防御。
            return new PowerCommandResult(PowerCommandResult.NotSupported, "macOS 非 root 不支持锁定屏幕");
        }

        return new PowerCommandResult(PowerCommandResult.NotSupported, "当前平台不支持锁定屏幕");
    }

    internal static PowerCommandResult RunSleep()
    {
        if (OperatingSystem.IsWindows())
        {
            return RunWindows("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0", SleepWaitMilliseconds);
        }

        if (OperatingSystem.IsLinux())
        {
            // systemctl suspend 通常阻塞至系统睡眠完成/唤醒；有界等待超时按“已发起”处理。
            return RunLinuxPowerVerb("suspend");
        }

        if (OperatingSystem.IsMacOS())
        {
            return RunCapturedTool("pmset", ["sleepnow"], SleepWaitMilliseconds);
        }

        return new PowerCommandResult(PowerCommandResult.NotSupported, "当前平台不支持睡眠操作");
    }

    // ---------- 平台执行器 ----------

    /// <summary>Windows 原生命令路径（沿用原版行为：不重定向输出、超时不杀进程）。</summary>
    private static PowerCommandResult RunWindows(string fileName, string arguments, int waitForExitMilliseconds)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            using var process = Process.Start(psi);
            if (process == null)
            {
                return new PowerCommandResult(PowerCommandResult.FailedToStart);
            }

            if (process.WaitForExit(waitForExitMilliseconds))
            {
                return new PowerCommandResult(process.ExitCode);
            }

            return new PowerCommandResult(PowerCommandResult.TimedOutInitiated);
        }
        catch (Exception ex)
        {
            return new PowerCommandResult(PowerCommandResult.FailedToStart, ex.Message);
        }
    }

    /// <summary>
    /// Linux 电源动词执行：systemd 的电源动词（poweroff/reboot/suspend）归属 systemctl，
    /// loginctl 只承担会话类动词（lock-session 等）；elogind 系发行版（非 systemd 启动）无
    /// systemctl，其 loginctl 携带电源动词，故作为回退。非 root 经 polkit 授权，失败如实上报。
    /// </summary>
    private static PowerCommandResult RunLinuxPowerVerb(string verb)
    {
        var systemctl = SystemPowerCapability.FindOnPath("systemctl");
        if (systemctl != null)
        {
            return RunCapturedTool(systemctl, [verb], DefaultWaitMilliseconds);
        }

        var loginctl = SystemPowerCapability.FindOnPath("loginctl");
        if (loginctl != null)
        {
            return RunCapturedTool(loginctl, [verb], DefaultWaitMilliseconds);
        }

        return new PowerCommandResult(PowerCommandResult.FailedToStart, "未找到 systemctl/loginctl（需要 systemd 或 elogind）");
    }

    /// <summary>loginctl 会话类动词执行（如 lock-session；锁屏专用，勿用于电源动词）。</summary>
    private static PowerCommandResult RunLoginctl(string verb, bool useCallerSession = false)
    {
        var exe = SystemPowerCapability.FindOnPath("loginctl");
        if (exe == null)
        {
            return new PowerCommandResult(PowerCommandResult.FailedToStart, "未找到 loginctl（需要 systemd-logind）");
        }

        var arguments = new System.Collections.Generic.List<string> { verb };
        if (useCallerSession)
        {
            var sessionId = Environment.GetEnvironmentVariable("XDG_SESSION_ID");
            if (!string.IsNullOrEmpty(sessionId))
            {
                arguments.Add(sessionId);
            }
        }

        return RunCapturedTool(exe, arguments.ToArray(), DefaultWaitMilliseconds);
    }

    private static PowerCommandResult RunOsascript(string script)
    {
        var exe = SystemPowerCapability.FindOnPath("osascript");
        if (exe == null)
        {
            return new PowerCommandResult(PowerCommandResult.FailedToStart, "未找到 osascript");
        }

        return RunCapturedTool(exe, ["-e", script], DefaultWaitMilliseconds);
    }

    /// <summary>
    /// Unix 工具执行：重定向输出、有界等待；超时杀进程并按“已发起、未确认”返回；
    /// 失败输出尾部随结果带回（授权拒绝经关键字识别为 AccessDenied）。
    /// </summary>
    private static PowerCommandResult RunCapturedTool(string exePath, string[] arguments, int waitForExitMilliseconds)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in arguments)
            {
                psi.ArgumentList.Add(argument);
            }

            using var process = Process.Start(psi);
            if (process == null)
            {
                return new PowerCommandResult(PowerCommandResult.FailedToStart, "进程未能启动");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(waitForExitMilliseconds))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception)
                {
                    // 进程可能已自行退出
                }

                try
                {
                    Task.WaitAll([stdoutTask, stderrTask], TimeSpan.FromMilliseconds(500));
                }
                catch (Exception)
                {
                    // 读取收尾失败不影响结果判定
                }

                return new PowerCommandResult(PowerCommandResult.TimedOutInitiated,
                    "进程未在限时内返回（系统可能正在处理请求或等待确认）");
            }

            Task.WaitAll([stdoutTask, stderrTask], TimeSpan.FromMilliseconds(2000));
            var exitCode = process.ExitCode;
            if (exitCode == 0)
            {
                return new PowerCommandResult(PowerCommandResult.Ok);
            }

            var output = (stdoutTask.IsCompleted ? stdoutTask.Result : string.Empty)
                         + (stderrTask.IsCompleted ? stderrTask.Result : string.Empty);
            var detail = TrimOutput(output);
            if (LooksLikeAuthorizationDenial(output))
            {
                return new PowerCommandResult(PowerCommandResult.AccessDenied, detail);
            }

            return new PowerCommandResult(PowerCommandResult.FailedToStart,
                string.IsNullOrEmpty(detail) ? $"退出码 {exitCode}" : detail);
        }
        catch (Exception ex)
        {
            return new PowerCommandResult(PowerCommandResult.FailedToStart, ex.Message);
        }
    }

    private static bool LooksLikeAuthorizationDenial(string output)
    {
        if (string.IsNullOrEmpty(output))
        {
            return false;
        }

        var lowered = output.ToLowerInvariant();
        return lowered.Contains("authentication required") // polkit：Interactive authentication required
            || lowered.Contains("not authorized")           // macOS TCC：-1743
            || lowered.Contains("not allowed")
            || lowered.Contains("-1743")
            || lowered.Contains("denied")
            || lowered.Contains("refused");
    }

    private static string? TrimOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        var collapsed = output.Replace('\r', ' ').Replace('\n', ' ').Trim();
        while (collapsed.Contains("  "))
        {
            collapsed = collapsed.Replace("  ", " ");
        }

        return collapsed.Length <= 220 ? collapsed : collapsed[..220];
    }
}
