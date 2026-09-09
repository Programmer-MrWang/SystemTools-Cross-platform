using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Controls;
using ClassIsland.Platforms.Abstraction;
using ClassIsland.Platforms.Abstraction.Enums;
using ClassIsland.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using SystemTools.CrossPlatform.ConfigHandlers;
using SystemTools.CrossPlatform.Triggers;

namespace SystemTools.CrossPlatform.Services;

public class FloatingWindowService
{
    private const int FollowClassIslandTheme = 0;
    private const int LightTheme = 1;
    private const int DarkTheme = 2;
    private static readonly TimeSpan TouchLikeMouseGracePeriod = TimeSpan.FromMilliseconds(250);

    private readonly MainConfigHandler _configHandler;
    private readonly FloatingWindowProfileManager _profileManager;
    private readonly Dictionary<FloatingWindowTrigger, FloatingWindowEntry> _entries = new();
    private Window? _window;
    private Grid? _windowRoot;
    private StackPanel? _stackPanel;
    private Border? _windowContainer;
    private bool _windowBoundsClampQueued;
    private bool _pointerPressed;
    private bool _dragInitiated;
    private Point _pointerDownPoint;
    private PointerPressedEventArgs? _lastPressedArgs;
    private bool _isThemeSubscribed;
    private readonly Dictionary<string, double> _buttonWidthCache = new();
    private double _lastButtonLayoutScale = double.NaN;
    private bool _allowWindowClose;
    private bool _restoringFromMinimized;
    private bool _isStarted;
    private bool _isStopped;
    private bool _isTouchDeviceDetected;
    private bool _touchDragAllowed;
    private PixelPoint _touchDragStartScreenPoint;
    private PixelPoint _touchDragStartWindowPosition;
    private Border? _touchDragHandle;
    private DateTime _lastTouchGeneratedMouseEventAt = DateTime.MinValue;
    private ILessonsService? _lessonsService;

    private bool _rulesetHidingWindow = false;
    private readonly HashSet<string> _rulesetHiddenButtons = new();
    private readonly HashSet<int> _rulesetHiddenRows = new();

    public event EventHandler? EntriesChanged;

    public FloatingWindowService(
        MainConfigHandler configHandler,
        FloatingWindowProfileManager profileManager)
    {
        _configHandler = configHandler;
        _profileManager = profileManager;
    }

    public IReadOnlyList<FloatingWindowEntry> Entries => _entries.Values.ToList();

    public FloatingWindowProfileManager ProfileManager => _profileManager;

    public void Start()
    {
        if (_isStarted)
        {
            return;
        }

        _isStarted = true;
        _isStopped = false;
        Dispatcher.UIThread.Post(() =>
        {
            if (_isStopped)
            {
                return;
            }

            _profileManager.LoadProfile(_configHandler.Data.CurrentFloatingWindowProfile);
            EnsureWindow();
            EnsureRulesetPatrol();
            SubscribeThemeChanged();
            _configHandler.Data.PropertyChanged += OnConfigPropertyChanged;
            ApplyVisibility();
            RecheckWindowLayer();
            RefreshWindowButtons();
        });
    }

    public void Stop()
    {
        if (!_isStarted)
        {
            return;
        }

        _isStarted = false;
        _isStopped = true;
        Dispatcher.UIThread.Post(() =>
        {
            if (_window != null)
            {
                _allowWindowClose = true;
                _window.Close();
            }

            DiscardWindowState();

            RemoveRulesetPatrol();
            UnsubscribeThemeChanged();
            _configHandler.Data.PropertyChanged -= OnConfigPropertyChanged;
        });
    }

    public void RegisterTrigger(FloatingWindowTrigger trigger)
    {
        var isExistingTrigger = _entries.ContainsKey(trigger);
        _entries[trigger] = CreateEntry(trigger);

        PruneButtonWidthCache();
        NotifyEntriesChanged();
    }

    public void EnsureUniqueButtonIds()
    {
        var usedButtonIds = new HashSet<string>();
        var changed = false;

        foreach (var trigger in _entries.Keys.ToList())
        {
            var oldButtonId = trigger.GetButtonId();
            var buttonId = trigger.GetUniqueButtonId(usedButtonIds.Contains);
            usedButtonIds.Add(buttonId);
            _entries[trigger] = CreateEntry(trigger);

            if (!string.Equals(oldButtonId, buttonId, StringComparison.Ordinal))
            {
                changed = true;
            }
        }

        if (changed)
        {
            PruneButtonWidthCache();
        }
    }

    private FloatingWindowEntry CreateEntry(FloatingWindowTrigger trigger)
    {
        var buttonId = trigger.GetUniqueButtonId(id => _entries.Any(x =>
            !ReferenceEquals(x.Key, trigger) && string.Equals(x.Value.ButtonId, id, StringComparison.Ordinal)));

        return new FloatingWindowEntry(
            buttonId,
            trigger.GetIcon(),
            trigger.GetButtonName(),
            trigger.ShouldUseRevertStyle(),
            trigger.IsRevertEnabled(),
            trigger.GetLayoutButtonName(),
            trigger.TriggerFromFloatingWindow,
            trigger.CancelIsOnState);
    }

