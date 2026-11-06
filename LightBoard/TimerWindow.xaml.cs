using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace LightBoard;

public partial class TimerWindow : Window
{
    private readonly TimerViewModel viewModel;

    /// <summary>
    /// 上次重排时的布局条件，null 表示尚未算过
    /// </summary>
    private (bool Idle, bool Adjustable, bool Countdown)? layoutKey;

    public TimerWindow( )
    {
        InitializeComponent( );

        viewModel = (TimerViewModel) DataContext;
        viewModel.DisplayChanged += DisplayChanged;
    }

    private void TimerClick(object o, RoutedEventArgs e)
    {
        viewModel.Toggle( );
    }

    private void ResetClick(object o, RoutedEventArgs e)
    {
        viewModel.Reset( );
    }

    private void ModeClick(object o, RoutedEventArgs e)
    {
        if (o is not RadioButton radio)
        {
            return;
        }

        viewModel.SetMode(radio == CountdownRadio ? TimerMode.Countdown : TimerMode.Countup);
    }

    private void HourClick(object o, RoutedEventArgs e)
    {
        if (o is not Button button)
        {
            return;
        }

        viewModel.Adjust(ReferenceEquals(button, HourUpButton) ? TimeSpan.FromHours(1) : TimeSpan.FromHours(-1));
    }

    private void MinuteClick(object o, RoutedEventArgs e)
    {
        if (o is not Button button)
        {
            return;
        }

        viewModel.Adjust(ReferenceEquals(button, MinuteUpButton) ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(-1));
    }

    private void FullscreenClick(object o, RoutedEventArgs e)
    {
        // 由内容定尺寸的窗口最大化会缩回内容大小，全屏期间改回手动
        SizeToContent = SizeToContent.Manual;
        WindowState = WindowState.Maximized;
    }

    private void WindowMouseLeftButtonDown(object o, MouseButtonEventArgs e)
    {
        if (WindowState != WindowState.Maximized)
        {
            return;
        }

        WindowState = WindowState.Normal;

        // 字号跟窗口状态走样式触发器，绑定更新排在 DataBind；立刻按内容定尺寸会量到全屏字号
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(( ) =>
        {
            if (WindowState == WindowState.Normal)
            {
                SizeToContent = SizeToContent.WidthAndHeight;
            }
        }));
    }

    private void DisplayChanged(object? o, EventArgs e)
    {
        SyncRing( );

        // 窗口尺寸只随布局条件与窗口状态变化，tick 不触发重排
        var layout = (viewModel.IsIdle, viewModel.IsAdjustable, viewModel.IsCountdown);
        if (layout == layoutKey)
        {
            return;
        }

        layoutKey = layout;
        ResizeToContent( );
    }

    protected override void OnClosed(EventArgs e)
    {
        viewModel.DisplayChanged -= DisplayChanged;
        viewModel.Stop( );

        base.OnClosed(e);
    }
}
