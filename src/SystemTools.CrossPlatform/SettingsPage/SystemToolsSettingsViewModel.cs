using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Shared;
using SystemTools.CrossPlatform.ConfigHandlers;
using SystemTools.CrossPlatform.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SystemTools.CrossPlatform.SettingsPage;

/// <summary>
/// 主设置页 / 悬浮窗编辑页共享视图模型（阶段 1 A 档骨架，礼部 p1-06）。
/// 源插件同名文件（SettingsPage\SystemToolsSettingsViewModel.cs，含功能下载、语音识别模型、
/// 悬浮窗触发器拖拽等 B/C 档逻辑）不整体迁移；骨架仅保留 A 档消费面：
/// ① 配置根 <see cref="Settings"/>（MainConfigData A 档成员绑定）；
/// ② 悬浮窗配置方案选择面（FloatingWindowProfileManager 为 A3/A4 共享类型，p1-03 交付），
///    选择语义与 A3 行动一致：保存当前方案 → 加载目标方案 → 更新 CurrentFloatingWindowProfile → 落盘
///    （源 Actions\ToggleFloatingWindowProfileAction.cs:116-124 先例）。
/// 完整整合属阶段 3（05 阶段合同）。
/// </summary>

// ===== p3-02 增补开始（兵部 war / 悬浮窗编辑页 p3-02 页属类型；消费面归 p3-02，主页 p3-01
// AI/抽屉面不消费本段。内容：FloatingTriggerItem/FloatingTriggerRow 视图模型，源
// SettingsPage\SystemToolsSettingsViewModel.cs:54-83 非拖拽子集（D6 口径：行内拖拽排序/跨行
// 拖动状态不迁）。另一增补段（类内成员）见类内同款界标。） =====

/// <summary>
/// 悬浮窗触发器按钮项视图模型（源 SystemToolsSettingsViewModel.cs:54-75 随源非拖拽子集）。
/// </summary>
public partial class FloatingTriggerItem : ObservableObject
{
    [ObservableProperty] private string _buttonId = string.Empty;
    [ObservableProperty] private string _icon = string.Empty;
    [ObservableProperty] private string _buttonName = string.Empty;
    [ObservableProperty] private ButtonRulesetConfig _config = new();

    /// <summary>
    /// 图标表达式解析出的图标源，供 IconSourceElement 显示。兼容旧版 /uXXXX 配置（源随源）。
    /// </summary>
    public FluentAvalonia.UI.Controls.FAIconSource? IconSource =>
        Services.FloatingWindowIconProvider.TryResolveIconSource(Icon);

    partial void OnIconChanged(string value) { OnPropertyChanged(nameof(IconSource)); }
}

/// <summary>
/// 悬浮窗按钮行视图模型（源 SystemToolsSettingsViewModel.cs:77-83 随源；D6 口径下不携带任何
/// 拖拽状态，行排序仅由添加/插入/移除行语义承载）。
/// </summary>
public partial class FloatingTriggerRow : ObservableObject
{
    [ObservableProperty] private ObservableCollection<FloatingTriggerItem> _buttons = new();
    [ObservableProperty] private int _rowIndex = 0;
    [ObservableProperty] private RowRulesetConfig _rowRuleset = new();
}

// ===== p3-02 增补结束（页属类型段） =====

// ===== p3-01 增补开始（主页「启用功能选项」管理抽屉条目类型；源 SettingsPage\SystemToolsSettingsViewModel.cs:22-28/:36-52 随源；
// 抽屉清单枚举白名单=新插件注册面已迁功能项（行动/触发器/组件/规则），C 档条目由枚举构造结构性零呈现，
// p3-05 §2.1-W1/§3.4-3。消费面归主页 SystemToolsSettingsPage（p3-01 批），悬浮窗编辑页不消费本段。） =====

/// <summary>功能抽屉条目类型（源 :22-28 随源）。</summary>
public enum FeatureItemType
{
    Action,
    Trigger,
    Component,
    Rule
}

/// <summary>功能抽屉条目（源 UnifiedFeatureItem :36-52 随源）。</summary>
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

