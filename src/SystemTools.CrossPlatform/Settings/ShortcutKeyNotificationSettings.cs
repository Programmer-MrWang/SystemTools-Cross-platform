using System.Text.Json.Serialization;

namespace SystemTools.CrossPlatform.Settings;

public class ShortcutKeyNotificationSettings
{

    [JsonPropertyName("notifyOnExecute")]
    public bool NotifyOnExecute { get; set; } = false;
}
