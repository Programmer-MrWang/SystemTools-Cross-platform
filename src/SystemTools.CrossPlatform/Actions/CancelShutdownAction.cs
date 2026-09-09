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

[ActionInfo("SystemTools.CrossPlatform.CancelShutdown", "取消关机计划", "\uE4CC", false)]
public class CancelShutdownAction(ILogger<CancelShutdownAction> logger) : ActionBase<ShortcutKeyNotificationSettings>
{
    private readonly ILogger<CancelShutdownAction> _logger = logger;

    protected override async Task OnInvoke()
    {
        _logger.LogDebug("CancelShutdownAction OnInvoke 开始");

        if (!SystemPowerCapability.IsActionSupported(SystemPowerCapability.CancelShutdownId))
        {
            _logger.LogWarning("取消关机计划预检未通过：当前环境不支持（平台能力门），跳过执行。");
            await NotifyDegradedAsync("取消关机计划", "取消关机计划在当前环境不可用，已跳过执行");
            await base.OnInvoke();
            return;
        }

        _logger.LogInformation("正在执行取消关机命令");
        var result = SystemPowerCommand.RunCancelScheduledShutdown();
        if (result.ExitCode == PowerCommandResult.Ok)
        {
            _logger.LogInformation("关机已取消");
        }
        else if (result.ExitCode == PowerCommandResult.NoShutdownInProgress)
        {
            _logger.LogInformation("当前没有活动的关机计划（exit={ExitCode}），无计划可取消。", result.ExitCode);
            await NotifyDegradedAsync("取消关机计划", "当前没有活动的关机计划");
            await base.OnInvoke();
            return;
        }
        else
        {
            _logger.LogError("取消关机失败（exit={ExitCode}，detail={Detail}）。", result.ExitCode, result.Detail);
            await NotifyDegradedAsync("取消关机计划", "取消关机计划未执行成功");
            await base.OnInvoke();
            return;
        }

        if (Settings.NotifyOnExecute)
            IAppHost.GetService<SystemToolsNotificationProvider>()?.ShowNotification(new NotificationRequest
            {
                MaskContent = NotificationContent.CreateTwoIconsMask("已取消关机计划", "\uE9FB", "")
            });

        await base.OnInvoke();
        _logger.LogDebug("CancelShutdownAction OnInvoke 完成");
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