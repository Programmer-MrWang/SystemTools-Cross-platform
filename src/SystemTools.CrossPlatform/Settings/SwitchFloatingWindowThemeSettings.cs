using System.Text.Json.Serialization;

namespace SystemTools.CrossPlatform.Settings;

/// <summary>
/// 切换悬浮窗主题行动的设置
/// </summary>
public class SwitchFloatingWindowThemeSettings
{
    [JsonPropertyName("notifyOnExecute")]
    public bool NotifyOnExecute { get; set; } = false;

    /// <summary>
    /// 目标主题。-1=切换到下一个, 0=跟随系统, 1=浅色, 2=深色。「自适应背景」（3）已移除；旧配置值按 0 归一。
    /// </summary>
    [JsonPropertyName("targetTheme")]
    public int TargetTheme { get; set; } = -1;
}
