using Avalonia.Controls;
using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Shared;
using SystemTools.CrossPlatform.ConfigHandlers;
using SystemTools.CrossPlatform.Services;
using SystemTools.CrossPlatform.Shared;

namespace SystemTools.CrossPlatform.SettingsPage;

[SettingsPageInfo("SystemTools.CrossPlatform.settings.more", "更多功能选项…", "\uE28E", "\uE28E", true)]
[Group("SystemTools.CrossPlatform.settings")]
public partial class MoreFeaturesOptionsSettingsPage : SettingsPageBase
{
    public MainConfigData Config => GlobalConstants.MainConfig!.Data;

    public MoreFeaturesOptionsSettingsPage()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void VirtualAfterSchoolToggle_OnChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggleSwitch)
        {
            Config.VirtualAfterSchoolEnabled = toggleSwitch.IsChecked == true;
        }

        IAppHost.TryGetService<VirtualAfterSchoolService>()?.ApplyConfig();
        GlobalConstants.MainConfig?.Save();
    }

    private void AutoCleanupMemoryToggle_OnChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggleSwitch)
        {
            Config.AutoCleanupClassIslandMemory = toggleSwitch.IsChecked == true;
        }

        var service = IAppHost.GetService<ClassIslandMemoryAutoCleanupService>();
        service.ApplyConfig();
        GlobalConstants.MainConfig?.Save();
    }

}