    public void UnregisterTrigger(FloatingWindowTrigger trigger)
    {
        if (_entries.Remove(trigger))
        {
            PruneButtonWidthCache();
            NotifyEntriesChanged();
        }
    }

    public void UpdateWindowState()
    {
        if (_isStopped) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (_isStopped) return;
            ApplyVisibility();
            RecheckWindowLayer();
            RefreshWindowButtons();
        });
    }

    private void NotifyEntriesChanged()
    {
        EntriesChanged?.Invoke(this, EventArgs.Empty);
        if (_isStopped) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (_isStopped) return;
            ApplyVisibility();
            RecheckWindowLayer();
            RefreshWindowButtons();
        });
    }

    private void SubscribeThemeChanged()
    {
        if (_isThemeSubscribed || Application.Current == null)
        {
            return;
        }

        Application.Current.PropertyChanged += OnApplicationPropertyChanged;
        _isThemeSubscribed = true;
    }

    private void UnsubscribeThemeChanged()
    {
        if (!_isThemeSubscribed || Application.Current == null)
        {
            return;
        }

        Application.Current.PropertyChanged -= OnApplicationPropertyChanged;
        _isThemeSubscribed = false;
    }

    private void OnApplicationPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (string.Equals(e.Property?.Name, "ActualThemeVariant", StringComparison.Ordinal)
            && _configHandler.Data.FloatingWindowTheme == FollowClassIslandTheme)
        {
            Dispatcher.UIThread.Post(() =>
            {
                RefreshWindowButtons();
                ApplyWindowAppearance();
            });
        }
    }

    private ThemeVariant ResolveWindowThemeVariant()
    {
        return _configHandler.Data.FloatingWindowTheme switch
        {
            LightTheme => ThemeVariant.Light,
            DarkTheme => ThemeVariant.Dark,
            _ => _window?.ActualThemeVariant ?? Application.Current?.ActualThemeVariant ?? ThemeVariant.Dark
        };
    }

    private bool IsLightTheme()
    {
        return ResolveWindowThemeVariant() == ThemeVariant.Light;
    }

    public void SetWindowTheme(int theme)
    {
        var normalized = theme is LightTheme or DarkTheme
            ? theme
            : FollowClassIslandTheme;
        if (_configHandler.Data.FloatingWindowTheme == normalized)
        {
            return;
        }

        _configHandler.Data.FloatingWindowTheme = normalized;
        _configHandler.Save();
        Dispatcher.UIThread.Post(RefreshWindowButtons);
    }

    public void ToggleWindowTheme()
    {
        var next = (_configHandler.Data.FloatingWindowTheme + 1) % 3;
        SetWindowTheme(next);
    }

    private void EnsureWindow()
    {
        if (_window != null || _isStopped)
        {
            return;
        }

        _allowWindowClose = false;
        _stackPanel = new StackPanel { Margin = new Thickness(6), Spacing = 6 };
        _windowContainer = new Border
        {
            Background = TryParseColor("#CC1F1F1F") ??
                         new SolidColorBrush(Color.FromArgb(0xCC, 0x1F, 0x1F, 0x1F)),
            CornerRadius = new CornerRadius(8),
            Child = _stackPanel
        };
        _windowRoot = new Grid
        {
            Children =
            {
                _windowContainer
            }
        };
        _window = new Window
        {
            Width = 64,
            Height = 64,
            ShowActivated = false,
            Topmost = _configHandler.Data.FloatingWindowLayer == 1,
            WindowDecorations = WindowDecorations.None,
            Background = Brushes.Transparent,
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
            CanResize = false,
            ShowInTaskbar = false,
            SizeToContent = SizeToContent.WidthAndHeight,
            Content = _windowRoot
        };

        _window.Loaded += OnWindowLoaded;
        _window.Opened += OnWindowOpened;
        _window.SizeChanged += OnWindowSizeChanged;
        _window.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, true);
        _window.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel, true);
        _window.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel, true);
        _window.Closing += (_, e) =>
        {
            if (!_allowWindowClose)
            {
                e.Cancel = true;
            }
        };
        _window.PropertyChanged += OnWindowPropertyChanged;

        _window.Show();
    }

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, _window))
        {
            return;
        }

        QueueWindowBoundsClamp();
    }

    private void QueueWindowBoundsClamp()
    {
        if (_window == null || _windowBoundsClampQueued)
        {
            return;
        }

        var targetWindow = _window;
        _windowBoundsClampQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _windowBoundsClampQueued = false;
            if (!ReferenceEquals(_window, targetWindow))
            {
                return;
            }

            var clamped = ClampToVisibleScreen(targetWindow.Position);
            if (clamped != targetWindow.Position)
            {
                targetWindow.Position = clamped;
                SavePosition(clamped);
            }
        }, DispatcherPriority.Background);
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_window == null || _restoringFromMinimized)
        {
            return;
        }

        if (e.Property == Window.WindowStateProperty && _window.WindowState == WindowState.Minimized)
        {
            RestoreWindowFromMinimized();
        }
    }

    private void RestoreWindowFromMinimized()
    {
        if (_window == null || _restoringFromMinimized || _isStopped)
        {
            return;
        }

        _restoringFromMinimized = true;

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (_window == null || _isStopped)
                {
                    return;
                }

                if (!_window.IsVisible)
                {
                    try { _window.Show(); }
                    catch (InvalidOperationException)
                    {
                        DiscardWindowState();
                    }
                }

                if (_window != null)
                {
                    _window.WindowState = WindowState.Normal;
                }
            }
            finally
            {
                _restoringFromMinimized = false;
            }
        }, DispatcherPriority.Background);
    }

    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        if (_window == null || !ReferenceEquals(sender, _window))
        {
            return;
        }

        EnsureWindowPositionVisibleOnStartup();
        RecheckWindowLayer();
        ApplyWindowAppearance();
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, _window) || _isStopped)
        {
            return;
        }

        ApplyWindowAppearance();
    }

    private void OnConfigPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainConfigData.FloatingWindowOpacity)
            or nameof(MainConfigData.FloatingWindowTheme)
            or nameof(MainConfigData.FloatingWindowScale)
            or nameof(MainConfigData.FloatingWindowIconSize)
            or nameof(MainConfigData.FloatingWindowTextSize)
            or nameof(MainConfigData.FloatingWindowShadowEnabled)
            or nameof(MainConfigData.FloatingWindowDragHandleAlwaysVisible))
        {
            Dispatcher.UIThread.Post(() =>
            {
                RefreshWindowButtons();
                ApplyWindowAppearance();
            });
        }
    }

    private void ApplyWindowAppearance()
    {
        if (_windowContainer == null)
        {
            return;
        }

        var config = _configHandler.Data;
        var scale = Math.Clamp(config.FloatingWindowScale, 0.5, 2.0);
        var opacity = Math.Clamp(config.FloatingWindowOpacity, 10, 100) / 100.0;

        _windowContainer.CornerRadius = new CornerRadius(8);
        _windowContainer.Background = CreateFallbackWindowBrush(IsLightTheme(), opacity);
        _windowContainer.BoxShadow = config.FloatingWindowShadowEnabled
            ? CreateFallbackShadow(IsLightTheme(), scale)
            : default;
    }

    private static IBrush CreateFallbackWindowBrush(bool isLightTheme, double opacity)
    {
        var alpha = (byte)Math.Round(255 * opacity);
        return new SolidColorBrush(isLightTheme
            ? Color.FromArgb(alpha, 0xFF, 0xFF, 0xFF)
            : Color.FromArgb(alpha, 0x1F, 0x1F, 0x1F));
    }

    private static BoxShadows CreateFallbackShadow(bool isLightTheme, double scale)
    {
        return new BoxShadows(new BoxShadow
        {
            OffsetX = 0,
            OffsetY = 6 * scale,
            Blur = 18 * scale,
            Spread = 0,
            Color = isLightTheme ? Color.Parse("#28000000") : Color.Parse("#60000000")
        });
    }

    private void CheckFloatingWindowRuleset()
    {
        var profile = _profileManager.CurrentProfile;
        if (!_configHandler.Data.FloatingWindowRulesetEnabled)
        {
            if (_rulesetHidingWindow)
            {
                _rulesetHidingWindow = false;
                ApplyVisibility();
            }
            return;
        }

        var rulesetService = IAppHost.TryGetService<IRulesetService>();
        if (rulesetService == null)
        {
            return;
        }

        var isSatisfied = rulesetService.IsRulesetSatisfied(_configHandler.Data.FloatingWindowRuleset);
        var shouldHide = isSatisfied;

        if (shouldHide != _rulesetHidingWindow)
        {
            _rulesetHidingWindow = shouldHide;
            ApplyVisibility();
        }
    }

    private void CheckButtonRulesets()
    {
        var profile = _profileManager.CurrentProfile;
        var rulesetService = IAppHost.TryGetService<IRulesetService>();
        if (rulesetService == null)
        {
            return;
        }

        var changed = false;
        foreach (var entry in _entries.Values)
        {
            if (!profile.FloatingWindowButtonRulesets.TryGetValue(entry.ButtonId, out var config))
            {
                continue;
            }

            var shouldHide = false;
            if (!config.IsVisible)
            {
                shouldHide = true;
            }
            else if (config.HideOnRule)
            {
                shouldHide = rulesetService.IsRulesetSatisfied(config.HidingRules);
            }

            var wasHidden = _rulesetHiddenButtons.Contains(entry.ButtonId);
            if (shouldHide != wasHidden)
            {
                if (shouldHide)
                {
                    _rulesetHiddenButtons.Add(entry.ButtonId);
                }
                else
                {
                    _rulesetHiddenButtons.Remove(entry.ButtonId);
                }
                changed = true;
            }
        }

        if (changed)
        {
            Dispatcher.UIThread.Post(RefreshWindowButtons);
        }
    }

    private void CheckRowRulesets()
    {
        var profile = _profileManager.CurrentProfile;
        var rowConfigs = profile.FloatingWindowRowRulesets;
        if (rowConfigs == null || rowConfigs.Count == 0)
        {
            if (_rulesetHiddenRows.Count > 0)
            {
                _rulesetHiddenRows.Clear();
                Dispatcher.UIThread.Post(RefreshWindowButtons);
            }
            return;
        }

        var rulesetService = IAppHost.TryGetService<IRulesetService>();
        if (rulesetService == null)
        {
            return;
        }

        var changed = false;
        for (int i = 0; i < rowConfigs.Count; i++)
        {
            var config = rowConfigs[i];
            var shouldHide = false;
            if (!config.IsVisible)
            {
                shouldHide = true;
            }
            else if (config.HideOnRule)
            {
                shouldHide = rulesetService.IsRulesetSatisfied(config.HidingRules);
            }

            var wasHidden = _rulesetHiddenRows.Contains(i);
            if (shouldHide != wasHidden)
            {
                if (shouldHide)
                {
                    _rulesetHiddenRows.Add(i);
                }
                else
                {
                    _rulesetHiddenRows.Remove(i);
                }
                changed = true;
            }
        }

        if (changed)
        {
            Dispatcher.UIThread.Post(RefreshWindowButtons);
        }
    }

    private void ApplyVisibility()
    {
        if (_isStopped) return;
        EnsureWindow();
        if (_window == null)
        {
            return;
        }

        var hasVisibleButtons = HasAnyVisibleButton();
        var shouldShow = _configHandler.Data.ShowFloatingWindow && hasVisibleButtons && !_rulesetHidingWindow;

        if (shouldShow)
        {
            if (!_window.IsVisible)
            {
                try
                {
                    _window.Show();
                }
                catch (InvalidOperationException)
                {
                    DiscardWindowState();
                    if (_isStopped) return;
                    EnsureWindow();
                    if (_window != null)
                    {
                        try { _window.Show(); }
                        catch (InvalidOperationException) { /* 放弃重建 */ }
                    }
                }
            }
        }
        else
        {
            if (_window != null && _window.IsVisible)
            {
                try
                {
                    _window.Hide();
                }
                catch (InvalidOperationException)
                {
                    DiscardWindowState();
                }
            }
        }
    }

    private void DiscardWindowState()
    {
        _window = null;
        _windowRoot = null;
        _stackPanel = null;
        _windowContainer = null;
        _touchDragHandle = null;
        _windowBoundsClampQueued = false;
        _pointerPressed = false;
        _dragInitiated = false;
        _lastPressedArgs = null;
        _touchDragAllowed = false;
    }

    private void RefreshWindowButtons()
    {
        if (_stackPanel == null)
        {
            return;
        }

        var profile = _profileManager.CurrentProfile;
        var config = _configHandler.Data;
        var scale = Math.Clamp(config.FloatingWindowScale, 0.5, 2.0);
        var iconSize = Math.Clamp(config.FloatingWindowIconSize, 15, 50) * scale;
        var textSize = Math.Clamp(config.FloatingWindowTextSize, 8, 30) * scale;
        var isLightTheme = IsLightTheme();
        var contentForeground = isLightTheme ? Brushes.Black : Brushes.White;

        if (double.IsNaN(_lastButtonLayoutScale) ||
            Math.Abs(_lastButtonLayoutScale - scale) > 0.0001)
        {
            _buttonWidthCache.Clear();
            _lastButtonLayoutScale = scale;
        }

        _stackPanel.Orientation = Orientation.Vertical;
        _stackPanel.Spacing = 6 * scale;
        _stackPanel.Margin = new Thickness(6 * scale);
        _stackPanel.HorizontalAlignment = HorizontalAlignment.Center;

        _stackPanel.Children.Clear();
        _touchDragHandle = null;

        int rowIndex = 0;
        foreach (var rowEntries in GetOrderedRows())
        {
            if (_rulesetHiddenRows.Contains(rowIndex))
            {
                rowIndex++;
                continue;
            }

            var rowPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6 * scale,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            foreach (var entry in rowEntries)
            {
                var iconBlock = FloatingWindowIconProvider.CreateIconControl(
                    entry.Icon, iconSize, contentForeground);

                var nameBlock = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(entry.Name) ? "触发" : entry.Name,
                    FontSize = textSize,
                    FontWeight = FontWeight.Normal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    TextTrimming = TextTrimming.None,
                    MaxWidth = 100 * scale,
                    Margin = new Thickness(0, 2 * scale, 0, 0),
                    Foreground = contentForeground
                };

                var contentPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Spacing = 2 * scale,
                    Children =
                    {
                        iconBlock,
                        nameBlock
                    }
                };

                var button = new Button
                {
                    Content = contentPanel,
                    MinWidth = 54 * scale,
                    MinHeight = 52 * scale,
                    Padding = new Thickness(6 * scale, 4 * scale),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = contentForeground,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center
                };

                if (entry.IsRevertStyleActive)
                {
                    button.Background = TryGetButtonPointerOverBrush() ??
                                        new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));

                    if (_buttonWidthCache.TryGetValue(entry.ButtonId, out var cachedWidth) && cachedWidth > 0)
                    {
                        button.Width = cachedWidth;
                    }
                }
                else
                {
                    button.Width = double.NaN;
                }

                if (!entry.IsRevertStyleActive)
                {
                    EventHandler? layoutUpdatedHandler = null;
                    layoutUpdatedHandler = (_, _) =>
                    {
                        var width = button.Bounds.Width;
                        if (width > 0)
                        {
                            _buttonWidthCache[entry.ButtonId] = width;
                            button.LayoutUpdated -= layoutUpdatedHandler;
                        }
                    };
                    button.LayoutUpdated += layoutUpdatedHandler;
                }

                button.PointerPressed += (_, e) =>
                {
                    if (!entry.IsRevertStyleActive || !entry.IsRevertEnabled)
                    {
                        return;
                    }

                    if (e.GetCurrentPoint(button).Properties.IsRightButtonPressed)
                    {
                        entry.CancelIsOnAction();
                        e.Handled = true;
                    }
                };

                button.Click += (_, _) => entry.TriggerAction();
                rowPanel.Children.Add(button);
            }

            _stackPanel.Children.Add(rowPanel);

            rowIndex++;
        }

        var hasVisibleButtons = _stackPanel.Children.Count > 0;
        if (hasVisibleButtons)
        {
            _touchDragHandle = CreateTouchDragHandle(scale, contentForeground);
            UpdateDragHandleVisibility();
            _stackPanel.Children.Insert(0, _touchDragHandle);
        }
    }

    private bool HasAnyVisibleButton()
    {
        if (_entries.Count == 0)
        {
            return false;
        }

        var profile = _profileManager.CurrentProfile;
        var rowConfigs = profile.FloatingWindowRowRulesets;
        var hiddenRowSet = new HashSet<int>();

        if (rowConfigs != null)
        {
            for (int i = 0; i < rowConfigs.Count; i++)
            {
                var cfg = rowConfigs[i];
                var shouldHide = !cfg.IsVisible
                    || (cfg.HideOnRule && cfg.HidingRules != null
                        && IAppHost.TryGetService<IRulesetService>() is { } rs
                        && rs.IsRulesetSatisfied(cfg.HidingRules));
                if (shouldHide)
                {
                    hiddenRowSet.Add(i);
                }
            }
        }

        int rowIndex = 0;
        foreach (var row in GetConfiguredButtonRowsWithFallback(profile))
        {
            if (!hiddenRowSet.Contains(rowIndex))
            {
                foreach (var id in row)
                {
                    if (_rulesetHiddenButtons.Contains(id))
                    {
                        continue;
                    }
                    foreach (var entry in _entries.Values)
                    {
                        if (string.Equals(entry.ButtonId, id, StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                }
            }
            rowIndex++;
        }

        return false;
    }

    private List<List<FloatingWindowEntry>> GetOrderedRows()
    {
        var profile = _profileManager.CurrentProfile;
        var validButtonIds = _entries.Values.Select(x => x.ButtonId).ToHashSet();

        if (profile.PruneInvalidButtonIds(validButtonIds))
        {
            _profileManager.SaveProfile();
        }

        var values = _entries.Values
            .Where(x => !_rulesetHiddenButtons.Contains(x.ButtonId))
            .GroupBy(x => x.ButtonId)
            .ToDictionary(g => g.Key, g => g.First());

        var rows = new List<List<FloatingWindowEntry>>();

        foreach (var row in GetConfiguredButtonRowsWithFallback(profile))
        {
            var items = new List<FloatingWindowEntry>();
            foreach (var id in row)
            {
                if (values.TryGetValue(id, out var entry))
                {
                    items.Add(entry);
                }
            }
            if (items.Count > 0)
            {
                rows.Add(items);
            }
        }

        return rows;
    }


    private List<List<string>> GetConfiguredButtonRowsWithFallback(FloatingWindowProfile profile)
    {
        var validButtonIds = _entries.Values.Select(x => x.ButtonId).Distinct().ToList();
        var validSet = validButtonIds.ToHashSet();
        var rows = (profile.FloatingWindowButtonRows ?? [])
            .Select(row => row.Where(validSet.Contains).Distinct().ToList())
            .Where(row => row.Count > 0)
            .ToList();

        var configuredIds = rows.SelectMany(row => row).ToHashSet();
        var missingIds = validButtonIds
            .Where(id => !configuredIds.Contains(id))
            .Where(id => !profile.FloatingWindowButtonRulesets.ContainsKey(id))
            .ToList();

        if (missingIds.Count == 0)
        {
            return rows;
        }

        if (rows.Count == 0)
        {
            rows.Add(missingIds);
        }
        else
        {
            rows[0].AddRange(missingIds);
        }

        return rows;
    }

    private void PruneButtonWidthCache()
    {
        if (_buttonWidthCache.Count == 0)
        {
            return;
        }

        var validIds = _entries.Values.Select(x => x.ButtonId).ToHashSet();
        var staleIds = _buttonWidthCache.Keys.Where(id => !validIds.Contains(id)).ToList();
        foreach (var id in staleIds)
        {
            _buttonWidthCache.Remove(id);
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_window == null)
        {
            return;
        }

        UpdateInputMode(e.Pointer.Type);

        if (_isTouchDeviceDetected)
        {
            if (!IsTouchLikePointer(e) || !IsEventFromTouchDragHandle(e.Source))
            {
                _touchDragAllowed = false;
                return;
            }

            _touchDragAllowed = true;
            _touchDragStartScreenPoint = _window.PointToScreen(e.GetPosition(_window));
            _touchDragStartWindowPosition = _window.Position;
            e.Pointer.Capture(_window);
            e.Handled = true;
            return;
        }

        if (!e.GetCurrentPoint(_window).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _pointerPressed = true;
        _dragInitiated = false;
        _pointerDownPoint = e.GetPosition(_window);
        _lastPressedArgs = e;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_window == null)
        {
            return;
        }

        UpdateInputMode(e.Pointer.Type);

        if (_isTouchDeviceDetected)
        {
            if (!IsTouchLikePointer(e) || !_touchDragAllowed)
            {
                return;
            }

            var screenPoint = _window.PointToScreen(e.GetPosition(_window));
            var deltaX = screenPoint.X - _touchDragStartScreenPoint.X;
            var deltaY = screenPoint.Y - _touchDragStartScreenPoint.Y;
            var target = new PixelPoint(_touchDragStartWindowPosition.X + deltaX,
                _touchDragStartWindowPosition.Y + deltaY);
            _window.Position = ClampToVisibleScreen(target);
            e.Handled = true;
            return;
        }

        if (!_pointerPressed)
        {
            return;
        }

        if (!_dragInitiated)
        {
            var point = e.GetPosition(_window);
            var dx = point.X - _pointerDownPoint.X;
            var dy = point.Y - _pointerDownPoint.Y;

            if (Math.Abs(dx) + Math.Abs(dy) < 4)
            {
                return;
            }

            _dragInitiated = true;
            if (_lastPressedArgs != null)
            {
                _window.BeginMoveDrag(_lastPressedArgs);
            }

            return;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_window == null)
        {
            return;
        }

        UpdateInputMode(e.Pointer.Type);

        if (_isTouchDeviceDetected)
        {
            if (!IsTouchLikePointer(e))
            {
                return;
            }

            var wasTouchDragging = _touchDragAllowed;
            _touchDragAllowed = false;
            if (!wasTouchDragging)
            {
                return;
            }

            e.Pointer.Capture(null);
            var touchClamped = ClampToVisibleScreen(_window.Position);
            _window.Position = touchClamped;
            SavePosition(touchClamped);
            e.Handled = true;
            return;
        }

        _pointerPressed = false;
        _dragInitiated = false;
        _lastPressedArgs = null;
        e.Pointer.Capture(null);

        var clamped = ClampToVisibleScreen(_window.Position);
        _window.Position = clamped;
        SavePosition(clamped);
    }

    private Border CreateTouchDragHandle(double scale, IBrush foreground)
    {
        var handle = new Border
        {
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(999),
            HorizontalAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(13 * scale, 5 * scale),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 3 * scale,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    CreateDragHandleDot(scale, foreground),
                    CreateDragHandleDot(scale, foreground),
                    CreateDragHandleDot(scale, foreground)
                }
            }
        };

        return handle;
    }

    private static Border CreateDragHandleDot(double scale, IBrush foreground)
    {
        return new Border
        {
            Width = 3 * scale,
            Height = 3 * scale,
            CornerRadius = new CornerRadius(999),
            Background = foreground,
            Opacity = 0.62
        };
    }

    private bool IsEventFromTouchDragHandle(object? source)
    {
        if (_touchDragHandle == null || source is not Visual visual)
        {
            return false;
        }

        var current = visual;
        while (current != null)
        {
            if (ReferenceEquals(current, _touchDragHandle))
            {
                return true;
            }

            current = current.GetVisualParent();
        }

        return false;
    }

    private bool IsTouchLikePointer(PointerEventArgs e)
    {
        return e.Pointer.Type == PointerType.Touch
               || (e.Pointer.Type == PointerType.Mouse && IsRecentTouchGeneratedMouseEvent());
    }

    private bool IsRecentTouchGeneratedMouseEvent()
    {
        return DateTime.UtcNow - _lastTouchGeneratedMouseEventAt <= TouchLikeMouseGracePeriod;
    }

    private void UpdateInputMode(PointerType pointerType)
    {
        if (pointerType == PointerType.Touch)
        {
            SetTouchInputMode(true);
            return;
        }

        if (pointerType == PointerType.Mouse)
        {
            if (IsRecentTouchGeneratedMouseEvent())
            {
                SetTouchInputMode(true);
                return;
            }

            SetTouchInputMode(false);
            return;
        }

        if (pointerType == PointerType.Pen)
        {
            SetTouchInputMode(false);
        }
    }

    private void SetTouchInputMode(bool isTouch)
    {
        if (_isTouchDeviceDetected == isTouch)
        {
            return;
        }

        _isTouchDeviceDetected = isTouch;
        _pointerPressed = false;
        _dragInitiated = false;
        _lastPressedArgs = null;
        _touchDragAllowed = false;
        Dispatcher.UIThread.Post(UpdateDragHandleVisibility);
    }

    private void UpdateDragHandleVisibility()
    {
        if (_touchDragHandle == null)
        {
            return;
        }

        _touchDragHandle.IsVisible = _isTouchDeviceDetected ||
                                     _configHandler.Data.FloatingWindowDragHandleAlwaysVisible;
    }

    private PixelRect GetWindowRect(PixelPoint position)
    {
        if (_window == null)
        {
            return new PixelRect(position.X, position.Y, 0, 0);
        }

        var size = GetWindowPixelSize();
        return new PixelRect(position.X, position.Y, size.Width, size.Height);
    }

    private bool IsWindowInsideAnyScreen(PixelRect rect)
    {
        if (_window?.Screens?.All is not { } screens || screens.Count == 0)
        {
            return true;
        }

        return screens.Any(screen => screen.WorkingArea.Intersects(rect));
    }

    private PixelPoint GetCenteredPositionOnPrimaryScreen()
    {
        if (_window?.Screens?.Primary is not { } primary || _window == null)
        {
            return _window?.Position ?? new PixelPoint(0, 0);
        }

        var area = primary.WorkingArea;
        var size = GetWindowPixelSize();
        var width = size.Width;
        var height = size.Height;

        var x = area.X + (area.Width - width) / 2;
        var y = area.Y + (area.Height - height) / 2;
        return new PixelPoint(x, y);
    }

    private PixelPoint ClampToVisibleScreen(PixelPoint position)
    {
        if (_window == null)
        {
            return position;
        }

        var screens = _window.Screens?.All;
        if (screens == null || screens.Count == 0)
        {
            return position;
        }

        var screen = screens.FirstOrDefault(s => s.WorkingArea.Contains(position))
                     ?? _window.Screens?.Primary
                     ?? screens[0];

        var area = screen.WorkingArea;
        var size = GetWindowPixelSize();
        var width = size.Width;
        var height = size.Height;

        var minX = area.X;
        var minY = area.Y;
        var maxX = area.X + Math.Max(0, area.Width - width);
        var maxY = area.Y + Math.Max(0, area.Height - height);

        return new PixelPoint(Math.Clamp(position.X, minX, maxX), Math.Clamp(position.Y, minY, maxY));
    }

    private PixelSize GetWindowPixelSize()
    {
        if (_window == null)
        {
            return new PixelSize(1, 1);
        }

        var scaling = Math.Max(0.1, _window.RenderScaling);
        var dipSize = _window.ClientSize.Width > 0 && _window.ClientSize.Height > 0
            ? _window.ClientSize
            : new Size(_window.Bounds.Width, _window.Bounds.Height);
        return new PixelSize(
            Math.Max(1, (int)Math.Ceiling(dipSize.Width * scaling)),
            Math.Max(1, (int)Math.Ceiling(dipSize.Height * scaling)));
    }

    private void EnsureWindowPositionVisibleOnStartup()
    {
        if (_window == null)
        {
            return;
        }

        var configured = new PixelPoint(_configHandler.Data.FloatingWindowPositionX, _configHandler.Data.FloatingWindowPositionY);
        var rect = GetWindowRect(configured);
        var target = IsWindowInsideAnyScreen(rect) ? ClampToVisibleScreen(configured) : GetCenteredPositionOnPrimaryScreen();

        _window.Position = target;
        SavePosition(target, forceSave: configured != target);
    }

    private void SavePosition(PixelPoint position, bool forceSave = false)
    {
        var changed = false;

        if (_configHandler.Data.FloatingWindowPositionX != position.X)
        {
            _configHandler.Data.FloatingWindowPositionX = position.X;
            changed = true;
        }

        if (_configHandler.Data.FloatingWindowPositionY != position.Y)
        {
            _configHandler.Data.FloatingWindowPositionY = position.Y;
            changed = true;
        }

        if (forceSave || changed)
        {
            _configHandler.Save();
        }
    }

    private void EnsureRulesetPatrol()
    {
        _lessonsService ??= IAppHost.TryGetService<ILessonsService>();
        if (_lessonsService != null)
        {
            _lessonsService.PostMainTimerTicked -= OnPostMainTimerTicked;
            _lessonsService.PostMainTimerTicked += OnPostMainTimerTicked;
        }
    }

    private void RemoveRulesetPatrol()
    {
        if (_lessonsService != null)
        {
            _lessonsService.PostMainTimerTicked -= OnPostMainTimerTicked;
        }
    }

    private void OnPostMainTimerTicked(object? sender, EventArgs e)
    {
        CheckFloatingWindowRuleset();
        CheckButtonRulesets();
        CheckRowRulesets();
        ApplyVisibility();
    }

    private void RecheckWindowLayer()
    {
        if (_window == null)
        {
            return;
        }

        var windowPlatformService = PlatformServices.WindowPlatformService;
        if (_configHandler.Data.FloatingWindowLayer == 0)
        {
            _window.Topmost = false;
            windowPlatformService.SetWindowFeature(_window, WindowFeatures.Topmost, false);
            windowPlatformService.SetWindowFeature(_window, WindowFeatures.Bottommost, true);
            return;
        }

        _window.Topmost = true;
        windowPlatformService.SetWindowFeature(_window, WindowFeatures.Bottommost, false);
        windowPlatformService.SetWindowFeature(_window, WindowFeatures.Topmost, true);
    }

    public void ToggleWindowLayer()
    {
        SetWindowLayer(_configHandler.Data.FloatingWindowLayer == 1 ? 0 : 1);
    }

    public void SetWindowLayer(int layer)
    {
        _configHandler.Data.FloatingWindowLayer = layer == 1 ? 1 : 0;
        _configHandler.Save();
        Dispatcher.UIThread.Post(() =>
        {
            if (_window != null)
            {
                _window.Topmost = _configHandler.Data.FloatingWindowLayer == 1;
            }
            RecheckWindowLayer();
        });
    }

    public void ToggleWindowProfile()
    {
        var names = _profileManager.GetProfileNames();
        if (names.Count <= 1)
        {
            return;
        }

        var currentName = _profileManager.CurrentProfileName;
        var currentIndex = -1;
        for (int i = 0; i < names.Count; i++)
        {
            if (string.Equals(names[i], currentName, StringComparison.OrdinalIgnoreCase))
            {
                currentIndex = i;
                break;
            }
        }
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var newIndex = (currentIndex + 1) % names.Count;
        var newName = names[newIndex];
        SwitchToProfile(newName);
    }

    public void SwitchToProfile(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return;
        }

        var names = _profileManager.GetProfileNames();
        if (!names.Contains(profileName))
        {
            return;
        }

        if (_profileManager.ProfileFileExists(_profileManager.CurrentProfileName))
        {
            _profileManager.SaveProfile();
        }
        _profileManager.LoadProfile(profileName);
        _configHandler.Data.CurrentFloatingWindowProfile = profileName;
        _configHandler.Save();

        Dispatcher.UIThread.Post(() =>
        {
            RefreshWindowButtons();
            ApplyVisibility();
            RecheckWindowLayer();
        });
    }

    private static IBrush? TryParseColor(string colorString)
    {
        try
        {
            return new SolidColorBrush(Color.Parse(colorString));
        }
        catch
        {
            return null;
        }
    }

    private static IBrush? TryGetButtonPointerOverBrush()
    {
        if (Application.Current == null)
        {
            return null;
        }

        if (Application.Current.TryGetResource("SubtleFillColorSecondaryBrush", null, out var subtle) &&
            subtle is IBrush subtleBrush)
        {
            return subtleBrush;
        }

        if (Application.Current.TryGetResource("ControlFillColorSecondaryBrush", null, out var control) &&
            control is IBrush controlBrush)
        {
            return controlBrush;
        }

        return null;
    }
}

public record FloatingWindowEntry(
    string ButtonId,
    string Icon,
    string Name,
    bool IsRevertStyleActive,
    bool IsRevertEnabled,
    string LayoutName,
    Action TriggerAction,
    Action CancelIsOnAction);
