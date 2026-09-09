using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Shared;
using SystemTools.CrossPlatform.Actions;
using SystemTools.CrossPlatform.ConfigHandlers;
using SystemTools.CrossPlatform.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SystemTools.CrossPlatform.SettingsPage;

public partial class FloatingTriggerItem : ObservableObject
{
    [ObservableProperty] private string _buttonId = string.Empty;
    [ObservableProperty] private string _icon = string.Empty;
    [ObservableProperty] private string _buttonName = string.Empty;
    [ObservableProperty] private ButtonRulesetConfig _config = new();

    public FluentAvalonia.UI.Controls.FAIconSource? IconSource =>
        Services.FloatingWindowIconProvider.TryResolveIconSource(Icon);

    partial void OnIconChanged(string value) { OnPropertyChanged(nameof(IconSource)); }
}

public partial class FloatingTriggerRow : ObservableObject
{
    [ObservableProperty] private ObservableCollection<FloatingTriggerItem> _buttons = new();
    [ObservableProperty] private int _rowIndex = 0;
    [ObservableProperty] private RowRulesetConfig _rowRuleset = new();
}

public enum FeatureItemType
{
    Action,
    Trigger,
    Component,
    Rule
}

public partial class UnifiedFeatureItem : ObservableObject
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private FeatureItemType _itemType;
    [ObservableProperty] private string? _groupName;

    public string TypeDisplayName => ItemType switch
    {
        FeatureItemType.Action => "行动",
        FeatureItemType.Trigger => "触发器",
        FeatureItemType.Component => "组件",
        FeatureItemType.Rule => "规则",
        _ => "未知"
    };
}


public partial class SystemToolsSettingsViewModel : ObservableObject
{
    private readonly MainConfigHandler _configHandler;
    private readonly FloatingWindowProfileManager? _profileManager;

    public SystemToolsSettingsViewModel(MainConfigHandler configHandler, FloatingWindowProfileManager? profileManager)
    {
        _configHandler = configHandler;
        _profileManager = profileManager;
    }


    private readonly FloatingWindowService? _floatingWindowService;
    private readonly EventHandler? _entriesChangedHandler;

    public SystemToolsSettingsViewModel(
        MainConfigHandler configHandler,
        FloatingWindowProfileManager? profileManager,
        FloatingWindowService? floatingWindowService)
    {
        _configHandler = configHandler;
        _profileManager = profileManager;
        _floatingWindowService = floatingWindowService;
        if (_floatingWindowService != null)
        {
            _entriesChangedHandler = (_, _) =>
                Avalonia.Threading.Dispatcher.UIThread.Post(RefreshFloatingTriggers);
            _floatingWindowService.EntriesChanged += _entriesChangedHandler;
        }
    }
    
    public MainConfigData Settings => _configHandler.Data;

    public ObservableCollection<string> FloatingWindowProfileNames { get; } = [];

    public string CurrentFloatingWindowProfileName => _profileManager?.CurrentProfileName ?? string.Empty;

    public void RefreshFloatingWindowProfiles()
    {
        FloatingWindowProfileNames.Clear();
        if (_profileManager is null)
        {
            return;
        }

        foreach (var name in _profileManager.GetProfileNames())
        {
            FloatingWindowProfileNames.Add(name);
        }
    }

    public void SelectFloatingWindowProfile(string profileName)
    {
        if (_profileManager is null || string.IsNullOrWhiteSpace(profileName))
        {
            return;
        }

        if (_profileManager.ProfileFileExists(_profileManager.CurrentProfileName))
        {
            _profileManager.SaveProfile();
            _profileManager.LoadProfile(profileName);
        }
        else
        {
            _profileManager.LoadProfile(profileName);
        }

        _configHandler.Data.CurrentFloatingWindowProfile = profileName;
        _configHandler.Save();
        OnPropertyChanged(nameof(CurrentFloatingWindowProfileName));
        OnPropertyChanged(nameof(CurrentFloatingWindowProfile));
    }

    public FloatingWindowProfile? CurrentFloatingWindowProfile => _profileManager?.CurrentProfile;

    public string FloatingWindowProfilesDirectory => _profileManager?.ProfilesDirectory ?? string.Empty;

    public void AddFloatingWindowProfile(string? name = null)
    {
        if (_profileManager is null)
        {
            return;
        }

        var newName = _profileManager.CreateProfile(name);
        RefreshFloatingWindowProfiles();
        SelectFloatingWindowProfile(newName);
    }

