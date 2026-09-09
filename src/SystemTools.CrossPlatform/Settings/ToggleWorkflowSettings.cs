using System.Text.Json.Serialization;

namespace SystemTools.CrossPlatform.Settings;

public class ToggleWorkflowSettings
{
    [JsonPropertyName("targetWorkflowName")]
    public string TargetWorkflowName { get; set; } = string.Empty;

    [JsonPropertyName("targetWorkflowIndex")]
    public int TargetWorkflowIndex { get; set; } = -1;

    [JsonPropertyName("enableMode")]
    public bool? EnableMode { get; set; } = null;

    [JsonPropertyName("revertToOriginal")]
    public bool RevertToOriginal { get; set; } = true;
}
