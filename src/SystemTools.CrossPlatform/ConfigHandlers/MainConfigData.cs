using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using ClassIsland.Core.Models.Ruleset;

namespace SystemTools.CrossPlatform.ConfigHandlers;

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

    [JsonPropertyName("enabledActions")] public Dictionary<string, bool> EnabledActions { get; set; } = new();

    [JsonPropertyName("enabledTriggers")] public Dictionary<string, bool> EnabledTriggers { get; set; } = new();

    [JsonPropertyName("enabledComponents")]
    public Dictionary<string, bool> EnabledComponents { get; set; } = new();

    [JsonPropertyName("enabledRules")]
    public Dictionary<string, bool> EnabledRules { get; set; } = new();

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