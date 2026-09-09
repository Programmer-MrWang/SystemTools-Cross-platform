using System;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Shared;
using SystemTools.CrossPlatform.Rules;

namespace SystemTools.CrossPlatform.Rules.Handlers;

/// <summary>
/// 正在使用某时间表规则处理器
/// </summary>
public static class UsingTimeLayoutRuleHandler
{
    public static bool Handle(object? settings)
    {
        if (settings is not UsingTimeLayoutRuleSettings ruleSettings ||
            !Guid.TryParse(ruleSettings.TimeLayoutId, out var timeLayoutId))
        {
            return false;
        }

        var profile = IAppHost.TryGetService<IProfileService>()?.Profile;
        if (profile == null || !profile.TimeLayouts.TryGetValue(timeLayoutId, out var timeLayout))
        {
            return false;
        }

        return timeLayout.IsActivated;
    }
}
