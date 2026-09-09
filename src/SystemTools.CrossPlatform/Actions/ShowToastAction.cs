using System;
using System.Threading.Tasks;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Models.Notification;
using ClassIsland.Platforms.Abstraction;
using ClassIsland.Shared;
using Microsoft.Extensions.Logging;
using SystemTools.CrossPlatform.Services;
using SystemTools.CrossPlatform.Settings;

namespace SystemTools.CrossPlatform.Actions;

// 显示名经“文案去 Windows 化”裁决由「拉起自定义Windows通知」改写（ID 与图标不变）。
[ActionInfo("SystemTools.CrossPlatform.ShowToast", "拉起自定义系统通知", "\uE3E4", false)]
public class ShowToastAction(ILogger<ShowToastAction> logger) : ActionBase<ShowToastSettings>
{
    private readonly ILogger<ShowToastAction> _logger = logger;

    protected override async Task OnInvoke()
    {
        _logger.LogDebug("ShowToastAction OnInvoke 开始");

        var title = Settings.Title;
        var content = Settings.Content;

        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("通知标题和内容均为空，跳过显示");
            return;
        }

        try
        {
            await PlatformServices.DesktopToastService.ShowToastAsync(
                title ?? "SystemTools",
                content ?? string.Empty,
                () => { _logger.LogInformation("用户点击了通知"); }
            );

            _logger.LogInformation("已显示通知: {Title}", title);
        }
        catch (Exception ex)
        {
            // 桌面通知通道失败（如 Linux 无 org.freedesktop.Notifications 守护进程、macOS 拒绝授权等）：
            // 记日志并经应用内通知渠道如实提示用户，不递归调用已失败的桌面 Toast 通道、不伪造成功。
            _logger.LogError(ex, "显示通知失败（桌面通知通道异常）");
            try
            {
                IAppHost.GetService<SystemToolsNotificationProvider>()?.ShowNotification(new NotificationRequest
                {
                    MaskContent = NotificationContent.CreateTwoIconsMask("自定义通知显示失败", "\uE9FB", "")
                });
            }
            catch (Exception notifyEx)
            {
                _logger.LogError(notifyEx, "应用内失败提示发送失败");
            }
        }

        await base.OnInvoke();
        _logger.LogDebug("ShowToastAction OnInvoke 完成");
    }
}
