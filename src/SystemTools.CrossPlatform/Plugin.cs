using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls.ApplicationLifetimes;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Core.Models.Automation;
using ClassIsland.Core.Models.XamlTheme;
using ClassIsland.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SystemTools.CrossPlatform.Actions;
using SystemTools.CrossPlatform.ConfigHandlers;
using SystemTools.CrossPlatform.Controls;
using SystemTools.CrossPlatform.Controls.Components;
using SystemTools.CrossPlatform.Models.ComponentSettings;
using SystemTools.CrossPlatform.Rules;
using SystemTools.CrossPlatform.Rules.Handlers;
using SystemTools.CrossPlatform.Services;
using SystemTools.CrossPlatform.Settings;
using SystemTools.CrossPlatform.SettingsPage;
using SystemTools.CrossPlatform.Shared;
using SystemTools.CrossPlatform.Triggers;
using SystemTools.CrossPlatform.Version;

namespace SystemTools.CrossPlatform;

/// <summary>
/// SystemTools-Cross-platform 插件唯一注册面（礼部 p1-06，p1-05 §4.1）。
/// 按源插件 Plugin.cs 注册先例（只读参照）注册阶段 1 A 档全部功能：
/// 主题 3 + 组件 6 + 规则 4 + 触发器 1 + 行动 15 + 服务群 3（AI 文本链/虚拟放学/版本检查）
/// + 设置页 6（p1-05 §6 A33 覆盖表）；全部功能 ID 使用 SystemTools.CrossPlatform.* 前缀
/// （p0-05 D7，与原插件 ID 空间不相交）；注册一律按源 RegisterActionIfEnabled 先例的启用开闭形态。
/// 行动设置控件经 AddAction&lt;TAction, TSettingsControl&gt; 以 ActionInfo.Id 为 key 的 keyed 注册
/// （宿主 ActionRegistryExtensions.AddAction&lt;TAction,TSettingsControl&gt;，ActionSettingsControlBase.GetInstance
/// 经 GetKeyedService 以行动 ID 解析，满足 p1-03 §3.1 keyed 注册需求）。
/// 兵部 p1-01～p1-04 四批结构化注册清单为本文件唯一输入源（各清单见案卷 evidence/）。
/// 阶段 2 B 档增补（礼部 p2-06）：B19 逐项注册（行动 14/触发器 1/服务 4）与 DI/lifecycle 接线
/// 按兵部 p2-01 §8 / p2-02 §6 / p2-03 §4（W1–W13）三批交接清单落实；悬浮窗行动菜单组与
/// EnableFloatingWindowFeature 组门按源形态恢复（p1-06 §9-4/§9-6 报备恢复点）。
/// </summary>
[PluginEntrance]
public class Plugin : PluginBase
{
    /// <summary>
    /// 系统关机/会话结束监视器（p2-03 W5；源 Plugin.cs:56/:125-127 形态）。Windows 分支承载
    /// WinForms 会话消息面（R-2 条件文件形态 a）；非 Windows 分支为 no-op 护栏（IsSessionEnding 恒 false）。
    /// </summary>
    private SystemShutdownMonitor? _systemShutdownMonitor;

    /// <summary>
    /// DesktopLifetime.ShutdownRequested 处理器（p2-03 W7；源 Plugin.cs:57/:1070-1079 形态）：
    /// 经 SystemShutdownMonitor.MarkIfOsShutdown 标记系统关机路径，AppStopping 时退订。
    /// </summary>
    private EventHandler<ShutdownRequestedEventArgs>? _shutdownRequestedHandler;

    /// <summary>
    /// 阶段 1 A 档注册：配置初始化 → 主题/服务/通知提供方/设置页 DI → 行动菜单树与四类功能注册 →
    /// 生命周期钩子（虚拟放学启动/停止、版本检查、AI 浮窗关闭、配置落盘）。
    /// 阶段 2 B 档接线（礼部 p2-06）：MigrateFromLegacyConfig 旧配置迁移、悬浮窗服务启停、
    /// 自动主题同步/遮挡检测/ClassIsland 内存清理服务生命周期与 ApplyConfig、
    /// SystemShutdownMonitor 注册及 IsSessionEnding→CancelPlanOnAppStopping 联动
    /// （p2-01 §8 / p2-02 §6 / p2-03 §4 W1-W13 三批交接清单）。
    /// </summary>
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        // ========== 初始化配置（p1-03 §3.1 DI/初始化接线需求；源 Plugin.cs:67-70 先例） ==========
        GlobalConstants.PluginConfigFolder = PluginConfigFolder;
        GlobalConstants.Information.PluginFolder = Info.PluginFolderPath;
        GlobalConstants.Information.PluginVersion = Info.Manifest.Version;
        GlobalConstants.MainConfig = new MainConfigHandler(PluginConfigFolder);

        // 源 Plugin.cs:74 的 services.AddLogging() 未随入：宿主 Generic Host 先于插件初始化注册日志
        // 服务，该调用为冗余防御（礼部 p1-06 核减项，已在批证据注记）。

