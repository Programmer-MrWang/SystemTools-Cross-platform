using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Controls.Ruleset;
using ClassIsland.Core.Models.Ruleset;
using ClassIsland.Shared;
using FluentAvalonia.UI.Controls;
using SystemTools.CrossPlatform.ConfigHandlers;
using SystemTools.CrossPlatform.Services;
using SystemTools.CrossPlatform.Shared;

namespace SystemTools.CrossPlatform.SettingsPage;

/// <summary>
/// 悬浮窗编辑页
/// 选择语义经 <see cref="SystemToolsSettingsViewModel.SelectFloatingWindowProfile"/> 与 A3 行动一致）。
/// </summary>
[HidePageTitle]
[SettingsPageInfo("SystemTools.CrossPlatform.settings.floating", "悬浮窗编辑", "\uEA37", "\uEA37")]
[Group("SystemTools.CrossPlatform.settings")]
public partial class FloatingWindowEditorSettingsPage : SettingsPageBase
{
    public MainConfigData Config => GlobalConstants.MainConfig!.Data;

    public FloatingWindowEditorSettingsPage()
    {
        ViewModel = new SystemToolsSettingsViewModel(GlobalConstants.MainConfig!,
            IAppHost.TryGetService<FloatingWindowProfileManager>(),
            IAppHost.GetService<FloatingWindowService>());
        DataContext = this;
        InitializeComponent();
        ViewModel.RefreshFloatingWindowProfiles();
        ViewModel.RefreshFloatingTriggers();
        ViewModel.Settings.PropertyChanged += OnSettingsPropertyChanged;
        RegisterHidingRulesEvents();
    }

    public SystemToolsSettingsViewModel ViewModel { get; }

    private bool _isDisposed;

    private enum RulesetTargetType { Button, Row, Window }
    private RulesetTargetType _currentRulesetTarget;
    private FloatingTriggerItem? _currentButtonTarget;
    private FloatingTriggerRow? _currentRowTarget;

    private ToggleSwitch? _drawerIsVisibleToggle;
    private ToggleSwitch? _drawerHideOnRuleToggle;
    private RulesetControl? _drawerRulesetControl;

