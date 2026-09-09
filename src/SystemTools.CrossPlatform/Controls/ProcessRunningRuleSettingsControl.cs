using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ClassIsland.Core.Abstractions.Controls;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using SystemTools.CrossPlatform.Rules;

namespace SystemTools.CrossPlatform.Controls;

/// <summary>
/// 程序正在运行规则设置控件
/// </summary>
public class ProcessRunningRuleSettingsControl : RuleSettingsControlBase<ProcessRunningRuleSettings>
{
    private readonly TextBox _processNameBox;

    public ProcessRunningRuleSettingsControl()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new(10) };

        panel.Children.Add(new TextBlock
        {
            Text = "进程名:",
            FontWeight = FontWeight.Bold,
            Margin = new(0, 5, 0, 0)
        });

        _processNameBox = new TextBox
        {
            PlaceholderText = "输入进程名（如: notepad）"
        };
        _processNameBox.TextChanged += (_, _) => Settings.ProcessName = _processNameBox.Text ?? string.Empty;
        panel.Children.Add(_processNameBox);

        var viewProcessesButton = new Button
        {
            Content = "查看正在运行的进程",
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new(0, 10, 0, 0)
        };
        viewProcessesButton.Click += async (_, _) => await ShowProcessList();
        panel.Children.Add(viewProcessesButton);

        Content = panel;
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _processNameBox.Text = Settings.ProcessName;
    }

    private async Task ShowProcessList()
    {
        try
        {
            var output = string.Join(Environment.NewLine, Process.GetProcesses()
                .OrderBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase)
                .Select(p => $"{p.ProcessName,-40}{p.Id,10}"));
            await ShowProcessListWindow(output);
        }
        catch (Exception ex)
        {
            await ShowErrorDialog("获取进程列表失败", ex.Message);
        }
    }

    private async Task ShowErrorDialog(string title, string message)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
        {
            return;
        }

        var window = new Window
        {
            Title = title,
            Width = 400,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Spacing = 10,
                Margin = new(10),
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new Button
                    {
                        Content = "确定",
                        Width = 100,
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                }
            }
        };
        await window.ShowDialog((Window)topLevel);
    }

    private async Task ShowProcessListWindow(string processList)
    {
        var window = new Window
        {
            Title = "正在运行的进程",
            Width = 900,
            Height = 600,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var textBox = new TextBox
        {
            Text = processList,
            IsReadOnly = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Consolas, monospace"),
            FontSize = 12
        };

        ScrollViewer.SetVerticalScrollBarVisibility(textBox, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(textBox, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);

        var copyButton = new Button
        {
            Content = "复制全部",
            Width = 100,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new(10)
        };
        copyButton.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is { } topLevel && topLevel.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(processList);
            }
        };

        var dockPanel = new DockPanel();
        DockPanel.SetDock(copyButton, Dock.Top);
        dockPanel.Children.Add(copyButton);
        dockPanel.Children.Add(textBox);

        window.Content = dockPanel;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            await window.ShowDialog((Window)topLevel);
        }
    }
}