        // ========== 注册主题 3 项（源 Plugin.cs:75-110 形态） ==========
        // Banner 经 ThemeBannerCacheService 缓存路径（后台下载主题预览图，缓存失败时主题列表无预览图，
        // 不影响主题本身）。Id/Name/Description/版本与安全区参数随源，Id 按前缀规则改写。
        services.AddXamlTheme(
            new Uri("avares://SystemTools.CrossPlatform/Themes/CardTypeComponent/Styles.axaml"),
            new ThemeManifest
            {
                Id = "SystemTools.CrossPlatform.Card-type-component",
                Name = "Card-type Component",
                Description = "A theme that provides a main interface with higher components",
                Version = "1.0.0.0",
                Author = "Programmer-MrWang",
                Banner = ThemeBannerCacheService.BannerPath,
                VerticalSafeAreaPx = 20
            });
        services.AddXamlTheme(
            new Uri("avares://SystemTools.CrossPlatform/Themes/ClassWidgets/Styles.axaml"),
            new ThemeManifest
            {
                Id = "SystemTools.CrossPlatform.classwidgets",
                Name = "ClassWidgets 2 Style",
                Description = "A ClassWidgets-inspired main interface theme for ClassIsland",
                Version = "1.0.0.0",
                Author = "Programmer-MrWang",
                Banner = ThemeBannerCacheService.ClassWidgetsBannerPath,
                VerticalSafeAreaPx = 20
            });
        services.AddXamlTheme(
            new Uri("avares://SystemTools.CrossPlatform/Themes/NotchStyle/Styles.axaml"),
            new ThemeManifest
            {
                Id = "SystemTools.CrossPlatform.notch-style",
                Name = "Notch Style",
                Description = "An iPhone X to iPhone 12 inspired notch-style main interface theme for ClassIsland",
                Version = "1.0.0.0",
                Author = "Programmer-MrWang",
                Banner = ThemeBannerCacheService.NotchStyleBannerPath,
                VerticalSafeAreaPx = 0
            });

        // ========== 服务 DI（p1-03 §3.1 / p1-04 §6 注册清单） ==========
        services.AddSingleton(GlobalConstants.MainConfig);
        services.AddSingleton<ThemeBannerCacheService>();
        services.AddSingleton<AboutTitleImageCacheService>();
        services.AddSingleton<FloatingWindowProfileManager>();
        // B 档服务 DI（p2-03 W1 源 :115；p2-01 §8 源 :122——新增维护接口对
        // IProcessMemoryMaintenanceService/ProcessMemoryMaintenanceService 须在内存清理服务之前，
        // 跨平台注册面免平台条件代码，p2-01 §1.3；顺序随源 :114-:124 相对序）。
        services.AddSingleton<FloatingWindowService>();
        services.AddSingleton<IProcessMemoryMaintenanceService, ProcessMemoryMaintenanceService>();
        services.AddSingleton<ClassIslandMemoryAutoCleanupService>();
        // 系统关机/会话结束监视器（p2-03 W5；源 :125-127 形态；非 Windows 分支 Start 为 no-op 护栏）。
        _systemShutdownMonitor = new SystemShutdownMonitor();
        _systemShutdownMonitor.Start();
        services.AddSingleton(_systemShutdownMonitor);
        services.AddSingleton<VirtualAfterSchoolService>();
        services.AddSingleton<IOpenAiCompatibleService, OpenAiCompatibleService>();
        if (GlobalConstants.MainConfig?.Data.EnableAiService == true)
        {
            services.AddSingleton<AiConversationStore>();
            services.AddSingleton<AiChatOperationGate>();
            services.AddSingleton<AiPromptService>();
            services.AddSingleton<ClassIslandProfileAiService>();
            services.AddSingleton<ClassIslandActionAiService>();
            services.AddSingleton<AiChatWindowService>();
        }

        // 通知提供方（p1-04 §6：AddNotificationProvider + 去重后具体类型/托管服务复用同一实例，
        // 防止每个提醒渠道被注册两次；源 Plugin.cs:141-149 先例）。
        services.AddNotificationProvider<SystemToolsNotificationProvider>();
        var notificationHostedService = services.Single(descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(SystemToolsNotificationProvider));
        services.Remove(notificationHostedService);
        services.AddSingleton<SystemToolsNotificationProvider>();
        services.AddSingleton<IHostedService>(serviceProvider =>
            serviceProvider.GetRequiredService<SystemToolsNotificationProvider>());

        // ========== 注册设置页面 6 页（礼部 p1-06 SettingsPage\ 骨架；源 Plugin.cs:175-187 形态） ==========
        services.AddSettingsPageGroup("SystemTools.CrossPlatform.settings", "\uE079", "SystemTools 设置");
        services.AddSettingsPage<SystemToolsSettingsPage>();
        services.AddSettingsPage<MoreFeaturesOptionsSettingsPage>();
        if (GlobalConstants.MainConfig?.Data.EnableAiService == true)
        {
            services.AddSettingsPage<AiChatSettingsPage>();
        }
        // 悬浮窗编辑页注册门恢复（p1-06 §9-6 报备恢复点兑现；p2-03 W11；源 :182-185 形态：
        // EnableFloatingWindowFeature 已由 p2-03 增补至配置根，按源条件注册）。
        if (GlobalConstants.MainConfig?.Data.EnableFloatingWindowFeature == true)
        {
            services.AddSettingsPage<FloatingWindowEditorSettingsPage>();
        }
        services.AddSettingsPage<AboutSettingsPage>();

        // ========== 构建行动树（根据配置；A 档面，源 BuildBaseActionTree 先例） ==========
        BuildBaseActionTree();

        // ========== 注册行动、触发器和组件（根据配置） ==========
        RegisterBaseActions(services);
        RegisterBaseTriggers(services);
        RegisterBaseRules(services);
        RegisterBaseComponents(services);

