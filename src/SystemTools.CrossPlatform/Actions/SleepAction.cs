using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Platforms.Abstraction;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using SystemTools.CrossPlatform.Services;

namespace SystemTools.CrossPlatform.Actions;

[ActionInfo("SystemTools.CrossPlatform.Sleep", "睡眠", "\uF44B", false)]
public class SleepAction(ILogger<SleepAction> logger) : ActionBase
{
    private readonly ILogger<SleepAction> _logger = logger;

    protected override async Task OnInvoke()
    {
        _logger.LogDebug("SleepAction OnInvoke 开始");

        if (!SystemPowerCapability.IsActionSupported(SystemPowerCapability.SleepId))
        {
            _logger.LogWarning("睡眠预检未通过：当前环境不支持（平台能力门），跳过执行。");
            await NotifyDegradedAsync("睡眠", "睡眠在当前环境不可用，已跳过执行");
            await base.OnInvoke();
            return;
        }

        var result = SystemPowerCommand.RunSleep();
        switch (result.ExitCode)
        {
            case PowerCommandResult.Ok:
                break;
            case PowerCommandResult.TimedOutInitiated:
                // 原版语义：有界等待超时按“已发起、未确认”处理（睡眠期间命令本就无法返回）。
                _logger.LogInformation("睡眠命令已发起（有界等待超时，未阻塞确认；exit={ExitCode}）。", result.ExitCode);
                await base.OnInvoke();
                return;
            case PowerCommandResult.AccessDenied:
                _logger.LogError("睡眠未执行：系统拒绝了授权（exit={ExitCode}，detail={Detail}）。", result.ExitCode, result.Detail);
                await NotifyDegradedAsync("睡眠", "睡眠未执行：系统拒绝了请求（可能需要系统授权）");
                await base.OnInvoke();
                return;
            default:
                _logger.LogError("睡眠未执行（exit={ExitCode}，detail={Detail}）。", result.ExitCode, result.Detail);
                await NotifyDegradedAsync("睡眠", "睡眠未执行");
                await base.OnInvoke();
                return;
        }

        _logger.LogInformation("已执行睡眠命令");

        await base.OnInvoke();
        _logger.LogDebug("SleepAction OnInvoke 完成");
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