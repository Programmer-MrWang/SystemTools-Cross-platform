using System;
using System.IO;

namespace SystemTools.CrossPlatform.Actions;

/// <summary>
/// 电源族行动的“平台能力”静态探测（单一 cipx 三平台通用，运行期分派）。
/// 供注册期（Plugin.RegisterBaseActions / BuildBaseActionTree）与功能抽屉
/// （SystemToolsSettingsViewModel.InitializeFeatureItems）共用：能力缺失的行动
/// “不注册、不显示”，避免出现必然失败的菜单项。
/// 探测只做环境变量/文件检查，不派生进程、不触发任何授权询问，结果惰性缓存。
/// 平台口径：
/// - Windows：shutdown.exe / rundll32.exe 存在（沿用原版命令预检语义）；
/// - Linux：systemd/elogind 电源通道可用（systemctl 或 loginctl 可执行文件）且处于图形会话
///   （X11 或 Wayland 按用户裁决均注册，与显示服务器无关）；关机/重启/睡眠执行走 systemctl
///   （elogind 无 systemctl 时回退 loginctl），锁屏走 loginctl lock-session；
/// - macOS：关机/重启/定时族经 osascript → System Events（TCC 自动化授权，
///   运行时拒绝如实上报）；睡眠经 pmset sleepnow；锁屏无非 root 公共途径，不注册。
/// </summary>
internal static class SystemPowerCapability
{
    // ---- 行动 ID 常量（注册/菜单/抽屉/能力查询共用，避免散落字符串） ----
    public const string TimedShutdownId = "SystemTools.CrossPlatform.Shutdown";
    public const string AdvancedShutdownId = "SystemTools.CrossPlatform.AdvancedShutdown";
    public const string CancelShutdownId = "SystemTools.CrossPlatform.CancelShutdown";
    public const string LockScreenId = "SystemTools.CrossPlatform.LockScreen";
    public const string ImmediateRestartId = "SystemTools.CrossPlatform.ImmediateRestart";
    public const string ImmediateShutdownId = "SystemTools.CrossPlatform.ImmediateShutdown";
    public const string SleepId = "SystemTools.CrossPlatform.Sleep";

    /// <summary>电源选项行动组全部 ID（组门/菜单/抽屉过滤复用）。</summary>
    public static readonly string[] PowerActionIds =
    [
        TimedShutdownId,
        AdvancedShutdownId,
        CancelShutdownId,
        LockScreenId,
        ImmediateRestartId,
        ImmediateShutdownId,
        SleepId
    ];

    private sealed class Capabilities
    {
        public bool TimedShutdown;
        public bool AdvancedShutdown;
        public bool CancelShutdown;
        public bool LockScreen;
        public bool ImmediateRestart;
        public bool ImmediateShutdown;
        public bool Sleep;
    }

    private static readonly Lazy<Capabilities> Probe = new(EvaluateCapabilities);

    /// <summary>该行动是否在本机当前环境下注册可用（非电源族行动恒为 true）。</summary>
    public static bool IsActionSupported(string actionId) => actionId switch
    {
        TimedShutdownId => Probe.Value.TimedShutdown,
        AdvancedShutdownId => Probe.Value.AdvancedShutdown,
        CancelShutdownId => Probe.Value.CancelShutdown,
        LockScreenId => Probe.Value.LockScreen,
        ImmediateRestartId => Probe.Value.ImmediateRestart,
        ImmediateShutdownId => Probe.Value.ImmediateShutdown,
        SleepId => Probe.Value.Sleep,
        _ => true
    };

    /// <summary>电源族是否有任意行动可在本机注册（决定“电源选项…”组是否入树）。</summary>
    public static bool HasAnyPowerActionSupported()
    {
        var c = Probe.Value;
        return c.TimedShutdown || c.AdvancedShutdown || c.CancelShutdown || c.LockScreen
               || c.ImmediateRestart || c.ImmediateShutdown || c.Sleep;
    }

    private static Capabilities EvaluateCapabilities()
    {
        if (OperatingSystem.IsWindows())
        {
            var systemDir = Environment.SystemDirectory;
            if (string.IsNullOrEmpty(systemDir))
            {
                systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            }
            var shutdown = !string.IsNullOrEmpty(systemDir) && File.Exists(Path.Combine(systemDir, "shutdown.exe"));
            var rundll32 = !string.IsNullOrEmpty(systemDir) && File.Exists(Path.Combine(systemDir, "rundll32.exe"));
            return new Capabilities
            {
                TimedShutdown = shutdown,
                AdvancedShutdown = shutdown,
                CancelShutdown = shutdown,
                LockScreen = rundll32,
                ImmediateRestart = shutdown,
                ImmediateShutdown = shutdown,
                Sleep = rundll32
            };
        }

        if (OperatingSystem.IsLinux())
        {
            // systemd 电源动词归属 systemctl（loginctl 自始不提供）；elogind 系无 systemctl，
            // 其 loginctl 携带电源动词。注册门只看“systemctl 或 loginctl 可执行 + 图形会话”，
            // polkit 授权只能在执行时验证（失败如实上报）。锁屏仅要求 loginctl（systemctl 无 lock-session）。
            var graphical = IsInGraphicalSession();
            var systemctlAvailable = FindOnPath("systemctl") != null;
            var loginctlAvailable = FindOnPath("loginctl") != null;
            var powerToolAvailable = (systemctlAvailable || loginctlAvailable) && graphical;
            return new Capabilities
            {
                TimedShutdown = powerToolAvailable,
                AdvancedShutdown = powerToolAvailable,
                CancelShutdown = powerToolAvailable,
                LockScreen = loginctlAvailable && graphical,
                ImmediateRestart = powerToolAvailable,
                ImmediateShutdown = powerToolAvailable,
                Sleep = powerToolAvailable
            };
        }

        if (OperatingSystem.IsMacOS())
        {
            var osascript = FindOnPath("osascript") != null;
            var pmset = FindOnPath("pmset") != null;
            return new Capabilities
            {
                // 定时关机族在 macOS 为“应用内计划 + 到点 System Events 关机”（非 root 无系统级计划）。
                TimedShutdown = osascript,
                AdvancedShutdown = osascript,
                CancelShutdown = osascript,
                LockScreen = false, // 非 root 无可靠公共锁屏 API（CGSession 需 root；模拟快捷键需辅助功能）
                ImmediateRestart = osascript,
                ImmediateShutdown = osascript,
                Sleep = pmset
            };
        }

        return new Capabilities();
    }

    private static bool IsInGraphicalSession()
    {
        var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        if (string.Equals(sessionType, "x11", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")) ||
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
    }

    /// <summary>在 PATH 中查找可执行文件，返回完整路径；未找到返回 null。</summary>
    internal static string? FindOnPath(string fileName)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathValue))
        {
            return null;
        }

        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim(), fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (Exception)
            {
                // 忽略个别不可访问的 PATH 目录项
            }
        }

        return null;
    }
}