        // ========== 生命周期钩子 ==========
        // AppStarted 接线：主题预览图/关于页题图缓存、悬浮窗服务、内存清理、虚拟放学（随源相对序）；
        // 源 handler 内的语音/人脸/USB/系统内存清理等已删功能服务与日志面不随入。
        AppBase.Current.AppStarted += (_, _) =>
        {
            // ShutdownRequested 处理器注册（p2-03 W7；源 :203 形态；方法幂等）。
            RegisterShutdownRequestedHandler();
            // 主题预览图/关于页题图缓存服务：AppStarted → Start()（源 Plugin.cs:207-208 形态，
            // 后台下载失败仅记日志，下次启动重试，不影响插件其它功能）。
            IAppHost.GetService<ThemeBannerCacheService>().Start();
            IAppHost.GetService<AboutTitleImageCacheService>().Start();
            // 旧版悬浮窗配置迁移到文件存储（p2-03 W4；源 :207；p1-03 §3.1 阶段 1 预留接线路径兑现）。
            IAppHost.GetService<FloatingWindowProfileManager>().MigrateFromLegacyConfig(GlobalConstants.MainConfig!.Data);
            // 悬浮窗服务启动，EnableFloatingWindowFeature 门随源（p2-03 W2；源 :210-213 形态）。
            if (GlobalConstants.MainConfig?.Data.EnableFloatingWindowFeature == true)
            {
                IAppHost.GetService<FloatingWindowService>().Start();
            }
            // ClassIsland 内存自动清理：ApplyConfig（p2-01 §8；源 :218；GlobalConstants.MainConfig
            // 赋值于 Initialize 先行完成，满足"须在 MainConfig 赋值之后"约束，p2-05 §2.1 #10）。
            IAppHost.GetService<ClassIslandMemoryAutoCleanupService>().ApplyConfig();
            // 虚拟放学：AppStarted → Start()（p1-04 §6；源 Plugin.cs:220）。
            IAppHost.GetService<VirtualAfterSchoolService>().Start();
        };
        // 版本检查：静态服务，AppStarted → CheckAndNotify()（p1-04 §6；源 Plugin.cs:284）。
        AppBase.Current.AppStarted += (_, _) => { VersionCheckService.CheckAndNotify(); };
        // ShutdownRequested 处理器于 Initialize 直注一次（源 :287 形态；方法幂等，AppStarted 内重复调用无害）。
        RegisterShutdownRequestedHandler();
        AppBase.Current.AppStopping += OnAppStopping;
    }

    private void OnAppStopping(object? sender, EventArgs e)
    {
        // 会话结束标记读取（p2-03 W6；源 Plugin.cs:1034-1035 形态）：SystemShutdownMonitor 非
        // Windows no-op 分支恒 false → CancelPlanOnAppStopping 走"ClassIsland 主动退出"分支（源 :1055 语义）。
        var systemShutdownMonitor = _systemShutdownMonitor ?? IAppHost.GetService<SystemShutdownMonitor>();
        var isSessionEnding = systemShutdownMonitor.IsSessionEnding;
        // 退订 ShutdownRequested 处理器（源 :1036-1040 形态；W7 配套）。
        if (AppBase.Current.DesktopLifetime is { } desktopLifetime && _shutdownRequestedHandler != null)
        {
            desktopLifetime.ShutdownRequested -= _shutdownRequestedHandler;
            _shutdownRequestedHandler = null;
        }
        // ClassIsland 内存自动清理：AppStopping → Stop()（p2-01 §8 lifecycle；源 Plugin.cs:1045）。
        IAppHost.GetService<ClassIslandMemoryAutoCleanupService>().Stop();
        // 虚拟放学：AppStopping → Stop()（p1-04 §6；源 Plugin.cs:1047）。
        IAppHost.GetService<VirtualAfterSchoolService>().Stop();
        // 高级计时关机：会话结束标记传递静态取消路径（p2-01 §8 lifecycle / p2-03 W6；源 Plugin.cs:1048；
        // 看门狗移除后宿主退出路径承担计划取消，p2-01 §2-A5；源 :1049-1056 信息日志面未随入，见批证据报备）。
        AdvancedShutdownAction.CancelPlanOnAppStopping(isSessionEnding);
        // 会话结束监视器释放（p2-03 W6；源 Plugin.cs:1057）。
        systemShutdownMonitor.Dispose();
        // AI 浮窗：无直接注册，AppStopping 时按需取用并关闭（p1-04 §6；源 Plugin.cs:1058）。
        IAppHost.TryGetService<AiChatWindowService>()?.Close();
        // 悬浮窗服务：AppStopping → Stop()，EnableFloatingWindowFeature 门随源（p2-03 W3；源 Plugin.cs:1061-1064）。
        if (GlobalConstants.MainConfig?.Data.EnableFloatingWindowFeature == true)
        {
            IAppHost.GetService<FloatingWindowService>().Stop();
        }
        // 配置根落盘（源 OnAppStopping 配置保存先例 Plugin.cs:1067）。
        GlobalConstants.MainConfig?.Save();
    }

    /// <summary>
    /// 注册 DesktopLifetime.ShutdownRequested 处理器（p2-03 W7；源 Plugin.cs:1070-1079 形态）：
    /// 幂等（已注册或无 DesktopLifetime 时跳过）；处理器经 MarkIfOsShutdown(object) 标记系统关机
    /// （p2-03 条件文件以 object 承载事件参数，两分支同签名）。
    /// </summary>
    private void RegisterShutdownRequestedHandler()
    {
        if (_shutdownRequestedHandler != null || AppBase.Current.DesktopLifetime is not { } desktopLifetime)
        {
            return;
        }

        _shutdownRequestedHandler = (_, args) => _systemShutdownMonitor?.MarkIfOsShutdown(args);
        desktopLifetime.ShutdownRequested += _shutdownRequestedHandler;
    }

    #region 注册方法（A 档面，源 RegisterBaseActions/RegisterBaseTriggers/RegisterBaseRules/RegisterBaseComponents 先例）

    private void RegisterBaseActions(IServiceCollection services)
    {
        var config = GlobalConstants.MainConfig!.Data;

        // 电源选项（p2-01 §8；源 Plugin.cs:375-382 先例，B4–B10；B8–B10 无设置对，单参形态随源）。
        // 三平台能力门（SystemPowerCapability）：本机当前环境不支持的电源行动不注册也不显示；
        // Windows 全 7 项、Linux(X11/Wayland, systemctl/loginctl) 全 7 项、macOS 6 项（锁定屏幕无 root
        // 途径除外）。定时关机族在 Linux/macOS 为应用内计划（ShutdownPlanCenter）。
        RegisterPowerActionIfEnabled<ShutdownAction, ShutdownSettingsControl>(services, config,
            SystemPowerCapability.TimedShutdownId);
        RegisterPowerActionIfEnabled<AdvancedShutdownAction, AdvancedShutdownSettingsControl>(services, config,
            SystemPowerCapability.AdvancedShutdownId);
        RegisterPowerActionIfEnabled<LockScreenAction, ShortcutKeyNotificationSettingsControl>(services, config,
            SystemPowerCapability.LockScreenId);
        RegisterPowerActionIfEnabled<CancelShutdownAction, ShortcutKeyNotificationSettingsControl>(services, config,
            SystemPowerCapability.CancelShutdownId);
        RegisterPowerActionIfEnabled<ImmediateRestartAction>(services, config, SystemPowerCapability.ImmediateRestartId);
        RegisterPowerActionIfEnabled<ImmediateShutdownAction>(services, config, SystemPowerCapability.ImmediateShutdownId);
        RegisterPowerActionIfEnabled<SleepAction>(services, config, SystemPowerCapability.SleepId);

        // 文件操作（p2-02 §6；源 Plugin.cs:385-387 先例，B1–B3）
        RegisterActionIfEnabled<CopyAction, CopySettingsControl>(services, config, "SystemTools.CrossPlatform.Copy");
        RegisterActionIfEnabled<MoveAction, MoveSettingsControl>(services, config, "SystemTools.CrossPlatform.Move");
        RegisterActionIfEnabled<DeleteAction, DeleteSettingsControl>(services, config, "SystemTools.CrossPlatform.Delete");

        // 实用工具
        RegisterActionIfEnabled<KillProcessAction, KillProcessSettingsControl>(services, config,
            "SystemTools.CrossPlatform.KillProcess");
        RegisterActionIfEnabled<ShowToastAction, ShowToastSettingsControl>(services, config,
            "SystemTools.CrossPlatform.ShowToast");

        // 悬浮窗设置（组门恢复，p1-06 §9-4 报备恢复点兑现；源 :414-424 以 EnableFloatingWindowFeature
        // 为门；B11/B12 新增注册（p2-03 W8/W9），A3/A4 自阶段 1 常规开闭注册恢复入源门，注册序随源 :416-423）
        if (config.EnableFloatingWindowFeature)
        {
            RegisterActionIfEnabled<ShowFloatingWindowAction, ShowFloatingWindowSettingsControl>(services, config,
                "SystemTools.CrossPlatform.ShowFloatingWindow");
            RegisterActionIfEnabled<ToggleFloatingWindowLayerAction, ToggleFloatingWindowLayerSettingsControl>(services, config,
                "SystemTools.CrossPlatform.ToggleFloatingWindowLayer");
            RegisterActionIfEnabled<ToggleFloatingWindowProfileAction, ToggleFloatingWindowProfileSettingsControl>(services, config,
                "SystemTools.CrossPlatform.ToggleFloatingWindowProfile");
            RegisterActionIfEnabled<SwitchFloatingWindowThemeAction, SwitchFloatingWindowThemeSettingsControl>(services, config,
                "SystemTools.CrossPlatform.SwitchFloatingWindowTheme");
        }

        // 媒体工具
        RegisterActionIfEnabled<BackgroundPlayAudioAction, BackgroundPlayAudioSettingsControl>(services, config,
            "SystemTools.CrossPlatform.BackgroundPlayAudio");

        // 高级自动化工具
        RegisterActionIfEnabled<ActionFlowExecutionConfirmationAction, ActionFlowExecutionConfirmationSettingsControl>(
            services, config, "SystemTools.CrossPlatform.ActionFlowExecutionConfirmation");
        RegisterActionIfEnabled<TriggerCustomTriggerAction, TriggerCustomTriggerSettingsControl>(services, config,
            "SystemTools.CrossPlatform.TriggerCustomTrigger");
        RegisterActionIfEnabled<ToggleWorkflowAction, ToggleWorkflowSettingsControl>(services, config,
            "SystemTools.CrossPlatform.ToggleWorkflow");

        // AI 功能（源 Plugin.cs:450-457 先例：A9 行动注册于 EnableAiService 开关内）
        if (config.EnableAiService)
        {
            RegisterActionIfEnabled<ShowAiChatDialogAction>(services, config, "SystemTools.CrossPlatform.ShowAiChatDialog");
        }

        // 其他工具
        RegisterActionIfEnabled<FullscreenClockAction, FullscreenClockSettingsControl>(services, config,
            "SystemTools.CrossPlatform.FullscreenClock");

        // ClassIsland（A11/A13/A14/A15 共享 ShortcutKeyNotification 设置对）
        RegisterActionIfEnabled<ClearAllNotificationsAction, ShortcutKeyNotificationSettingsControl>(services, config,
            "SystemTools.CrossPlatform.ClearAllNotifications");
        RegisterActionIfEnabled<LoadTemporaryClassPlanAction, LoadTemporaryClassPlanSettingsControl>(services, config,
            "SystemTools.CrossPlatform.LoadTemporaryClassPlan");
        RegisterActionIfEnabled<OpenAppSettingsAction, ShortcutKeyNotificationSettingsControl>(services, config,
            "SystemTools.CrossPlatform.OpenAppSettings");
        RegisterActionIfEnabled<OpenProfileEditorAction, ShortcutKeyNotificationSettingsControl>(services, config,
            "SystemTools.CrossPlatform.OpenProfileEditor");
        RegisterActionIfEnabled<OpenClassSwapWindowAction, ShortcutKeyNotificationSettingsControl>(services, config,
            "SystemTools.CrossPlatform.OpenClassSwapWindow");
    }

    private void RegisterBaseTriggers(IServiceCollection services)
    {
        var config = GlobalConstants.MainConfig!.Data;

        RegisterTriggerIfEnabled<ActionInProgressTrigger, ActionInProgressTriggerSettings>(services, config,
            "SystemTools.CrossPlatform.ActionInProgressTrigger");

        // 悬浮窗触发器（组门恢复：源 :475-479 以 EnableFloatingWindowFeature 为门；p2-03 W10/W11）
        if (config.EnableFloatingWindowFeature)
        {
            RegisterTriggerIfEnabled<FloatingWindowTrigger, FloatingWindowTriggerSettings>(services, config,
                "SystemTools.CrossPlatform.FloatingWindowTrigger");
        }
    }

    private void RegisterBaseRules(IServiceCollection services)
    {
        var config = GlobalConstants.MainConfig!.Data;

        // 双参 AddRule<TSettings, TSettingsControl> 形态（p1-02 §2 清单；源 Plugin.cs:486-508 先例）。
        if (config.IsRuleEnabled("SystemTools.CrossPlatform.ProcessRunningRule"))
        {
            services.AddRule<ProcessRunningRuleSettings, ProcessRunningRuleSettingsControl>(
                "SystemTools.CrossPlatform.ProcessRunningRule", "程序正在运行", "\uE342", ProcessRunningRuleHandler.Handle);
        }

        if (config.IsRuleEnabled("SystemTools.CrossPlatform.UsingClassPlanRule"))
        {
            services.AddRule<UsingClassPlanRuleSettings, UsingClassPlanRuleSettingsControl>(
                "SystemTools.CrossPlatform.UsingClassPlanRule", "正在使用某课程表", "\uE6B1", UsingClassPlanRuleHandler.Handle);
        }

        if (config.IsRuleEnabled("SystemTools.CrossPlatform.UsingTimeLayoutRule"))
        {
            services.AddRule<UsingTimeLayoutRuleSettings, UsingTimeLayoutRuleSettingsControl>(
                "SystemTools.CrossPlatform.UsingTimeLayoutRule", "正在使用某时间表", "\uE69D", UsingTimeLayoutRuleHandler.Handle);
        }

        if (config.IsRuleEnabled("SystemTools.CrossPlatform.InTimePeriodRule"))
        {
            services.AddRule<InTimePeriodRuleSettings, InTimePeriodRuleSettingsControl>(
                "SystemTools.CrossPlatform.InTimePeriodRule", "是否在某时间段", "\uE4CA", InTimePeriodRuleHandler.Handle);
        }
    }

    private void RegisterBaseComponents(IServiceCollection services)
    {
        var config = GlobalConstants.MainConfig!.Data;

        RegisterComponentIfEnabled<NetworkStatusComponent, NetworkStatusSettingsControl>(services, config,
            "SystemTools.CrossPlatform.NetworkStatus");
        RegisterComponentIfEnabled<ClipboardContentComponent, ClipboardContentSettingsControl>(services, config,
            "SystemTools.CrossPlatform.ClipboardContent");
        RegisterComponentIfEnabled<LocalQuoteComponent, LocalQuoteSettingsControl>(services, config,
            "SystemTools.CrossPlatform.LocalQuote");
        RegisterComponentIfEnabled<NextClassDisplayComponent, NextClassDisplaySettingsControl>(services, config,
            "SystemTools.CrossPlatform.NextClassDisplay");
        RegisterComponentIfEnabled<BetterCarouselContainerComponent, BetterCarouselContainerSettingsControl>(services, config,
            "SystemTools.CrossPlatform.BetterCarouselContainer");
        RegisterComponentIfEnabled<ScrollingTextComponent, ScrollingTextSettingsControl>(services, config,
            "SystemTools.CrossPlatform.ScrollingText");
    }

    #endregion

    #region 条件注册辅助方法（源 RegisterActionIfEnabled 等先例 Plugin.cs:572-612，A 档面随源保留）

    private void RegisterActionIfEnabled<TAction>(IServiceCollection services, MainConfigData config, string actionId)
        where TAction : ActionBase
    {
        if (config.IsActionEnabled(actionId))
        {
            services.AddAction<TAction>();
        }
    }

    private void RegisterActionIfEnabled<TAction, TSettingsControl>(IServiceCollection services, MainConfigData config,
        string actionId)
        where TAction : ActionBase
        where TSettingsControl : ActionSettingsControlBase
    {
        if (config.IsActionEnabled(actionId))
        {
            services.AddAction<TAction, TSettingsControl>();
        }
    }

    /// <summary>电源族注册：能力门（本机环境不支持则整体跳过）+ 启用开关（与 RegisterActionIfEnabled 同级）。</summary>
    private void RegisterPowerActionIfEnabled<TAction>(IServiceCollection services, MainConfigData config, string actionId)
        where TAction : ActionBase
    {
        if (!SystemPowerCapability.IsActionSupported(actionId) || !config.IsActionEnabled(actionId))
        {
            return;
        }

        services.AddAction<TAction>();
    }

    private void RegisterPowerActionIfEnabled<TAction, TSettingsControl>(IServiceCollection services,
        MainConfigData config, string actionId)
        where TAction : ActionBase
        where TSettingsControl : ActionSettingsControlBase
    {
        if (!SystemPowerCapability.IsActionSupported(actionId) || !config.IsActionEnabled(actionId))
        {
            return;
        }

        services.AddAction<TAction, TSettingsControl>();
    }

    private void RegisterTriggerIfEnabled<TTrigger, TSettings>(IServiceCollection services, MainConfigData config,
        string triggerId)
        where TTrigger : TriggerBase
        where TSettings : TriggerSettingsControlBase
    {
        if (config.IsTriggerEnabled(triggerId))
        {
            services.AddTrigger<TTrigger, TSettings>();
        }
    }

    private void RegisterComponentIfEnabled<TComponent, TSettingsControl>(IServiceCollection services,
        MainConfigData config, string componentId)
        where TComponent : ComponentBase
        where TSettingsControl : ComponentBase
    {
        if (config.IsComponentEnabled(componentId))
        {
            services.AddComponent<TComponent, TSettingsControl>();
        }
    }

    #endregion

    #region 菜单构建（A 档面；源 BuildBaseActionTree/BuildXxxMenu 先例 Plugin.cs:618-996，B/C 项与悬浮窗组除外）

    private void BuildBaseActionTree()
    {
        var config = GlobalConstants.MainConfig!.Data;

        IActionService.ActionMenuTree.Add(new ActionMenuTreeGroup("SystemTools 行动", "\uE079"));

        // 电源选项（p2-01 §8 行动菜单树交接行；源 :641-646 组门 + :805-821 逐项；ID 前缀改写，文案随源）
        // 组门叠加平台能力门（SystemPowerCapability）：本机环境不支持的电源行动不注册也不入组显示
        //（macOS 无“锁定屏幕”；Linux/macOS 定时关机族为应用内计划，见 ShutdownPlanCenter）。
        if (HasAnyPowerActionEnabled(config))
        {
            IActionService.ActionMenuTree["SystemTools 行动"].Add(new ActionMenuTreeGroup("电源选项…", "\uEDE8"));
            BuildPowerMenu(config);
        }

        // 文件操作（尚书省微修 1 补齐：p2-02 §6 行动注册对应菜单组织面；源 :648-653 组门形态随源，
        // 三成员均为 B 档在册注册项，组门无裁剪）
        if (HasAnyActionEnabled(config, "SystemTools.CrossPlatform.Copy", "SystemTools.CrossPlatform.Move",
                "SystemTools.CrossPlatform.Delete"))
        {
            IActionService.ActionMenuTree["SystemTools 行动"].Add(new ActionMenuTreeGroup("文件操作…", "\uE759"));
            BuildFileMenu(config);
        }

        // 实用工具
        if (HasAnyActionEnabled(config, "SystemTools.CrossPlatform.KillProcess", "SystemTools.CrossPlatform.ShowToast"))
        {
            IActionService.ActionMenuTree["SystemTools 行动"].Add(new ActionMenuTreeGroup("实用工具…", "\uE352"));
            BuildUtilityMenu(config);
        }

        // 悬浮窗设置（组门恢复，p1-06 §9-4 报备恢复点兑现；源 :671-677 形态：EnableFloatingWindowFeature
        // + HasAnyActionEnabled(B11/B12/A3/A4)；B11/B12/A3/A4 归组随源）
        if (config.EnableFloatingWindowFeature && HasAnyActionEnabled(config, "SystemTools.CrossPlatform.ShowFloatingWindow",
                "SystemTools.CrossPlatform.ToggleFloatingWindowLayer", "SystemTools.CrossPlatform.ToggleFloatingWindowProfile",
                "SystemTools.CrossPlatform.SwitchFloatingWindowTheme"))
        {
            IActionService.ActionMenuTree["SystemTools 行动"].Add(new ActionMenuTreeGroup("悬浮窗设置…", "\uEA37"));
            BuildFloatingWindowMenu(config);
        }

        // 媒体工具
        if (HasAnyActionEnabled(config, "SystemTools.CrossPlatform.BackgroundPlayAudio"))
        {
            IActionService.ActionMenuTree["SystemTools 行动"].Add(new ActionMenuTreeGroup("媒体工具…", "\uE342"));
            BuildMediaToolsMenu(config);
        }

        // 高级自动化工具
        if (HasAnyActionEnabled(config, "SystemTools.CrossPlatform.ActionFlowExecutionConfirmation",
                "SystemTools.CrossPlatform.TriggerCustomTrigger", "SystemTools.CrossPlatform.ToggleWorkflow"))
        {
            IActionService.ActionMenuTree["SystemTools 行动"].Add(new ActionMenuTreeGroup("高级自动化工具…", "\uE01F"));
            BuildAdvancedAutomationMenu(config);
        }

        // AI 功能
        if (config.EnableAiService && HasAnyActionEnabled(config, "SystemTools.CrossPlatform.ShowAiChatDialog"))
        {
            IActionService.ActionMenuTree["SystemTools 行动"].Add(new ActionMenuTreeGroup("AI 功能…", "\uEFFF"));
            BuildAiMenu(config);
        }

        // 其他工具
        if (config.IsActionEnabled("SystemTools.CrossPlatform.FullscreenClock"))
        {
            IActionService.ActionMenuTree["SystemTools 行动"].Add(new ActionMenuTreeGroup("其他工具…", "\uE32C"));
            BuildOtherMenu(config);
        }

        // ClassIsland
        if (HasAnyActionEnabled(config, "SystemTools.CrossPlatform.ClearAllNotifications",
                "SystemTools.CrossPlatform.LoadTemporaryClassPlan", "SystemTools.CrossPlatform.OpenAppSettings",
                "SystemTools.CrossPlatform.OpenProfileEditor", "SystemTools.CrossPlatform.OpenClassSwapWindow"))
        {
            IActionService.ActionMenuTree["SystemTools 行动"].Add(new ActionMenuTreeGroup("ClassIsland…", "\uE5CB"));
            BuildClassIslandMenu(config);
        }
    }

    private bool HasAnyActionEnabled(MainConfigData config, params string[] actionIds)
    {
        return actionIds.Any(id => config.IsActionEnabled(id));
    }

    private void BuildPowerMenu(MainConfigData config)
    {
        var items = new List<ActionMenuTreeItem>();

        if (SystemPowerCapability.IsActionSupported(SystemPowerCapability.TimedShutdownId)
            && config.IsActionEnabled(SystemPowerCapability.TimedShutdownId))
            items.Add(new ActionMenuTreeItem(SystemPowerCapability.TimedShutdownId, "计时关机", "\uE4C4"));
        if (SystemPowerCapability.IsActionSupported(SystemPowerCapability.AdvancedShutdownId)
            && config.IsActionEnabled(SystemPowerCapability.AdvancedShutdownId))
            items.Add(new ActionMenuTreeItem(SystemPowerCapability.AdvancedShutdownId, "高级计时关机", "\uE4D2"));
        if (SystemPowerCapability.IsActionSupported(SystemPowerCapability.CancelShutdownId)
            && config.IsActionEnabled(SystemPowerCapability.CancelShutdownId))
            items.Add(new ActionMenuTreeItem(SystemPowerCapability.CancelShutdownId, "取消关机计划", "\uE4CC"));
        if (SystemPowerCapability.IsActionSupported(SystemPowerCapability.LockScreenId)
            && config.IsActionEnabled(SystemPowerCapability.LockScreenId))
            items.Add(new ActionMenuTreeItem(SystemPowerCapability.LockScreenId, "锁定屏幕", "\uEAF0"));
        if (SystemPowerCapability.IsActionSupported(SystemPowerCapability.ImmediateRestartId)
            && config.IsActionEnabled(SystemPowerCapability.ImmediateRestartId))
            items.Add(new ActionMenuTreeItem(SystemPowerCapability.ImmediateRestartId, "立即重启", "\uE0BD"));
        if (SystemPowerCapability.IsActionSupported(SystemPowerCapability.ImmediateShutdownId)
            && config.IsActionEnabled(SystemPowerCapability.ImmediateShutdownId))
            items.Add(new ActionMenuTreeItem(SystemPowerCapability.ImmediateShutdownId, "立即关机", "\uEDE9"));
        if (SystemPowerCapability.IsActionSupported(SystemPowerCapability.SleepId)
            && config.IsActionEnabled(SystemPowerCapability.SleepId))
            items.Add(new ActionMenuTreeItem(SystemPowerCapability.SleepId, "睡眠", "\uF44B"));

        if (items.Count > 0)
        {
            foreach (var item in items)
            {
                IActionService.ActionMenuTree["SystemTools 行动"]["电源选项…"].Add(item);
            }
        }
    }

    /// <summary>电源族组门：任一行动“本机支持且配置启用”即入树。</summary>
    private static bool HasAnyPowerActionEnabled(MainConfigData config)
    {
        return SystemPowerCapability.PowerActionIds.Any(id =>
            SystemPowerCapability.IsActionSupported(id) && config.IsActionEnabled(id));
    }

    private void BuildFileMenu(MainConfigData config)
    {
        var items = new List<ActionMenuTreeItem>();

        if (config.IsActionEnabled("SystemTools.CrossPlatform.Copy"))
            items.Add(new ActionMenuTreeItem("SystemTools.CrossPlatform.Copy", "复制", "\uE6AB"));
        if (config.IsActionEnabled("SystemTools.CrossPlatform.Move"))
            items.Add(new ActionMenuTreeItem("SystemTools.CrossPlatform.Move", "移动", "\uE6E7"));
        if (config.IsActionEnabled("SystemTools.CrossPlatform.Delete"))
            items.Add(new ActionMenuTreeItem("SystemTools.CrossPlatform.Delete", "删除", "\uE61D"));

        if (items.Count > 0)
        {
            foreach (var item in items)
            {
                IActionService.ActionMenuTree["SystemTools 行动"]["文件操作…"].Add(item);
            }
        }
    }

    private void BuildFloatingWindowMenu(MainConfigData config)
    {
        var items = new List<ActionMenuTreeItem>();

        if (config.EnableFloatingWindowFeature && config.IsActionEnabled("SystemTools.CrossPlatform.ShowFloatingWindow"))
            items.Add(new ActionMenuTreeItem("SystemTools.CrossPlatform.ShowFloatingWindow", "显示悬浮窗", "\uEA37"));
        if (config.EnableFloatingWindowFeature && config.IsActionEnabled("SystemTools.CrossPlatform.ToggleFloatingWindowLayer"))
            items.Add(new ActionMenuTreeItem("SystemTools.CrossPlatform.ToggleFloatingWindowLayer", "切换悬浮窗层级", "\uE9A8"));
        if (config.EnableFloatingWindowFeature && config.IsActionEnabled("SystemTools.CrossPlatform.ToggleFloatingWindowProfile"))
            items.Add(new ActionMenuTreeItem("SystemTools.CrossPlatform.ToggleFloatingWindowProfile", "切换悬浮窗配置方案", "\uE9A8"));
        if (config.EnableFloatingWindowFeature && config.IsActionEnabled("SystemTools.CrossPlatform.SwitchFloatingWindowTheme"))
            items.Add(new ActionMenuTreeItem("SystemTools.CrossPlatform.SwitchFloatingWindowTheme", "切换悬浮窗主题", "\uE790"));

        if (items.Count > 0)
        {
            foreach (var item in items)
            {
                IActionService.ActionMenuTree["SystemTools 行动"]["悬浮窗设置…"].Add(item);
            }
        }
    }

    private void BuildUtilityMenu(MainConfigData config)
    {
        var items = new List<ActionMenuTreeItem>();

        if (config.IsActionEnabled("SystemTools.CrossPlatform.KillProcess"))
            items.Add(new ActionMenuTreeItem("SystemTools.CrossPlatform.KillProcess", "退出进程", "\uE0DE"));
        if (config.IsActionEnabled("SystemTools.CrossPlatform.ShowToast"))
            items.Add(new ActionMenuTreeItem("SystemTools.CrossPlatform.ShowToast", "拉起自定义系统通知", "\uE3E4"));

        if (items.Count > 0)
        {
            foreach (var item in items)
            {
                IActionService.ActionMenuTree["SystemTools 行动"]["实用工具…"].Add(item);
            }
        }
    }

    private void BuildMediaToolsMenu(MainConfigData config)
    {
        if (config.IsActionEnabled("SystemTools.CrossPlatform.BackgroundPlayAudio"))
        {
            IActionService.ActionMenuTree["SystemTools 行动"]["媒体工具…"].Add(
                new ActionMenuTreeItem("SystemTools.CrossPlatform.BackgroundPlayAudio", "后台播放音频", "\uEBCC"));
        }
    }

    private void BuildAdvancedAutomationMenu(MainConfigData config)
    {
        var items = new List<ActionMenuTreeItem>();

        if (config.IsActionEnabled("SystemTools.CrossPlatform.ActionFlowExecutionConfirmation"))
            items.Add(new ActionMenuTreeItem("SystemTools.CrossPlatform.ActionFlowExecutionConfirmation", "行动流执行确认", "\uE01D"));
        if (config.IsActionEnabled("SystemTools.CrossPlatform.TriggerCustomTrigger"))
            items.Add(new ActionMenuTreeItem("SystemTools.CrossPlatform.TriggerCustomTrigger", "触发指定触发器", "\uEAB7"));
        if (config.IsActionEnabled("SystemTools.CrossPlatform.ToggleWorkflow"))
            items.Add(new ActionMenuTreeItem("SystemTools.CrossPlatform.ToggleWorkflow", "开关自动化", "\uE051"));

        if (items.Count > 0)
        {
            foreach (var item in items)
            {
                IActionService.ActionMenuTree["SystemTools 行动"]["高级自动化工具…"].Add(item);
            }
        }
    }

    private void BuildAiMenu(MainConfigData config)
    {
        if (config.EnableAiService && config.IsActionEnabled("SystemTools.CrossPlatform.ShowAiChatDialog"))
        {
            IActionService.ActionMenuTree["SystemTools 行动"]["AI 功能…"].Add(
                new ActionMenuTreeItem("SystemTools.CrossPlatform.ShowAiChatDialog", "显示AI对话框", "\uE8C3"));
        }
    }

    private void BuildOtherMenu(MainConfigData config)
    {
        if (config.IsActionEnabled("SystemTools.CrossPlatform.FullscreenClock"))
        {
            IActionService.ActionMenuTree["SystemTools 行动"]["其他工具…"].Add(
                new ActionMenuTreeItem("SystemTools.CrossPlatform.FullscreenClock", "沉浸式时钟", "\uE4D2"));
        }
    }

    private void BuildClassIslandMenu(MainConfigData config)
    {
        var items = new List<ActionMenuTreeItem>();

        if (config.IsActionEnabled("SystemTools.CrossPlatform.ClearAllNotifications"))
            items.Add(new ActionMenuTreeItem("SystemTools.CrossPlatform.ClearAllNotifications", "清除全部提醒", "\uE029"));
        if (config.IsActionEnabled("SystemTools.CrossPlatform.LoadTemporaryClassPlan"))
            items.Add(new ActionMenuTreeItem("SystemTools.CrossPlatform.LoadTemporaryClassPlan", "加载临时课表", "\uE6A1"));
        if (config.IsActionEnabled("SystemTools.CrossPlatform.OpenAppSettings"))
            items.Add(new ActionMenuTreeItem("SystemTools.CrossPlatform.OpenAppSettings", "打开应用设置", "\uEF27"));
        if (config.IsActionEnabled("SystemTools.CrossPlatform.OpenProfileEditor"))
            items.Add(new ActionMenuTreeItem("SystemTools.CrossPlatform.OpenProfileEditor", "打开档案编辑", "\uE699"));
        if (config.IsActionEnabled("SystemTools.CrossPlatform.OpenClassSwapWindow"))
            items.Add(new ActionMenuTreeItem("SystemTools.CrossPlatform.OpenClassSwapWindow", "打开换课窗口", "\uE13B"));

        if (items.Count > 0)
        {
            foreach (var item in items)
            {
                IActionService.ActionMenuTree["SystemTools 行动"]["ClassIsland…"].Add(item);
            }
        }
    }

    #endregion
}