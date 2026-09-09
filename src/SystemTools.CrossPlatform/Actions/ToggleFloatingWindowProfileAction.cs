using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Shared;
using Microsoft.Extensions.Logging;
using SystemTools.CrossPlatform.ConfigHandlers;
using SystemTools.CrossPlatform.Services;
using SystemTools.CrossPlatform.Settings;
using SystemTools.CrossPlatform.Shared;
using ClassIsland.Core.Models.Notification;

namespace SystemTools.CrossPlatform.Actions;

[ActionInfo("SystemTools.CrossPlatform.ToggleFloatingWindowProfile", "切换悬浮窗配置方案", "\uE9A8", false)]
public class ToggleFloatingWindowProfileAction(ILogger<ToggleFloatingWindowProfileAction> logger) : ActionBase<ToggleFloatingWindowProfileSettings>
{
    private readonly ILogger<ToggleFloatingWindowProfileAction> _logger = logger;
    private static readonly ConcurrentDictionary<Guid, string> PreviousProfiles = new();

    protected override async Task OnInvoke()
    {
        _logger.LogDebug("ToggleFloatingWindowProfileAction OnInvoke 开始");

        try
        {
            var profileManager = IAppHost.GetService<FloatingWindowProfileManager>();
            var currentProfileName = profileManager.CurrentProfileName;

            if (!string.IsNullOrWhiteSpace(Settings.TargetProfileName))
            {
                if (IsRevertable)
                {
                    PreviousProfiles[ActionSet.Guid] = currentProfileName;
                }

                SwitchToProfile(profileManager, Settings.TargetProfileName);
                _logger.LogInformation("已切换到悬浮窗配置方案: {Name}", Settings.TargetProfileName);
            }
            else
            {
                if (IsRevertable)
                {
                    PreviousProfiles[ActionSet.Guid] = currentProfileName;
                }

                ToggleToNextProfile(profileManager);
                _logger.LogInformation("已切换到下一个悬浮窗配置方案");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换悬浮窗配置方案失败");
            throw;
        }
        if (Settings.NotifyOnExecute)
            IAppHost.GetService<SystemToolsNotificationProvider>()?.ShowNotification(new NotificationRequest
            {
                MaskContent = NotificationContent.CreateTwoIconsMask("已自动切换悬浮窗配置方案", "\uE9FB", "")
            });


        await base.OnInvoke();
        _logger.LogDebug("ToggleFloatingWindowProfileAction OnInvoke 完成");
    }

    protected override async Task OnRevert()
    {
        await base.OnRevert();

        if (!PreviousProfiles.TryRemove(ActionSet.Guid, out var previousProfile))
        {
            _logger.LogInformation("未找到配置方案恢复快照，跳过悬浮窗配置方案恢复。ActionSet={ActionSetGuid}", ActionSet.Guid);
            return;
        }

        try
        {
            var profileManager = IAppHost.GetService<FloatingWindowProfileManager>();
            SwitchToProfile(profileManager, previousProfile);
            _logger.LogInformation("已恢复悬浮窗配置方案为: {Name}", previousProfile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "恢复悬浮窗配置方案失败");
            throw;
        }
    }

    private void SwitchToProfile(FloatingWindowProfileManager profileManager, string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return;
        }

        var names = profileManager.GetProfileNames();
        if (!names.Contains(profileName))
        {
            return;
        }

        if (profileManager.ProfileFileExists(profileManager.CurrentProfileName))
        {
            profileManager.SaveProfile();
        }
        profileManager.LoadProfile(profileName);

        var configHandler = GlobalConstants.MainConfig;
        if (configHandler != null)
        {
            configHandler.Data.CurrentFloatingWindowProfile = profileName;
            configHandler.Save();
        }
    }

    private void ToggleToNextProfile(FloatingWindowProfileManager profileManager)
    {
        var names = profileManager.GetProfileNames();
        if (names.Count <= 1)
        {
            return;
        }

        var currentName = profileManager.CurrentProfileName;
        var currentIndex = -1;
        for (int i = 0; i < names.Count; i++)
        {
            if (string.Equals(names[i], currentName, StringComparison.OrdinalIgnoreCase))
            {
                currentIndex = i;
                break;
            }
        }
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var newIndex = (currentIndex + 1) % names.Count;
        SwitchToProfile(profileManager, names[newIndex]);
    }
}
