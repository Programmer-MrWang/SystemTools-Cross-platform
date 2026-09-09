using System;
using SystemTools.CrossPlatform.Rules;

namespace SystemTools.CrossPlatform.Rules.Handlers;

/// <summary>
/// 程序正在运行规则处理器
/// </summary>
public static class ProcessRunningRuleHandler
{
    public static bool Handle(object? settings)
    {
        if (settings is not ProcessRunningRuleSettings ruleSettings ||
            string.IsNullOrWhiteSpace(ruleSettings.ProcessName))
        {
            return false;
        }

        var processName = ruleSettings.ProcessName.Trim();
        if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            processName = processName[..^4];
        }

        try
        {
            return System.Diagnostics.Process.GetProcessesByName(processName).Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
