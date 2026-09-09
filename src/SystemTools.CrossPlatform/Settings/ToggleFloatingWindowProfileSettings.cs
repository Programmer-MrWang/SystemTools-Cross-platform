using System.Text.Json.Serialization;

namespace SystemTools.CrossPlatform.Settings;

public class ToggleFloatingWindowProfileSettings
{
    [JsonPropertyName("notifyOnExecute")]
    public bool NotifyOnExecute { get; set; } = false;

    [JsonPropertyName("targetProfileName")]
    public string? TargetProfileName { get; set; }
}
