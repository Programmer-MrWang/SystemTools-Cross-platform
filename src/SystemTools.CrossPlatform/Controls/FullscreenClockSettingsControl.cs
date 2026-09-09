using Avalonia.Controls;
using Avalonia.Media;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Shared;
using ClassIsland.Platforms.Abstraction.Services;
using SystemTools.CrossPlatform.Settings;

namespace SystemTools.CrossPlatform.Controls;

public class FullscreenClockSettingsControl : ActionSettingsControlBase<FullscreenClockSettings>
{
    public FullscreenClockSettingsControl()
    {
        var panel = new StackPanel { Spacing = 8, Margin = new(10) };

        panel.Children.Add(new TextBlock
        {
            Text = "沉浸式时钟",
            FontWeight = FontWeight.Bold,
            FontSize = 14
        });

        panel.Children.Add(new TextBlock
        {
            Text = "本服务由 QQHKX 提供。您需要联网。",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray
        });

        var linkText = new TextBlock
        {
            Text = "该项目仓库：https://github.com/QQHKX/immersive-clock",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Blue,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };

        linkText.PointerPressed += async (s, e) =>
        {
            try
            {
                var launcher = IAppHost.GetService<ILauncherService>();
                if (launcher != null)
                {
                    await launcher.LaunchUrl("https://github.com/QQHKX/immersive-clock");
                }
            }
            catch
            {
                // 打开外部链接失败不影响设置界面
            }
        };

        panel.Children.Add(linkText);

        Content = panel;
    }
}