// ===== p3-01 增补结束（条目类型段） =====

public partial class SystemToolsSettingsViewModel : ObservableObject
{
    private readonly MainConfigHandler _configHandler;
    private readonly FloatingWindowProfileManager? _profileManager;

    public SystemToolsSettingsViewModel(MainConfigHandler configHandler, FloatingWindowProfileManager? profileManager)
    {
        _configHandler = configHandler;
        _profileManager = profileManager;
    }

    // ===== p3-02 增补开始（悬浮窗编辑页构造与订阅：注入 FloatingWindowService 消费触发器
    // 条目/窗口刷新面；主设置页两参构造签名与函数体保持 p1-06 原样，p3-01 主页 AI/抽屉消费面
    // 不受影响。页属成员增补段见类内另一界标。） =====

    private readonly FloatingWindowService? _floatingWindowService;
    private readonly EventHandler? _entriesChangedHandler;

    /// <summary>
    /// 悬浮窗编辑页构造（p3-02；注入 FloatingWindowService 以消费触发器条目/窗口刷新面）。
    /// </summary>
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

    // ===== p3-02 增补结束（构造与订阅段） =====

    /// <summary>插件聚合配置根（页内 A 档选项绑定入口）。</summary>
    public MainConfigData Settings => _configHandler.Data;

    /// <summary>悬浮窗配置方案名称列表（悬浮窗编辑页骨架消费）。</summary>
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

    /// <summary>
    /// 选择悬浮窗配置方案（持久化语义与 A3 行动 ToggleFloatingWindowProfileAction 一致）。
    /// </summary>
    public void SelectFloatingWindowProfile(string profileName)
    {
        if (_profileManager is null || string.IsNullOrWhiteSpace(profileName))
        {
            return;
        }

        // p3-02 修订（本批申报的悬浮窗方案面内）：当前方案文件存在性守卫，对齐 A3 行动
        // SwitchToProfile 源守卫（"只在当前方案文件还存在时才保存，避免刚被删除的方案被
        // 重新写回磁盘"，Actions\ToggleFloatingWindowProfileAction.cs:113-118 同款）；
        // 修订前骨架版此处误判目标方案文件存在性。
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

    // ===== p3-02 增补开始（悬浮窗编辑页属成员，W6-W7/W18 消费面：方案管理 + 按钮布局编辑器；
    // 源 SystemToolsSettingsViewModel.cs :119/:120/:401-574/:609-689/:782-860/:908-911 非拖拽
    // 子集。D6 口径：行内拖拽排序/跨行拖动/按钮池 MoveFloatingTrigger·AddTriggerFromPool 不迁。
    // 主页 p3-01 AI/抽屉面不消费本段。） =====

    /// <summary>当前悬浮窗配置方案对象（悬浮窗编辑页触发器行编辑消费；p3-02 页属面）。</summary>
    public FloatingWindowProfile? CurrentFloatingWindowProfile => _profileManager?.CurrentProfile;

    /// <summary>
    /// 悬浮窗方案 JSON 文件所在目录，供 UI 层打开文件夹/重名检测使用（源 :385-388 同名面）。
    /// </summary>
    public string FloatingWindowProfilesDirectory => _profileManager?.ProfilesDirectory ?? string.Empty;

    /// <summary>新建悬浮窗配置方案（源 AddFloatingWindowProfile :862-868 同语义；p3-02 页属面）。</summary>
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

    /// <summary>删除悬浮窗配置方案；删除当前方案时切回 Default（源 RemoveFloatingWindowProfile :870-886 同语义）。</summary>
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

    // ===== 悬浮窗按钮布局编辑器支撑（源 SystemToolsSettingsViewModel.cs
    // :401-574/:609-689/:782-860 非拖拽子集；D6 口径：行内拖拽排序/跨行拖动/按钮池
    // MoveFloatingTrigger·AddTriggerFromPool 不迁） =====

    /// <summary>悬浮窗按钮行集合（布局编辑器 ItemsSource）。</summary>
    public ObservableCollection<FloatingTriggerRow> FloatingTriggerRows { get; } = [];

    /// <summary>是否存在已注册的悬浮窗触发器条目（「显示悬浮窗」开关可用性随源）。</summary>
    public bool HasFloatingTriggerEntries { get; private set; }

    /// <summary>
    /// 以当前方案与悬浮窗服务条目重建按钮行（源 RefreshFloatingTriggers :401-574 随源，D6 子集）。
    /// 服务未注入（主页两参构造）时为空操作。
    /// </summary>
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

        // 清理不存在的按钮ID
        if (profile.PruneInvalidButtonIds(entries.Keys))
        {
            _profileManager.SaveProfile();
        }

        // 收集已配置的按钮ID
        var configuredIds = new HashSet<string>();
        foreach (var row in profile.FloatingWindowButtonRows ?? [])
        {
            foreach (var id in row)
            {
                configuredIds.Add(id);
            }
        }

        // 如果没有任何按钮被配置到行中，自动将所有可用按钮添加到第一行
        // 这样用户首次使用或从旧版本迁移时，按钮默认会显示出来
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

        // 新注册且尚未配置的按钮自动追加到第一行
        // 已存在按钮配置（如被用户移除/隐藏）的按钮不再自动添加
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

        // 注销旧对象上的事件处理程序，避免重复注册和内存泄漏
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

        // 构建已配置的行显示
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

        // 如果有新创建的默认配置，确保保存
        if (needSave)
        {
            _profileManager.SaveProfile();
        }
    }

