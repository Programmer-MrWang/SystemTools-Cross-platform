using System.Text.Json.Serialization;

namespace SystemTools.CrossPlatform.Settings;

/// <summary>
/// 高级计时关机行动设置
/// </summary>
public class AdvancedShutdownSettings
{
    [JsonPropertyName("minutes")] public int Minutes { get; set; } = 2;
}
