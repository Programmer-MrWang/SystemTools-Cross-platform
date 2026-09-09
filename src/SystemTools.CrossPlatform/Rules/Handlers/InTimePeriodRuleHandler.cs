using System;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Shared;
using SystemTools.CrossPlatform.Rules;

namespace SystemTools.CrossPlatform.Rules.Handlers;

/// <summary>
/// 是否在某时间段规则处理器
/// </summary>
public static class InTimePeriodRuleHandler
{
    public static bool Handle(object? settings)
    {
        if (settings is not InTimePeriodRuleSettings ruleSettings ||
            !TimeSpan.TryParse(ruleSettings.StartTime, out var start) ||
            !TimeSpan.TryParse(ruleSettings.EndTime, out var end))
        {
            return false;
        }

        var current = IAppHost.TryGetService<IExactTimeService>()?.GetCurrentLocalDateTime().TimeOfDay ?? DateTime.Now.TimeOfDay;
        if (start <= end)
        {
            return current >= start && current <= end;
        }

        return current >= start || current <= end;
    }
}