    public void RemoveFloatingWindowProfile(string profileName)
    {
        if (_profileManager is null || string.IsNullOrWhiteSpace(profileName))
        {
            return;
        }

        if (_profileManager.RemoveProfile(profileName))
        {
            RefreshFloatingWindowProfiles();
            if (string.Equals(CurrentFloatingWindowProfileName, profileName, StringComparison.OrdinalIgnoreCase))
            {
                SelectFloatingWindowProfile("Default");
            }
        }
    }

    public ObservableCollection<FloatingTriggerRow> FloatingTriggerRows { get; } = [];

    public bool HasFloatingTriggerEntries { get; private set; }

    public void RefreshFloatingTriggers()
    {
        if (_floatingWindowService is null || _profileManager is null)
        {
            return;
        }

        _floatingWindowService.EnsureUniqueButtonIds();
        var entries = _floatingWindowService.Entries
            .GroupBy(x => x.ButtonId)
            .ToDictionary(x => x.Key, x => x.First());
        HasFloatingTriggerEntries = entries.Count > 0;
        OnPropertyChanged(nameof(HasFloatingTriggerEntries));

        var profile = CurrentFloatingWindowProfile!;
        var globalShow = _configHandler.Data.ShowFloatingWindow;
        if (!HasFloatingTriggerEntries && globalShow)
        {
            _configHandler.Data.ShowFloatingWindow = false;
            _configHandler.Save();
            _floatingWindowService.UpdateWindowState();
        }

        if (profile.PruneInvalidButtonIds(entries.Keys))
        {
            _profileManager.SaveProfile();
        }

        var configuredIds = new HashSet<string>();
        foreach (var row in profile.FloatingWindowButtonRows ?? [])
        {
            foreach (var id in row)
            {
                configuredIds.Add(id);
            }
        }

        if (configuredIds.Count == 0 && entries.Count > 0)
        {
            var allButtonIds = entries.Values.Select(e => e.ButtonId).ToList();
            if (profile.FloatingWindowButtonRows == null || profile.FloatingWindowButtonRows.Count == 0)
            {
                profile.FloatingWindowButtonRows = [allButtonIds];
            }
            else
            {
                profile.FloatingWindowButtonRows[0] = allButtonIds;
            }
            foreach (var id in allButtonIds)
            {
                configuredIds.Add(id);
            }
            _profileManager.SaveProfile();
        }

        var newButtonIds = entries.Values
            .Where(e => !configuredIds.Contains(e.ButtonId))
            .Where(e => !profile.FloatingWindowButtonRulesets.ContainsKey(e.ButtonId))
            .Select(e => e.ButtonId)
            .ToList();
        if (newButtonIds.Count > 0)
        {
            if (profile.FloatingWindowButtonRows == null || profile.FloatingWindowButtonRows.Count == 0)
            {
                profile.FloatingWindowButtonRows = [newButtonIds];
            }
            else
            {
                profile.FloatingWindowButtonRows[0] = [.. profile.FloatingWindowButtonRows[0], .. newButtonIds];
            }
            foreach (var id in newButtonIds)
            {
                configuredIds.Add(id);
            }
            _profileManager.SaveProfile();
        }

        foreach (var oldRow in FloatingTriggerRows)
        {
            oldRow.RowRuleset.PropertyChanged -= OnRowRulesetPropertyChanged;
            if (oldRow.RowRuleset.HidingRules is INotifyPropertyChanged oldRowHidingRules)
            {
                oldRowHidingRules.PropertyChanged -= OnRowRulesetPropertyChanged;
            }
            foreach (var oldItem in oldRow.Buttons)
            {
                oldItem.Config.PropertyChanged -= OnButtonConfigPropertyChanged;
                if (oldItem.Config.HidingRules is INotifyPropertyChanged oldBtnHidingRules)
                {
                    oldBtnHidingRules.PropertyChanged -= OnButtonConfigPropertyChanged;
                }
            }
        }

        FloatingTriggerRows.Clear();
        var rowConfigs = profile.FloatingWindowRowRulesets;
        var rowIndex = 0;
        var needSave = false;
        foreach (var row in profile.FloatingWindowButtonRows ?? [])
        {
            while (rowConfigs.Count <= rowIndex)
            {
                rowConfigs.Add(new RowRulesetConfig());
                needSave = true;
            }
            var vmRow = new FloatingTriggerRow
            {
                RowIndex = rowIndex + 1,
                RowRuleset = rowConfigs[rowIndex]
            };
            vmRow.RowRuleset.PropertyChanged += OnRowRulesetPropertyChanged;
            if (vmRow.RowRuleset.HidingRules is INotifyPropertyChanged rowHidingRules)
            {
                rowHidingRules.PropertyChanged += OnRowRulesetPropertyChanged;
            }
            foreach (var id in row)
            {
                if (!entries.TryGetValue(id, out var entry))
                {
                    continue;
                }
                if (!profile.FloatingWindowButtonRulesets.TryGetValue(entry.ButtonId, out var btnConfig))
                {
                    btnConfig = new ButtonRulesetConfig();
                    profile.FloatingWindowButtonRulesets[entry.ButtonId] = btnConfig;
                    needSave = true;
                }
                var item = new FloatingTriggerItem
                {
                    ButtonId = entry.ButtonId,
                    Icon = entry.Icon,
                    ButtonName = entry.LayoutName,
                    Config = btnConfig
                };
                item.Config.PropertyChanged += OnButtonConfigPropertyChanged;
                if (item.Config.HidingRules is INotifyPropertyChanged btnHidingRules)
                {
                    btnHidingRules.PropertyChanged += OnButtonConfigPropertyChanged;
                }
                vmRow.Buttons.Add(item);
            }
            FloatingTriggerRows.Add(vmRow);
            rowIndex++;
        }

        if (FloatingTriggerRows.Count == 0)
        {
            if (rowConfigs.Count == 0)
            {
                rowConfigs.Add(new RowRulesetConfig());
                needSave = true;
            }
            var emptyRow = new FloatingTriggerRow
            {
                RowIndex = 1,
                RowRuleset = rowConfigs[0]
            };
            emptyRow.RowRuleset.PropertyChanged += OnRowRulesetPropertyChanged;
            if (emptyRow.RowRuleset.HidingRules is INotifyPropertyChanged emptyRowHidingRules)
            {
                emptyRowHidingRules.PropertyChanged += OnRowRulesetPropertyChanged;
            }
            FloatingTriggerRows.Add(emptyRow);
        }

        if (needSave)
        {
            _profileManager.SaveProfile();
        }
    }

