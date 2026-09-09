using Avalonia.Controls;
using ClassIsland.Core.Abstractions.Controls;
using SystemTools.CrossPlatform.Settings;

namespace SystemTools.CrossPlatform.Controls;

/// <summary>
/// 计时关机设置控件
/// </summary>
public class ShutdownSettingsControl : ActionSettingsControlBase<ShutdownSettings>
{
    private NumericUpDown _secondsInput;

    public ShutdownSettingsControl()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new(10) };

        panel.Children.Add(new TextBlock
        {
            Text = "计时关机设置",
            FontWeight = Avalonia.Media.FontWeight.Bold,
            FontSize = 14
        });

        var secondsPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 5
        };

        secondsPanel.Children.Add(new TextBlock
        {
            Text = "关机倒计时（秒）:",
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        });

        _secondsInput = new NumericUpDown
        {
            Width = 100,
            Minimum = 0,
            Maximum = 86400,
            Increment = 10
        };

        _secondsInput.ValueChanged += (s, e) => { Settings.Seconds = (int)(_secondsInput.Value ?? 60); };

        secondsPanel.Children.Add(_secondsInput);
        panel.Children.Add(secondsPanel);

        Content = panel;
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _secondsInput.Value = Settings.Seconds;
    }
}
