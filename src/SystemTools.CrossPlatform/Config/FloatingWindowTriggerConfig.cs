using System;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SystemTools.CrossPlatform.Config;

/// <summary>
/// 悬浮窗触发器的配置。抽取自源插件 Triggers\FloatingWindowTriggerConfig.cs
/// （落点按 p2-05 §1.2 权威表归 Config\ 目录——触发器三件套 Trigger/Config/Settings 结构先例，
/// 源插件其余 5 个触发器配置亦均在 Config\ 目录；命名空间按 p1-05 §3.2 目录镜像规则调整，
/// 其余逐行保留源实现）。
/// </summary>
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