    private void OnButtonConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsRulesetStateProperty(e.PropertyName))
        {
            return;
        }

        _profileManager?.SaveProfile();
        _floatingWindowService?.UpdateWindowState();
        NotifyRulesetStatusChanged();
    }

    private void OnRowRulesetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsRulesetStateProperty(e.PropertyName))
        {
            return;
        }

        _profileManager?.SaveProfile();
        _floatingWindowService?.UpdateWindowState();
        NotifyRulesetStatusChanged();
    }

    private void NotifyRulesetStatusChanged()
    {
        IAppHost.TryGetService<IRulesetService>()?.NotifyStatusChanged();
    }

    private static bool IsRulesetStateProperty(string? propertyName)
    {
        return propertyName == nameof(ClassIsland.Core.Models.Ruleset.Ruleset.State)
            || propertyName == nameof(ClassIsland.Core.Models.Ruleset.RuleGroup.State)
            || propertyName == nameof(ClassIsland.Core.Models.Ruleset.Rule.State);
    }

    public void AddFloatingTriggerRow()
    {
        if (_profileManager is null)
        {
            return;
        }

        var profile = CurrentFloatingWindowProfile!;
        var rowRulesets = profile.FloatingWindowRowRulesets;
        var newRowRuleset = new RowRulesetConfig();
        rowRulesets.Add(newRowRuleset);
        var newRow = new FloatingTriggerRow
        {
            RowIndex = FloatingTriggerRows.Count + 1,
            RowRuleset = newRowRuleset
        };
        newRow.RowRuleset.PropertyChanged += OnRowRulesetPropertyChanged;
        if (newRow.RowRuleset.HidingRules is INotifyPropertyChanged rowHidingRules)
        {
            rowHidingRules.PropertyChanged += OnRowRulesetPropertyChanged;
        }
        FloatingTriggerRows.Add(newRow);
        PersistFloatingTriggerRows();
    }

    public void InsertFloatingTriggerRow(int insertIndex)
    {
        if (_profileManager is null)
        {
            return;
        }

        var profile = CurrentFloatingWindowProfile!;
        var rowRulesets = profile.FloatingWindowRowRulesets;
        insertIndex = Math.Clamp(insertIndex, 0, FloatingTriggerRows.Count);
        var newRowRuleset = new RowRulesetConfig();
        rowRulesets.Insert(insertIndex, newRowRuleset);
        var newRow = new FloatingTriggerRow
        {
            RowIndex = insertIndex + 1,
            RowRuleset = newRowRuleset
        };
        newRow.RowRuleset.PropertyChanged += OnRowRulesetPropertyChanged;
        if (newRow.RowRuleset.HidingRules is INotifyPropertyChanged rowHidingRules)
        {
            rowHidingRules.PropertyChanged += OnRowRulesetPropertyChanged;
        }
        FloatingTriggerRows.Insert(insertIndex, newRow);

        for (int i = insertIndex; i < FloatingTriggerRows.Count; i++)
        {
            FloatingTriggerRows[i].RowIndex = i + 1;
        }

        PersistFloatingTriggerRows();
    }

    public bool RemoveFloatingTriggerRow(FloatingTriggerRow row)
    {
        if (_profileManager is null)
        {
            return false;
        }

        var index = FloatingTriggerRows.IndexOf(row);
        if (index < 0 || FloatingTriggerRows.Count <= 1)
        {
            return false;
        }

        row.RowRuleset.PropertyChanged -= OnRowRulesetPropertyChanged;
        if (row.RowRuleset.HidingRules is INotifyPropertyChanged rowHidingRules)
        {
            rowHidingRules.PropertyChanged -= OnRowRulesetPropertyChanged;
        }

        var targetRow = index > 0 ? FloatingTriggerRows[index - 1] : FloatingTriggerRows[index + 1];
        foreach (var item in row.Buttons)
        {
            targetRow.Buttons.Add(item);
        }

        FloatingTriggerRows.RemoveAt(index);

        for (int i = 0; i < FloatingTriggerRows.Count; i++)
        {
            FloatingTriggerRows[i].RowIndex = i + 1;
        }

        PersistFloatingTriggerRows();
        return true;
    }

    public void PersistFloatingTriggerRows(bool updateWindow = true, bool forceSave = true)
    {
        if (_profileManager is null)
        {
            return;
        }

        var profile = CurrentFloatingWindowProfile!;
        var newRows = FloatingTriggerRows
            .Select(row => row.Buttons.Select(x => x.ButtonId).ToList())
            .ToList();
        var newOrder = newRows
            .SelectMany(row => row)
            .ToList();

        var rowsChanged = !AreRowsEqual(profile.FloatingWindowButtonRows, newRows);
        var orderChanged = !(profile.FloatingWindowButtonOrder ?? []).SequenceEqual(newOrder);

        if (rowsChanged)
        {
            profile.FloatingWindowButtonRows = newRows;
        }

        if (orderChanged)
        {
            profile.FloatingWindowButtonOrder = newOrder;
        }

        var rowRulesets = profile.FloatingWindowRowRulesets;
        while (rowRulesets.Count < FloatingTriggerRows.Count)
        {
            rowRulesets.Add(new RowRulesetConfig());
        }
        while (rowRulesets.Count > FloatingTriggerRows.Count)
        {
            var removedRowRuleset = rowRulesets[rowRulesets.Count - 1];
            removedRowRuleset.PropertyChanged -= OnRowRulesetPropertyChanged;
            if (removedRowRuleset.HidingRules is INotifyPropertyChanged removedHidingRules)
            {
                removedHidingRules.PropertyChanged -= OnRowRulesetPropertyChanged;
            }
            rowRulesets.RemoveAt(rowRulesets.Count - 1);
        }
        for (int i = 0; i < FloatingTriggerRows.Count; i++)
        {
            var vmRow = FloatingTriggerRows[i];
            if (!ReferenceEquals(vmRow.RowRuleset, rowRulesets[i]))
            {
                vmRow.RowRuleset.PropertyChanged -= OnRowRulesetPropertyChanged;
                if (vmRow.RowRuleset.HidingRules is INotifyPropertyChanged oldHidingRules)
                {
                    oldHidingRules.PropertyChanged -= OnRowRulesetPropertyChanged;
                }
                vmRow.RowRuleset = rowRulesets[i];
                vmRow.RowRuleset.PropertyChanged += OnRowRulesetPropertyChanged;
                if (vmRow.RowRuleset.HidingRules is INotifyPropertyChanged newHidingRules)
                {
                    newHidingRules.PropertyChanged += OnRowRulesetPropertyChanged;
                }
            }
        }

        var usedButtonIds = new HashSet<string>(newOrder);
        var staleButtonIds = profile.FloatingWindowButtonRulesets.Keys.Where(id => !usedButtonIds.Contains(id)).ToList();
        foreach (var staleId in staleButtonIds)
        {
            profile.FloatingWindowButtonRulesets.Remove(staleId);
        }

        if (forceSave)
        {
            _profileManager.SaveProfile();
        }

        if (updateWindow)
        {
            _floatingWindowService?.UpdateWindowState();
        }
    }

    private static bool AreRowsEqual(IReadOnlyList<List<string>>? left, IReadOnlyList<List<string>> right)
    {
        if (left == null)
        {
            return right.Count == 0;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (!left[i].SequenceEqual(right[i]))
            {
                return false;
            }
        }

        return true;
    }

    public void Dispose()
    {
        if (_floatingWindowService != null && _entriesChangedHandler != null)
        {
            _floatingWindowService.EntriesChanged -= _entriesChangedHandler;
        }
    }

    [ObservableProperty] private ObservableCollection<UnifiedFeatureItem> _featureItems = new();
    [ObservableProperty] private ObservableCollection<UnifiedFeatureItem> _featureSearchResults = new();

    public bool IsFeatureSearchEmpty => FeatureSearchResults.Count == 0;

    [ObservableProperty] private bool _isFeatureDrawerOpen = false;
    [ObservableProperty] private object? _featureDrawerContent;

    public void InitializeFeatureItems()
    {
        FeatureItems.Clear();

        var components = new[]
        {
            ("SystemTools.CrossPlatform.NetworkStatus", "网络延迟"),
            ("SystemTools.CrossPlatform.ClipboardContent", "显示剪切板内容"),
            ("SystemTools.CrossPlatform.LocalQuote", "本地一言"),
            ("SystemTools.CrossPlatform.NextClassDisplay", "下节课是"),
            ("SystemTools.CrossPlatform.BetterCarouselContainer", "更好的轮播容器"),
            ("SystemTools.CrossPlatform.ScrollingText", " LED 文本仿真显示框"),
        };
        foreach (var (id, name) in components)
        {
            FeatureItems.Add(new UnifiedFeatureItem
            {
                Id = id,
                DisplayName = name,
                IsEnabled = Settings.IsComponentEnabled(id),
                ItemType = FeatureItemType.Component,
                GroupName = null
            });
        }

        var triggers = new List<(string Id, string Name)>
        {
            ("SystemTools.CrossPlatform.ActionInProgressTrigger", "行动进行时"),
        };

        if (Settings.EnableFloatingWindowFeature)
        {
            triggers.Add(("SystemTools.CrossPlatform.FloatingWindowTrigger", "从悬浮窗触发"));
        }
        foreach (var (id, name) in triggers)
        {
            FeatureItems.Add(new UnifiedFeatureItem
            {
                Id = id,
                DisplayName = name,
                IsEnabled = Settings.IsTriggerEnabled(id),
                ItemType = FeatureItemType.Trigger,
                GroupName = null
            });
        }

        var rules = new List<(string Id, string Name)>
        {
            ("SystemTools.CrossPlatform.ProcessRunningRule", "程序正在运行"),
            ("SystemTools.CrossPlatform.UsingClassPlanRule", "正在使用某课程表"),
            ("SystemTools.CrossPlatform.UsingTimeLayoutRule", "正在使用某时间表"),
            ("SystemTools.CrossPlatform.InTimePeriodRule", "是否在某时间段")
        };
        foreach (var (id, name) in rules)
        {
            FeatureItems.Add(new UnifiedFeatureItem
            {
                Id = id,
                DisplayName = name,
                IsEnabled = Settings.IsRuleEnabled(id),
                ItemType = FeatureItemType.Rule,
                GroupName = null
            });
        }

        var actions = new List<(string Id, string Name, string? Group)>
        {
            ("SystemTools.CrossPlatform.Shutdown", "计时关机", "电源选项"),
            ("SystemTools.CrossPlatform.AdvancedShutdown", "高级计时关机", "电源选项"),
            ("SystemTools.CrossPlatform.CancelShutdown", "取消关机计划", "电源选项"),
            ("SystemTools.CrossPlatform.LockScreen", "锁定屏幕", "电源选项"),
            ("SystemTools.CrossPlatform.ImmediateRestart", "立即重启", "电源选项"),
            ("SystemTools.CrossPlatform.ImmediateShutdown", "立即关机", "电源选项"),
            ("SystemTools.CrossPlatform.Sleep", "睡眠", "电源选项"),
            ("SystemTools.CrossPlatform.Copy", "复制", "文件操作"),
            ("SystemTools.CrossPlatform.Move", "移动", "文件操作"),
            ("SystemTools.CrossPlatform.Delete", "删除", "文件操作"),
            ("SystemTools.CrossPlatform.FullscreenClock", "沉浸式时钟", "其他工具"),
            ("SystemTools.CrossPlatform.KillProcess", "退出进程", "实用工具"),
            ("SystemTools.CrossPlatform.ShowToast", "拉起自定义系统通知", "实用工具"),
            ("SystemTools.CrossPlatform.BackgroundPlayAudio", "后台播放音频", "媒体工具"),
            ("SystemTools.CrossPlatform.TriggerCustomTrigger", "触发指定触发器", "高级自动化工具…"),
            ("SystemTools.CrossPlatform.ActionFlowExecutionConfirmation", "行动流执行确认", "高级自动化工具…"),
            ("SystemTools.CrossPlatform.ClearAllNotifications", "清除全部提醒", "ClassIsland"),
            ("SystemTools.CrossPlatform.LoadTemporaryClassPlan", "加载临时课表", "ClassIsland"),
            ("SystemTools.CrossPlatform.OpenAppSettings", "打开应用设置", "ClassIsland"),
            ("SystemTools.CrossPlatform.OpenProfileEditor", "打开档案编辑", "ClassIsland"),
            ("SystemTools.CrossPlatform.OpenClassSwapWindow", "打开换课窗口", "ClassIsland"),
            ("SystemTools.CrossPlatform.ToggleWorkflow", "开关自动化", "高级自动化工具…"),
        };

        if (Settings.EnableAiService)
        {
            actions.Add(("SystemTools.CrossPlatform.ShowAiChatDialog", "显示AI对话框", "AI 功能…"));
        }

        if (Settings.EnableFloatingWindowFeature)
        {
            actions.Add(("SystemTools.CrossPlatform.ShowFloatingWindow", "显示悬浮窗", "悬浮窗设置"));
            actions.Add(("SystemTools.CrossPlatform.ToggleFloatingWindowLayer", "切换悬浮窗层级", "悬浮窗设置"));
            actions.Add(("SystemTools.CrossPlatform.ToggleFloatingWindowProfile", "切换悬浮窗配置方案", "悬浮窗设置"));
            actions.Add(("SystemTools.CrossPlatform.SwitchFloatingWindowTheme", "切换悬浮窗主题", "悬浮窗设置"));
        }

        foreach (var (id, name, group) in actions)
        {
            // 平台能力门：本机环境不支持的电源族行动不进入抽屉（与注册/行动菜单同源过滤），
            // 见 SystemPowerCapability（如 macOS 无“锁定屏幕”）。
            if (!SystemPowerCapability.IsActionSupported(id))
            {
                continue;
            }

            FeatureItems.Add(new UnifiedFeatureItem
            {
                Id = id,
                DisplayName = name,
                IsEnabled = Settings.IsActionEnabled(id),
                ItemType = FeatureItemType.Action,
                GroupName = group
            });
        }
        UpdateFeatureSearchResults(null);
    }

    public void UpdateFeatureSearchResults(string? searchText)
    {
        var keyword = searchText?.Trim();
        FeatureSearchResults.Clear();

        foreach (var item in FeatureItems.Where(item => MatchesFeatureSearch(item, keyword)))
        {
            FeatureSearchResults.Add(item);
        }

        OnPropertyChanged(nameof(IsFeatureSearchEmpty));
    }

    private static bool MatchesFeatureSearch(UnifiedFeatureItem item, string? keyword)
    {
        return string.IsNullOrEmpty(keyword) ||
               item.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               item.TypeDisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               item.GroupName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true ||
               item.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    public void SaveFeatureSettings()
    {
        foreach (var item in FeatureItems)
        {
            switch (item.ItemType)
            {
                case FeatureItemType.Action:
                    Settings.EnabledActions[item.Id] = item.IsEnabled;
                    break;
                case FeatureItemType.Trigger:
                    Settings.EnabledTriggers[item.Id] = item.IsEnabled;
                    break;
                case FeatureItemType.Component:
                    Settings.EnabledComponents[item.Id] = item.IsEnabled;
                    break;
                case FeatureItemType.Rule:
                    Settings.EnabledRules[item.Id] = item.IsEnabled;
                    break;
            }
        }

        _configHandler.Save();
    }
}