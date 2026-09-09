using System;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SystemTools.CrossPlatform.Config;

public partial class FloatingWindowTriggerConfig : ObservableObject
{
    [ObservableProperty]
    [JsonPropertyName("buttonId")]
    private string _buttonId = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    [JsonPropertyName("icon")]
    private string _icon = Services.FloatingWindowIconProvider.DefaultIconExpression;

    [ObservableProperty]
    [JsonPropertyName("buttonName")]
    private string _buttonName = "触发按钮 1";

    [ObservableProperty]
    [JsonPropertyName("isVisible")]
    private bool _isVisible = true;

    [ObservableProperty]
    [JsonPropertyName("position")]
    private int _position = -1;
}
