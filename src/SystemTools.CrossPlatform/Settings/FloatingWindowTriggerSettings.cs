using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ClassIsland.Core.Abstractions.Controls;
using SystemTools.CrossPlatform.Config;
using SystemTools.CrossPlatform.Controls;
using SystemTools.CrossPlatform.Services;

namespace SystemTools.CrossPlatform.Settings;

public class FloatingWindowTriggerSettings : TriggerSettingsControlBase<FloatingWindowTriggerConfig>
{
    private readonly IconExpressionEditor _iconEditor = new();
    private readonly TextBox _nameTextBox;

    public FloatingWindowTriggerSettings()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new Thickness(10) };

        var iconRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8
        };
        iconRow.Children.Add(new TextBlock
        {
            Text = "按钮图标",
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        });
        _iconEditor.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_iconEditor, 1);
        iconRow.Children.Add(_iconEditor);
        panel.Children.Add(iconRow);

        panel.Children.Add(new TextBlock
        {
            Text = "悬浮窗按钮名称",
            TextWrapping = TextWrapping.Wrap
        });

        _nameTextBox = new TextBox { PlaceholderText = "例如：按钮1" };
        _nameTextBox.TextChanged += (_, _) => { Settings.ButtonName = _nameTextBox.Text ?? string.Empty; };
        panel.Children.Add(_nameTextBox);

        panel.Children.Add(new TextBlock
        {
            Text = "您可在 SystemTools 悬浮窗编辑页面中进一步设置悬浮窗样式。\n若勾选“启用恢复”则可通过再次点按按钮实现恢复。\n在按钮状态为“恢复”时，右键按钮可退出恢复状态且不触发恢复。",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray
        });

        Content = panel;
        _iconEditor.PropertyChanged += OnIconEditorPropertyChanged;
    }

    private void OnIconEditorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != IconExpressionEditor.IconExpressionProperty)
        {
            return;
        }

        var value = _iconEditor.IconExpression ?? string.Empty;
        if (string.Equals(value, Settings.Icon, StringComparison.Ordinal))
        {
            return;
        }

        Settings.Icon = value;
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _nameTextBox.Text = Settings.ButtonName;
        _iconEditor.IconExpression = FloatingWindowIconProvider.NormalizeForStorage(Settings.Icon);
    }
}
