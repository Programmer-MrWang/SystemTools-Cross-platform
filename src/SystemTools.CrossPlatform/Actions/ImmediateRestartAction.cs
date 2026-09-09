using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Platforms.Abstraction;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using SystemTools.CrossPlatform.Services;

namespace SystemTools.CrossPlatform.Actions;

[ActionInfo("SystemTools.CrossPlatform.ImmediateRestart", "立即重启", "\uE0BD", false)]
public class ImmediateRestartAction(ILogger<ImmediateRestartAction> logger) : ActionBase
{
    private readonly ILogger<ImmediateRestartAction> _logger = logger;

    protected override async Task OnInvoke()
    {
        _logger.LogDebug("ImmediateRestartAction OnInvoke 开始");

        if (!SystemPowerCapability.IsActionSupported(SystemPowerCapability.ImmediateRestartId))
        {
            _logger.LogWarning("立即重启预检未通过：当前环境不支持（平台能力门），跳过执行。");
            await NotifyDegradedAsync("立即重启", "立即重启在当前环境不可用，已跳过执行");
            await base.OnInvoke();
            return;
        }

        var result = SystemPowerCommand.RunImmediateRestart();
        switch (result.ExitCode)
        {
            case PowerCommandResult.Ok:
                break;
            case PowerCommandResult.TimedOutInitiated:
                // 命令已发起、系统未在限时内确认（系统可能正在处理或弹出授权提示）
                _logger.LogInformation("立即重启已发起（系统未在限时内确认，detail={Detail}）。", result.Detail);
                await base.OnInvoke();
                return;
            case PowerCommandResult.AccessDenied:
                _logger.LogError("立即重启未执行：系统拒绝了授权（exit={ExitCode}，detail={Detail}）。", result.ExitCode, result.Detail);
                await NotifyDegradedAsync("立即重启", "立即重启未执行：系统拒绝了请求（可能需要系统授权）");
                await base.OnInvoke();
                return;
            default:
                _logger.LogError("立即重启未执行（exit={ExitCode}，detail={Detail}）。", result.ExitCode, result.Detail);
                await NotifyDegradedAsync("立即重启", "立即重启未执行");
                await base.OnInvoke();
                return;
        }

        _logger.LogInformation("已执行立即重启命令");

        await base.OnInvoke();
        _logger.LogDebug("ImmediateRestartAction OnInvoke 完成");
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