using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using ClassIsland.Core.Models.Ruleset;

namespace SystemTools.CrossPlatform.ConfigHandlers;

/// <summary>
/// 插件聚合配置根（阶段 1 A 档裁剪形，落位与裁剪口径见 p1-05 §2.3-4）：
/// 仅包含本阶段 A 档功能确需的成员（悬浮窗方案/主题状态、行动流确认窗口位置、
/// 功能开闭字典及注册面辅助方法）与 FloatingWindowProfileManager 旧配置迁移所需的
/// 悬浮窗布局成员；B/C 档选项成员按规范留待阶段 2/3 按需增补。
/// JSON 属性名与源插件同名成员保持一致。
/// </summary>
public class MainConfigData : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    int _floatingWindowTheme = 0;

    [JsonPropertyName("floatingWindowTheme")]
    public int FloatingWindowTheme
    {
        get => _floatingWindowTheme;
        set
        {
            // 「自适应背景」主题项（取值 3）已移除；旧配置中的 3 在读取/写入时统一归一为 0（跟随 ClassIsland）。
            var normalized = value is 1 or 2 ? value : 0;
            if (normalized == _floatingWindowTheme) return;
            _floatingWindowTheme = normalized;
            OnPropertyChanged();
        }
    }

    string _currentFloatingWindowProfile = "Default";

    [JsonPropertyName("currentFloatingWindowProfile")]
    public string CurrentFloatingWindowProfile
    {
        get => _currentFloatingWindowProfile;
        set
        {
            if (string.Equals(value, _currentFloatingWindowProfile, StringComparison.Ordinal)) return;
            _currentFloatingWindowProfile = value;
            OnPropertyChanged();
        }
    }

    [JsonPropertyName("actionFlowExecutionConfirmationPositionX")]
    public int? ActionFlowExecutionConfirmationPositionX { get; set; }

    [JsonPropertyName("actionFlowExecutionConfirmationPositionY")]
    public int? ActionFlowExecutionConfirmationPositionY { get; set; }

    [JsonPropertyName("actionFlowExecutionDelayPositionX")]
    public int? ActionFlowExecutionDelayPositionX { get; set; }

    [JsonPropertyName("actionFlowExecutionDelayPositionY")]
    public int? ActionFlowExecutionDelayPositionY { get; set; }

    bool _floatingWindowHorizontal;

    [JsonPropertyName("floatingWindowHorizontal")]
    public bool FloatingWindowHorizontal
    {
        get => _floatingWindowHorizontal;
        set
        {
            if (value == _floatingWindowHorizontal) return;
            _floatingWindowHorizontal = value;
            OnPropertyChanged();
        }
    }

    [JsonPropertyName("floatingWindowButtonOrder")]
    public List<string> FloatingWindowButtonOrder { get; set; } = new();

    [JsonPropertyName("floatingWindowButtonRows")]
    public List<List<string>> FloatingWindowButtonRows { get; set; } = new();

    [JsonPropertyName("floatingWindowButtonRulesets")]
    public Dictionary<string, ButtonRulesetConfig> FloatingWindowButtonRulesets { get; set; } = new();

    [JsonPropertyName("floatingWindowRowRulesets")]
    public List<RowRulesetConfig> FloatingWindowRowRulesets { get; set; } = new();

    // ===== 以下 7 个 A 档成员由 p1-04 按需增补（p1-05 §2.3-4：聚合配置根仅含 A 档成员）；
    // 源锚点：E:\My Github Projects\SystemTools\ConfigHandlers\MainConfigData.cs
    //   VirtualAfterSchool* :141-183；EnableAiService :244-257；AiApiKey :323-336；AiApiUrl :338-351；AiModel :353-366。
    // JSON 属性名与源插件同名成员保持一致；源 EnableAiService 另发的 RestartPropertyChanged
    // 事件未随入（阶段 1 裁剪形无该事件，其消费方为设置页重启提示，属礼部 p1-06 增补面）。
    // 源 AI 液态玻璃成员（AiConversationFloatingWindowStyle/AiConversationLiquidGlass/AiConversationApprovalButtonGlass）
    // 不增补：U5/R-6 降级决议下新插件 AI 浮窗仅经典外观，无消费方。

    bool _virtualAfterSchoolEnabled;

    [JsonPropertyName("virtualAfterSchoolEnabled")]
    public bool VirtualAfterSchoolEnabled
    {
        get => _virtualAfterSchoolEnabled;
        set
        {
            if (value == _virtualAfterSchoolEnabled) return;
            _virtualAfterSchoolEnabled = value;
            OnPropertyChanged();
        }
    }

    TimeSpan _virtualAfterSchoolTriggerTime = new(12, 10, 0);

    [JsonPropertyName("virtualAfterSchoolTriggerTime")]
    public TimeSpan VirtualAfterSchoolTriggerTime
    {
        get => _virtualAfterSchoolTriggerTime;
        set
        {
            if (value < TimeSpan.Zero || value >= TimeSpan.FromDays(1) || value == _virtualAfterSchoolTriggerTime)
                return;
            _virtualAfterSchoolTriggerTime = value;
            OnPropertyChanged();
        }
    }

    int _virtualAfterSchoolDurationSeconds = 60;

    [JsonPropertyName("virtualAfterSchoolDurationSeconds")]
    public int VirtualAfterSchoolDurationSeconds
    {
        get => _virtualAfterSchoolDurationSeconds;
        set
        {
            var clamped = Math.Clamp(value, 1, 7200);
            if (clamped == _virtualAfterSchoolDurationSeconds) return;
            _virtualAfterSchoolDurationSeconds = clamped;
            OnPropertyChanged();
        }
    }

    bool _enableAiService;

    [JsonPropertyName("enableAiService")]
    public bool EnableAiService
    {
        get => _enableAiService;
        set
        {
            if (value == _enableAiService) return;
            _enableAiService = value;
            OnPropertyChanged();
        }
    }

    string _aiApiKey = string.Empty;

    [JsonPropertyName("aiApiKey")]
    public string AiApiKey
    {
        get => _aiApiKey;
        set
        {
            value ??= string.Empty;
            if (string.Equals(value, _aiApiKey, StringComparison.Ordinal)) return;
            _aiApiKey = value;
            OnPropertyChanged();
        }
    }

    string _aiApiUrl = "https://api.openai.com/v1";

    [JsonPropertyName("aiApiUrl")]
    public string AiApiUrl
    {
        get => _aiApiUrl;
        set
        {
            value ??= string.Empty;
            if (string.Equals(value, _aiApiUrl, StringComparison.Ordinal)) return;
            _aiApiUrl = value;
            OnPropertyChanged();
        }
    }

    string _aiModel = string.Empty;

    [JsonPropertyName("aiModel")]
    public string AiModel
    {
        get => _aiModel;
        set
        {
            value ??= string.Empty;
            if (string.Equals(value, _aiModel, StringComparison.Ordinal)) return;
            _aiModel = value;
            OnPropertyChanged();
        }
    }

    // ===== p1-04 增补结束 =====

    // ===== 以下 2 个 A 档成员由 p1-06 按需增补（p1-05 §2.3-4 共享配置根成员增补流程；尚书省派工预批，
    // 属主批 p1-03 已确认免另行确认）：
    //   AiProviderName                 —— 主设置页 AI 供应商名称绑定（源 MainConfigData.cs:308-321）。
    //   ShareAiRepliesWithClassIslandNotifications —— AiChatSettingsViewModel.IsClassIslandNotificationSharingEnabled
    //       绑定消费（源 MainConfigData.cs:368-380；VM 消费点源 SettingsPage\AiChatSettingsViewModel.cs:155-168）。
    // JSON 属性名与源插件同名成员保持一致；守卫语义逐行随源。

    string _aiProviderName = "OpenAI";

    [JsonPropertyName("aiProviderName")]
    public string AiProviderName
    {
        get => _aiProviderName;
        set
        {
            value ??= string.Empty;
            if (string.Equals(value, _aiProviderName, StringComparison.Ordinal)) return;
            _aiProviderName = value;
            OnPropertyChanged();
        }
    }

    bool _shareAiRepliesWithClassIslandNotifications;

    [JsonPropertyName("shareAiRepliesWithClassIslandNotifications")]
    public bool ShareAiRepliesWithClassIslandNotifications
    {
        get => _shareAiRepliesWithClassIslandNotifications;
        set
        {
            if (value == _shareAiRepliesWithClassIslandNotifications) return;
            _shareAiRepliesWithClassIslandNotifications = value;
            OnPropertyChanged();
        }
    }

    // ===== p1-06 增补结束 =====

    // ===== 以下 1 个 B 档成员由 p2-01 按需增补（p1-05 §2.3-4 与 p2-05 §2.1 #10 增补流程，尚书省派工预批；
    // 消费批 p2-01）：
    //   AutoCleanupClassIslandMemory —— ClassIslandMemoryAutoCleanupService.ApplyConfig() 消费
    //       （源锚点 E:\My Github Projects\SystemTools\ConfigHandlers\MainConfigData.cs:187-199；
    //        消费点源 Services\ClassIslandMemoryAutoCleanupService.cs:27 经 GlobalConstants.MainConfig?.Data）。
    // JSON 属性名与源插件同名成员保持一致；守卫语义逐行随源（相同值跳过 + PropertyChanged）。
    // 注册顺序注记（p2-05 §2.1 #10）：ApplyConfig 调用须在 GlobalConstants.MainConfig 赋值之后
    // （源 Plugin.cs:70 先例已由 p1-06 落实；源 :218 先例由礼部注册清单承载）。

    bool _autoCleanupClassIslandMemory;

    [JsonPropertyName("autoCleanupClassIslandMemory")]
    public bool AutoCleanupClassIslandMemory
    {
        get => _autoCleanupClassIslandMemory;
        set
        {
            if (value == _autoCleanupClassIslandMemory) return;
            _autoCleanupClassIslandMemory = value;
            OnPropertyChanged();
        }
    }

    // ===== p2-01 增补结束 =====

    // ===== 以下 14 个 B 档成员（7 组）由 p2-03 增补（p1-05 §2.3-4 共享配置根成员增补流程；
    // 尚书省预批 p2-05 §2.1 #1-#7）：
    //   EnableFloatingWindowFeature    —— B11/B12 行动注册组门 + 悬浮窗服务/触发器生命周期门
    //                                     （源 Plugin.cs:414/:475/:210-213/:1061-1064；源成员 :54-65）。
    //   ShowFloatingWindow             —— B11 行动写（源 Actions\ShowFloatingWindowAction.cs:47/:88）
    //                                     + FloatingWindowService 读（源 :1198）；源成员 :428-440。
    //   悬浮窗外观 6 成员               —— FloatingWindowService 经典外观消费
    //                                     （Scale/TextSize/IconSize/Opacity/ShadowEnabled/
    //                                     DragHandleAlwaysVisible；源 :463-536/:651-663）。
    //   FloatingWindowPositionX/Y      —— FloatingWindowService 位置记忆（源 :2097-2132）。
    //   FloatingWindowLayer            —— B12 行动写（源 Actions\ToggleFloatingWindowLayerAction.cs:43/:53）
    //                                     + FloatingWindowService 层级应用（源 :2329-2337）；源成员 :593-606。
    //   FloatingWindowLayerRecheckMode —— 源成员 :608-621 随源引入保持配置兼容；R-3 降级口径下
    //                                     源层级自动重检运行时不启用，服务端不消费本成员
    //                                     （p2-05 §2.1 #6 已批注记）。
    //   FloatingWindowRulesetEnabled
    //   + FloatingWindowRuleset        —— FloatingWindowService 整窗规则隐藏（R-3 保留面，
    //                                     源 :1055-1082）；源成员 :637-649/:665-666。
    // JSON 属性名与源插件同名成员保持一致；守卫语义逐行随源（归一化/钳制 + 相同值跳过 +
    // PropertyChanged）。源 EnableFloatingWindowFeature 另发的 RestartPropertyChanged 事件
    // 未随入（尚书省裁定沿用 p1-06 §9-7 等价口径：配置变更经既有 ApplyConfig/lifecycle 路径生效，
    // 不引入重启提示事件）。源液态玻璃成员（FloatingWindowAppearanceStyle/FloatingWindowLiquidGlass/
    // FloatingWindowGlassButtonScaleDip）不增补：U5/R-6 降级决议下新插件悬浮窗仅经典外观，
    // 消费方（液态玻璃捕获/外观/交互按钮面）不迁，禁引用（p1-10 §12.5 漂移点 B 档零消费同口径）。

    bool _enableFloatingWindowFeature = true;

    [JsonPropertyName("enableFloatingWindowFeature")]
    public bool EnableFloatingWindowFeature
    {
        get => _enableFloatingWindowFeature;
        set
        {
            if (value == _enableFloatingWindowFeature) return;
            _enableFloatingWindowFeature = value;
            OnPropertyChanged();
        }
    }

    bool _showFloatingWindow = true;

    [JsonPropertyName("showFloatingWindow")]
    public bool ShowFloatingWindow
    {
        get => _showFloatingWindow;
        set
        {
            if (value == _showFloatingWindow) return;
            _showFloatingWindow = value;
            OnPropertyChanged();
        }
    }

    double _floatingWindowScale = 1.0;

    [JsonPropertyName("floatingWindowScale")]
    public double FloatingWindowScale
    {
        get => _floatingWindowScale;
        set
        {
            var clamped = Math.Clamp(value, 0.5, 2.0);
            if (Math.Abs(clamped - _floatingWindowScale) < 0.0001) return;
            _floatingWindowScale = clamped;
            OnPropertyChanged();
        }
    }

    int _floatingWindowTextSize = 12;

    [JsonPropertyName("floatingWindowTextSize")]
    public int FloatingWindowTextSize
    {
        get => _floatingWindowTextSize;
        set
        {
            var clamped = Math.Clamp(value, 8, 30);
            if (clamped == _floatingWindowTextSize) return;
            _floatingWindowTextSize = clamped;
            OnPropertyChanged();
        }
    }

    int _floatingWindowIconSize = 22;

    [JsonPropertyName("floatingWindowIconSize")]
    public int FloatingWindowIconSize
    {
        get => _floatingWindowIconSize;
        set
        {
            var clamped = Math.Clamp(value, 15, 50);
            if (clamped == _floatingWindowIconSize) return;
            _floatingWindowIconSize = clamped;
            OnPropertyChanged();
        }
    }

    int _floatingWindowOpacity = 80;

    [JsonPropertyName("floatingWindowOpacity")]
    public int FloatingWindowOpacity
    {
        get => _floatingWindowOpacity;
        set
        {
            var clamped = Math.Clamp(value, 10, 100);
            if (clamped == _floatingWindowOpacity) return;
            _floatingWindowOpacity = clamped;
            OnPropertyChanged();
        }
    }

    bool _floatingWindowShadowEnabled = true;

    [JsonPropertyName("floatingWindowShadowEnabled")]
    public bool FloatingWindowShadowEnabled
    {
        get => _floatingWindowShadowEnabled;
        set
        {
            if (value == _floatingWindowShadowEnabled) return;
            _floatingWindowShadowEnabled = value;
            OnPropertyChanged();
        }
    }

    bool _floatingWindowDragHandleAlwaysVisible = false;

    [JsonPropertyName("floatingWindowDragHandleAlwaysVisible")]
    public bool FloatingWindowDragHandleAlwaysVisible
    {
        get => _floatingWindowDragHandleAlwaysVisible;
        set
        {
            if (value == _floatingWindowDragHandleAlwaysVisible) return;
            _floatingWindowDragHandleAlwaysVisible = value;
            OnPropertyChanged();
        }
    }

    int _floatingWindowPositionX = 100;

    [JsonPropertyName("floatingWindowPositionX")]
    public int FloatingWindowPositionX
    {
        get => _floatingWindowPositionX;
        set
        {
            if (value == _floatingWindowPositionX) return;
            _floatingWindowPositionX = value;
            OnPropertyChanged();
        }
    }

    int _floatingWindowPositionY = 100;

    [JsonPropertyName("floatingWindowPositionY")]
    public int FloatingWindowPositionY
    {
        get => _floatingWindowPositionY;
        set
        {
            if (value == _floatingWindowPositionY) return;
            _floatingWindowPositionY = value;
            OnPropertyChanged();
        }
    }

    int _floatingWindowLayer = 1;

    [JsonPropertyName("floatingWindowLayer")]
    public int FloatingWindowLayer
    {
        get => _floatingWindowLayer;
        set
        {
            var normalized = value is 0 or 1 ? value : 1;
            if (normalized == _floatingWindowLayer) return;
            _floatingWindowLayer = normalized;
            OnPropertyChanged();
        }
    }

    int _floatingWindowLayerRecheckMode = 1;

    [JsonPropertyName("floatingWindowLayerRecheckMode")]
    public int FloatingWindowLayerRecheckMode
    {
        get => _floatingWindowLayerRecheckMode;
        set
        {
            var normalized = Math.Clamp(value, 0, 3);
            if (normalized == _floatingWindowLayerRecheckMode) return;
            _floatingWindowLayerRecheckMode = normalized;
            OnPropertyChanged();
        }
    }

    bool _floatingWindowRulesetEnabled = false;

    [JsonPropertyName("floatingWindowRulesetEnabled")]
    public bool FloatingWindowRulesetEnabled
    {
        get => _floatingWindowRulesetEnabled;
        set
        {
            if (value == _floatingWindowRulesetEnabled) return;
            _floatingWindowRulesetEnabled = value;
            OnPropertyChanged();
        }
    }

    [JsonPropertyName("floatingWindowRuleset")]
    public Ruleset FloatingWindowRuleset { get; set; } = new();

    // ===== p2-03 增补结束 =====

    // 行动功能启用状态（Key: 行动ID, Value: 是否启用）
    [JsonPropertyName("enabledActions")] public Dictionary<string, bool> EnabledActions { get; set; } = new();

    // 触发器功能启用状态
    [JsonPropertyName("enabledTriggers")] public Dictionary<string, bool> EnabledTriggers { get; set; } = new();

    // 组件功能启用状态
    [JsonPropertyName("enabledComponents")]
    public Dictionary<string, bool> EnabledComponents { get; set; } = new();

    // 规则功能启用状态
    [JsonPropertyName("enabledRules")]
    public Dictionary<string, bool> EnabledRules { get; set; } = new();

    // 添加辅助方法检查功能是否启用
    public bool IsActionEnabled(string actionId) =>
        !EnabledActions.TryGetValue(actionId, out var enabled) || enabled;

    public bool IsTriggerEnabled(string triggerId) =>
        !EnabledTriggers.TryGetValue(triggerId, out var enabled) || enabled;

    public bool IsComponentEnabled(string componentId) =>
        !EnabledComponents.TryGetValue(componentId, out var enabled) || enabled;

    public bool IsRuleEnabled(string ruleId) =>
        !EnabledRules.TryGetValue(ruleId, out var enabled) || enabled;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}