    private void OnButtonConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 规则集求值时会写入 State（Ruleset/RuleGroup/Rule），避免因此递归触发通知
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
        // 规则集求值时会写入 State（Ruleset/RuleGroup/Rule），避免因此递归触发通知
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

    /// <summary>添加按钮行（源 AddFloatingTriggerRow :609-627 随源）。</summary>
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

    /// <summary>在指定位置插入按钮行（源 InsertFloatingTriggerRow :629-655 随源）。</summary>
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

        // 重新计算后续行的索引
        for (int i = insertIndex; i < FloatingTriggerRows.Count; i++)
        {
            FloatingTriggerRows[i].RowIndex = i + 1;
        }

        PersistFloatingTriggerRows();
    }

    /// <summary>移除按钮行；仅剩一行时保留（源 RemoveFloatingTriggerRow :657-689 随源）。</summary>
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

        // 注销被移除行的事件处理程序
        row.RowRuleset.PropertyChanged -= OnRowRulesetPropertyChanged;
        if (row.RowRuleset.HidingRules is INotifyPropertyChanged rowHidingRules)
        {
            rowHidingRules.PropertyChanged -= OnRowRulesetPropertyChanged;
        }

        var targetRow = index > 0 ? FloatingTriggerRows[index - 1] : FloatingTriggerRows[index + 1];
        foreach (var item in row.Buttons)
        {
            // 按钮的 Config 事件监听保持不变（对象引用不变，事件仍有效）
            targetRow.Buttons.Add(item);
        }

        FloatingTriggerRows.RemoveAt(index);

        // 重新计算行索引
        for (int i = 0; i < FloatingTriggerRows.Count; i++)
        {
            FloatingTriggerRows[i].RowIndex = i + 1;
        }

        PersistFloatingTriggerRows();
        return true;
    }

    /// <summary>
    /// 将按钮行顺序落盘到当前方案并刷新窗口（源 PersistFloatingTriggerRows :782-860 随源，
    /// D6 口径下由添加/插入/移除行调用）。
    /// </summary>
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

        // 同步行规则集：确保 FloatingWindowRowRulesets 与行数一致
        var rowRulesets = profile.FloatingWindowRowRulesets;
        while (rowRulesets.Count < FloatingTriggerRows.Count)
        {
            rowRulesets.Add(new RowRulesetConfig());
        }
        while (rowRulesets.Count > FloatingTriggerRows.Count)
        {
            // 注销被移除行规则集的事件
            var removedRowRuleset = rowRulesets[rowRulesets.Count - 1];
            removedRowRuleset.PropertyChanged -= OnRowRulesetPropertyChanged;
            if (removedRowRuleset.HidingRules is INotifyPropertyChanged removedHidingRules)
            {
                removedHidingRules.PropertyChanged -= OnRowRulesetPropertyChanged;
            }
            rowRulesets.RemoveAt(rowRulesets.Count - 1);
        }
        // 同步每行的 RowRuleset 引用（确保ViewModel中的修改反映到profile）
        for (int i = 0; i < FloatingTriggerRows.Count; i++)
        {
            var vmRow = FloatingTriggerRows[i];
            if (!ReferenceEquals(vmRow.RowRuleset, rowRulesets[i]))
            {
                // RowRuleset 引用变更时，重新注册事件
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

        // 清理不再使用的按钮规则集配置
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

    /// <summary>悬浮窗编辑页分离时注销服务事件订阅（源 Dispose :908-911 同语义；主页两参构造为空操作）。</summary>
    public void Dispose()
    {
        if (_floatingWindowService != null && _entriesChangedHandler != null)
        {
            _floatingWindowService.EntriesChanged -= _entriesChangedHandler;
        }
    }

    // ===== p3-02 增补结束（页属成员段；本文件 p3-02 增补共 3 段：页属类型 / 构造与订阅 / 页属成员，
    // 另含 SelectFloatingWindowProfile 守卫修订与文件头 using 增补，均在 p3-02 批证据登记） =====

    // ===== p3-01 增补开始（主页功能管理抽屉消费面；源 SettingsPage\SystemToolsSettingsViewModel.cs
    // :106-113/:174-381 随源适配；悬浮窗方案面/触发器行面（上方 p3-02 三段与既有方案成员）零触碰。
    // p3-08 刑部交叉核对口径：本段成员 = FeatureItems / FeatureSearchResults / IsFeatureSearchEmpty /
    // IsFeatureDrawerOpen / FeatureDrawerContent / InitializeFeatureItems / UpdateFeatureSearchResults /
    // MatchesFeatureSearch / SaveFeatureSettings（另类型段 FeatureItemType / UnifiedFeatureItem），
    // 消费页仅 SystemToolsSettingsPage（p3-01）。依赖下载/进度/MD5 面零引入（C13）。） =====

    [ObservableProperty] private ObservableCollection<UnifiedFeatureItem> _featureItems = new();
    [ObservableProperty] private ObservableCollection<UnifiedFeatureItem> _featureSearchResults = new();

    public bool IsFeatureSearchEmpty => FeatureSearchResults.Count == 0;

    // 抽屉开关与内容承载（源 :111-113 Drawer 字段随源）
    [ObservableProperty] private bool _isFeatureDrawerOpen = false;
    [ObservableProperty] private object? _featureDrawerContent;

    /// <summary>
    /// 初始化功能抽屉清单（源 InitializeFeatureItems :174-335 结构随源，枚举白名单结构性裁剪）：
    /// 仅枚举新插件注册面已迁功能项共 39 项（行动 27 + 触发器 2 + 组件 6 + 规则 4，ID 前缀
    /// SystemTools.CrossPlatform.*，与 Plugin.cs RegisterBaseActions/Triggers/Rules/Components 及
    /// [ActionInfo]/[TriggerInfo]/[ComponentInfo] 注册面一一对应；注册面 52 项口径中服务 7 / 主题 3 /
    /// 设置页 6 不属抽屉可枚举面，p3-05 §2.1-W1/§3.4-3：注册面即白名单）。名称/组别逐字随源抽屉
    /// 清单（源 :178-333）；源清单 C 档条目（模拟操作/显示设置/USB/热键/媒体 C 族/实验性/AI 语音/
    /// RestartAsAdmin 等）零呈现；门随源 :210-213/:302-307（EnableFloatingWindowFeature/
    /// EnableAiService，对应 Plugin.cs 注册组门 :301/:326/:362）；源 :317-321
    /// EnableExperimentalFeatures 门随 C1 裁剪整块移除。
    /// </summary>
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
            ("SystemTools.CrossPlatform.ShowToast", "拉起自定义Windows通知", "实用工具"),
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

    // ===== p3-01 增补结束（主页功能管理抽屉消费面段） =====
}