using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia;
using System;
using Avalonia.Threading;
using ClassIsland.Core.Controls;

namespace SystemTools.CrossPlatform.Views;

/// <summary>
/// 延长关机时间对话框
/// </summary>
public partial class ExtendShutdownDialog : MyWindow
{
    public int? ResultMinutes { get; private set; }

    public ExtendShutdownDialog()
    {
        InitializeComponent();
        if (ConfirmButton is not null)
        {
            ConfirmButton.Click += OnConfirmButtonClick;
        }

        if (CancelButton is not null)
        {
            CancelButton.Click += OnCancelButtonClick;
        }

        this.GetPropertyChangedObservable(Window.WindowStateProperty).Subscribe(e =>
        {
            if (this.WindowState == WindowState.Minimized)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    this.WindowState = WindowState.Normal;
                    this.Activate();
                    this.InvalidateVisual();

                    // var pos = this.Position;
                    // this.Position = pos.WithX(pos.X + 1);
                    // this.Position = pos;

                }, DispatcherPriority.MaxValue);
            }
        });
    }

    public NumericUpDown? MinutesInput => this.FindControl<NumericUpDown>("MinutesInputElement");
    public Button? ConfirmButton => this.FindControl<Button>("ConfirmButtonElement");
    public Button? CancelButton => this.FindControl<Button>("CancelButtonElement");

    private void OnConfirmButtonClick(object? sender, RoutedEventArgs e)
    {
        ResultMinutes = (int)(MinutesInput?.Value ?? 1);
        Close();
    }

    private void OnCancelButtonClick(object? sender, RoutedEventArgs e)
    {
        ResultMinutes = null;
        Close();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
