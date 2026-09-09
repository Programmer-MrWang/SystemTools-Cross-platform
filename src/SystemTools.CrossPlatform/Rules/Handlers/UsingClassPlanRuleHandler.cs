using System;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Shared;
using SystemTools.CrossPlatform.Rules;

namespace SystemTools.CrossPlatform.Rules.Handlers;

/// <summary>
/// 正在使用某课程表规则处理器
/// </summary>
public static class UsingClassPlanRuleHandler
{
    public static bool Handle(object? settings)
    {
        if (settings is not UsingClassPlanRuleSettings ruleSettings ||
            !Guid.TryParse(ruleSettings.ClassPlanId, out var classPlanId))
        {
            return false;
        }

        var profile = IAppHost.TryGetService<IProfileService>()?.Profile;
        if (profile == null || !profile.ClassPlans.TryGetValue(classPlanId, out var classPlan))
        {
            return false;
        }

        return classPlan.IsActivated;
    }
}