    private Ruleset? _currentDrawerRuleset;
    private readonly List<INotifyPropertyChanged> _rulesetPropertyListeners = new();

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_isDisposed)
        {
            return;
        }

        ViewModel.Settings.PropertyChanged -= OnSettingsPropertyChanged;
        UnregisterHidingRulesEvents();
        DetachRulesetListeners();
        ViewModel.Dispose();
        _isDisposed = true;
    }

    private void RegisterHidingRulesEvents()
    {
        if (ViewModel.Settings.FloatingWindowRuleset is INotifyPropertyChanged hidingRules)
        {
            hidingRules.PropertyChanged += OnHidingRulesPropertyChanged;
        }
    }

    private void UnregisterHidingRulesEvents()
    {
        if (ViewModel.Settings.FloatingWindowRuleset is INotifyPropertyChanged hidingRules)
        {
            hidingRules.PropertyChanged -= OnHidingRulesPropertyChanged;
        }
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainConfigData.FloatingWindowTheme)
            or nameof(MainConfigData.FloatingWindowScale)
            or nameof(MainConfigData.FloatingWindowIconSize)
            or nameof(MainConfigData.FloatingWindowTextSize)
            or nameof(MainConfigData.FloatingWindowOpacity)
            or nameof(MainConfigData.FloatingWindowShadowEnabled)
            or nameof(MainConfigData.FloatingWindowDragHandleAlwaysVisible)
            or nameof(MainConfigData.FloatingWindowLayer)
            or nameof(MainConfigData.FloatingWindowLayerRecheckMode))
        {
            GlobalConstants.MainConfig?.Save();
            IAppHost.GetService<FloatingWindowService>().UpdateWindowState();
        }
        else if (e.PropertyName is nameof(MainConfigData.ShowFloatingWindow)
            or nameof(MainConfigData.FloatingWindowRulesetEnabled))
        {
            GlobalConstants.MainConfig?.Save();
            IAppHost.GetService<FloatingWindowService>().UpdateWindowState();
            IAppHost.TryGetService<IRulesetService>()?.NotifyStatusChanged();
        }
        else if (e.PropertyName == nameof(MainConfigData.FloatingWindowRuleset))
        {
            UnregisterHidingRulesEvents();
            RegisterHidingRulesEvents();
            GlobalConstants.MainConfig?.Save();
            IAppHost.TryGetService<IRulesetService>()?.NotifyStatusChanged();
        }
    }

    private void OnHidingRulesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsRulesetStateProperty(e.PropertyName))
        {
            return;
        }

        GlobalConstants.MainConfig?.Save();
        IAppHost.TryGetService<IRulesetService>()?.NotifyStatusChanged();
    }


    private void OnFloatingWindowVisibleToggleChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch toggle)
        {
            return;
        }

        var service = IAppHost.GetService<FloatingWindowService>();
        var config = ViewModel.Settings;

        var shouldShow = toggle.IsChecked == true && service.Entries.Count > 0;
        config.ShowFloatingWindow = shouldShow;

        if (toggle.IsChecked != shouldShow)
        {
            toggle.IsChecked = shouldShow;
        }

        GlobalConstants.MainConfig?.Save();
        service.UpdateWindowState();
    }

    private void ProfileSelector_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: string profileName })
        {
            ViewModel.SelectFloatingWindowProfile(profileName);
        }
    }

    private async void OnAddFloatingWindowProfileClick(object? sender, RoutedEventArgs e)
    {
        var textBox = new TextBox { Text = "" };
        var dialog = new FAContentDialog
        {
            Title = "新建悬浮窗配置方案",
            DefaultButton = FAContentDialogButton.Primary,
            PrimaryButtonText = "创建",
            SecondaryButtonText = "取消",
            Content = new ClassIsland.Core.Controls.Field
            {
                Content = textBox,
                Label = "配置方案名称",
                Suffix = ".json"
            }
        };
        var topLevel = TopLevel.GetTopLevel(this)
                       ?? throw new InvalidOperationException("无法访问设置窗口");
        var dialogResult = await dialog.ShowAsync(topLevel);

        if (dialogResult != FAContentDialogResult.Primary)
        {
            return;
        }

        var createProfileName = textBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(createProfileName))
        {
            return;
        }

        var path = Path.Combine(ViewModel.FloatingWindowProfilesDirectory,
            createProfileName + ".json");
        if (File.Exists(path))
        {
            return;
        }

        ViewModel.AddFloatingWindowProfile(createProfileName);
    }

    private void OnOpenFloatingWindowProfileFolderClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.FloatingWindowProfilesDirectory) ||
            !Directory.Exists(ViewModel.FloatingWindowProfilesDirectory))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = Path.GetFullPath(ViewModel.FloatingWindowProfilesDirectory),
            UseShellExecute = true
        });
    }

    private void OnRemoveCurrentProfileClick(object? sender, RoutedEventArgs e)
    {
        var currentName = ViewModel.CurrentFloatingWindowProfileName;
        if (string.IsNullOrWhiteSpace(currentName))
        {
            return;
        }

        ViewModel.RemoveFloatingWindowProfile(currentName);
    }

    private void OnAddFloatingTriggerRowClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.AddFloatingTriggerRow();
    }

    private void OnRemoveFloatingTriggerRowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: FloatingTriggerRow row })
        {
            return;
        }

        if (ViewModel.FloatingTriggerRows.Count <= 1)
        {
            return;
        }

        _ = ViewModel.RemoveFloatingTriggerRow(row);
    }

    private void OnInsertRowBelowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: FloatingTriggerRow row })
        {
            return;
        }

        var index = ViewModel.FloatingTriggerRows.IndexOf(row);
        if (index < 0)
        {
            return;
        }

        ViewModel.InsertFloatingTriggerRow(index + 1);
    }

    private void OnFloatingTriggerItemSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: FloatingTriggerItem item })
        {
            return;
        }

        _currentRulesetTarget = RulesetTargetType.Button;
        _currentButtonTarget = item;
        _currentRowTarget = null;

        OpenRulesetDrawer(item.Config.HidingRules, item.Config.IsVisible, item.Config.HideOnRule);
    }

    private void OnRowRulesetClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: FloatingTriggerRow row })
        {
            return;
        }

        _currentRulesetTarget = RulesetTargetType.Row;
        _currentButtonTarget = null;
        _currentRowTarget = row;

        OpenRulesetDrawer(row.RowRuleset.HidingRules, row.RowRuleset.IsVisible, row.RowRuleset.HideOnRule);
    }

    private void ButtonOpenFloatingWindowRuleset_OnClick(object? sender, RoutedEventArgs e)
    {
        _currentRulesetTarget = RulesetTargetType.Window;
        _currentButtonTarget = null;
        _currentRowTarget = null;

        var config = ViewModel.Settings;
        OpenRulesetDrawer(config.FloatingWindowRuleset, true, config.FloatingWindowRulesetEnabled);
    }

    private void OpenRulesetDrawer(Ruleset ruleset, bool isVisible, bool hideOnRule)
    {
        DetachRulesetListeners();

        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };

        if (_currentRulesetTarget == RulesetTargetType.Window)
        {
            var hint = new TextBlock
            {
                Text = "此规则集用于控制整窗悬浮窗的隐藏。窗口的“显示 / 隐藏”由设置页顶栏的总开关控制。",
                Foreground = TextFillColorSecondaryBrush(),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 8)
            };
            panel.Children.Add(hint);
        }

        var togglesPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 16,
            Margin = new Thickness(22, 0, 0, -15)
        };

        _drawerIsVisibleToggle = new ToggleSwitch
        {
            OnContent = "显示",
            OffContent = "隐藏",
            IsChecked = isVisible,
            IsVisible = _currentRulesetTarget != RulesetTargetType.Window
        };
        ToolTip.SetTip(_drawerIsVisibleToggle, "控制此项目是否显示");
        _drawerIsVisibleToggle.IsCheckedChanged += OnDrawerIsVisibleChanged;

        _drawerHideOnRuleToggle = new ToggleSwitch
        {
            OnContent = "按规则隐藏",
            OffContent = "禁用规则",
            IsChecked = hideOnRule
        };
        ToolTip.SetTip(_drawerHideOnRuleToggle, "启用后，满足规则集条件时自动隐藏");
        _drawerHideOnRuleToggle.IsCheckedChanged += OnDrawerHideOnRuleChanged;

        togglesPanel.Children.Add(_drawerIsVisibleToggle);
        togglesPanel.Children.Add(_drawerHideOnRuleToggle);
        panel.Children.Add(togglesPanel);

        _drawerRulesetControl = new RulesetControl { Classes = { "in-drawer" }, Ruleset = ruleset };
        panel.Children.Add(_drawerRulesetControl);

        AttachRulesetListeners(ruleset);

        this.Resources["RulesetDrawerContent"] = panel;
        OpenDrawer("RulesetDrawerContent");
    }

    private IBrush? TextFillColorSecondaryBrush()
    {
        if (Application.Current?.Resources.TryGetResource("TextFillColorSecondaryBrush", null, out var res) == true
            && res is IBrush brush)
        {
            return brush;
        }
        return null;
    }

    private void OnDrawerIsVisibleChanged(object? sender, RoutedEventArgs e)
    {
        var value = _drawerIsVisibleToggle?.IsChecked == true;

        switch (_currentRulesetTarget)
        {
            case RulesetTargetType.Button when _currentButtonTarget != null:
                _currentButtonTarget.Config.IsVisible = value;
                break;
            case RulesetTargetType.Row when _currentRowTarget != null:
                _currentRowTarget.RowRuleset.IsVisible = value;
                break;
        }

        SaveCurrentRulesetTarget();
        IAppHost.GetService<FloatingWindowService>().UpdateWindowState();
        NotifyRulesetStatusChanged();
    }

    private void OnDrawerHideOnRuleChanged(object? sender, RoutedEventArgs e)
    {
        var value = _drawerHideOnRuleToggle?.IsChecked == true;

        switch (_currentRulesetTarget)
        {
            case RulesetTargetType.Button when _currentButtonTarget != null:
                _currentButtonTarget.Config.HideOnRule = value;
                break;
            case RulesetTargetType.Row when _currentRowTarget != null:
                _currentRowTarget.RowRuleset.HideOnRule = value;
                break;
            case RulesetTargetType.Window:
                ViewModel.Settings.FloatingWindowRulesetEnabled = value;
                GlobalConstants.MainConfig?.Save();
                break;
        }

        SaveCurrentRulesetTarget();
        IAppHost.GetService<FloatingWindowService>().UpdateWindowState();
        NotifyRulesetStatusChanged();
    }

    private void NotifyRulesetStatusChanged()
    {
        IAppHost.TryGetService<IRulesetService>()?.NotifyStatusChanged();
    }

    private void SaveCurrentRulesetTarget()
    {
        if (_currentRulesetTarget == RulesetTargetType.Window)
        {
            GlobalConstants.MainConfig?.Save();
            return;
        }

        IAppHost.GetService<FloatingWindowService>().ProfileManager.SaveProfile();
    }

    private void AttachRulesetListeners(Ruleset ruleset)
    {
        DetachRulesetListeners();
        _currentDrawerRuleset = ruleset;

        AddRulesetPropertyListener(ruleset);
        ruleset.Groups.CollectionChanged += OnRulesetGroupsCollectionChanged;

        foreach (var group in ruleset.Groups)
        {
            AddRulesetPropertyListener(group);
            group.Rules.CollectionChanged += OnRulesetRulesCollectionChanged;
            foreach (var rule in group.Rules)
            {
                AddRulesetPropertyListener(rule);
            }
        }
    }

    private void DetachRulesetListeners()
    {
        foreach (var listener in _rulesetPropertyListeners)
        {
            listener.PropertyChanged -= OnRulesetPropertyChanged;
        }
        _rulesetPropertyListeners.Clear();

        if (_currentDrawerRuleset != null)
        {
            _currentDrawerRuleset.Groups.CollectionChanged -= OnRulesetGroupsCollectionChanged;
            foreach (var group in _currentDrawerRuleset.Groups)
            {
                group.Rules.CollectionChanged -= OnRulesetRulesCollectionChanged;
            }
            _currentDrawerRuleset = null;
        }
    }

    private void AddRulesetPropertyListener(INotifyPropertyChanged listener)
    {
        listener.PropertyChanged += OnRulesetPropertyChanged;
        _rulesetPropertyListeners.Add(listener);
    }

    private static bool IsRulesetStateProperty(string? propertyName)
    {
        return propertyName == nameof(Ruleset.State)
            || propertyName == nameof(RuleGroup.State)
            || propertyName == nameof(Rule.State);
    }

    private void OnRulesetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsRulesetStateProperty(e.PropertyName))
        {
            return;
        }

        SaveCurrentRulesetTarget();
        NotifyRulesetStatusChanged();
        IAppHost.TryGetService<FloatingWindowService>()?.UpdateWindowState();
    }

    private void OnRulesetGroupsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_currentDrawerRuleset == null)
        {
            return;
        }

        var ruleset = _currentDrawerRuleset;
        DetachRulesetListeners();
        AttachRulesetListeners(ruleset);

        SaveCurrentRulesetTarget();
        NotifyRulesetStatusChanged();
        IAppHost.TryGetService<FloatingWindowService>()?.UpdateWindowState();
    }

    private void OnRulesetRulesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_currentDrawerRuleset == null)
        {
            return;
        }

        var ruleset = _currentDrawerRuleset;
        DetachRulesetListeners();
        AttachRulesetListeners(ruleset);

        SaveCurrentRulesetTarget();
        NotifyRulesetStatusChanged();
        IAppHost.TryGetService<FloatingWindowService>()?.UpdateWindowState();
    }
}