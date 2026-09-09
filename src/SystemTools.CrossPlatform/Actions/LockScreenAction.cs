using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Models.Notification;
using ClassIsland.Platforms.Abstraction;
using ClassIsland.Shared;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using SystemTools.CrossPlatform.Services;
using SystemTools.CrossPlatform.Settings;

namespace SystemTools.CrossPlatform.Actions;

[ActionInfo("SystemTools.CrossPlatform.LockScreen", "锁定屏幕", "\uEAF0", false)]
public class LockScreenAction(ILogger<LockScreenAction> logger) : ActionBase<ShortcutKeyNotificationSettings>
{
    private readonly ILogger<LockScreenAction> _logger = logger;

    protected override async Task OnInvoke()
    {
        _logger.LogDebug("LockScreenAction OnInvoke 开始");

        if (!SystemPowerCapability.IsActionSupported(SystemPowerCapability.LockScreenId))
        {
            _logger.LogWarning("锁定屏幕预检未通过：当前环境不支持（平台能力门），跳过执行。");
            await NotifyDegradedAsync("锁定屏幕", "锁定屏幕在当前环境不可用，已跳过执行");
            await base.OnInvoke();
            return;
        }

        _logger.LogInformation("正在执行锁定屏幕命令");
        var result = SystemPowerCommand.RunLockWorkstation();
        if (result.ExitCode != PowerCommandResult.Ok)
        {
            _logger.LogError("锁定屏幕未执行（exit={ExitCode}，detail={Detail}）。", result.ExitCode, result.Detail);
            await NotifyDegradedAsync("锁定屏幕",
                result.ExitCode == PowerCommandResult.AccessDenied
                    ? "锁定屏幕未执行：系统拒绝了请求（可能需要系统授权）"
                    : "锁定屏幕未执行");
            await base.OnInvoke();
            return;
        }

        _logger.LogInformation("屏幕已锁定");
        if (Settings.NotifyOnExecute)
            IAppHost.GetService<SystemToolsNotificationProvider>()?.ShowNotification(new NotificationRequest
            {
                MaskContent = NotificationContent.CreateTwoIconsMask("已锁定屏幕", "\uE9FB", "")
            });

        await base.OnInvoke();
        _logger.LogDebug("LockScreenAction OnInvoke 完成");
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