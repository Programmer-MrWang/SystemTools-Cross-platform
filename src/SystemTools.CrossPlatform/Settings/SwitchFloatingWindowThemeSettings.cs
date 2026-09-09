using System.Text.Json.Serialization;

namespace SystemTools.CrossPlatform.Settings;

public class SwitchFloatingWindowThemeSettings
{
    [JsonPropertyName("notifyOnExecute")]
    public bool NotifyOnExecute { get; set; } = false;

    [JsonPropertyName("targetTheme")]
    public int TargetTheme { get; set; } = -1;
}
