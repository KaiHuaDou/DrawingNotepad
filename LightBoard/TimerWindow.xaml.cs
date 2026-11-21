using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace LightBoard;

public partial class TimerWindow : Window
{
    private readonly TimerViewModel viewModel;

    private bool frameSubscribed;

    public TimerWindow( )
    {
        InitializeComponent( );

        viewModel = (TimerViewModel) DataContext;
        viewModel.DisplayChanged += DisplayChanged;
        viewModel.Finished += TimerFinished;
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

        viewModel.Mode = radio == CountdownRadio ? TimerMode.Countdown : TimerMode.Countup;
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
        EnterFullscreen( );
    }

    private void WindowMouseLeftButtonDown(object o, MouseButtonEventArgs e)
    {
        if (WindowState != WindowState.Maximized)
        {
            return;
        }

        ExitFullscreen( );
    }

    // 焦点所在的按钮先处理空格与方向键；全屏时按钮全部折叠，按键回到窗口
    private void WindowKeyDown(object o, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is ButtonBase { IsVisible: true })
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Space:
                viewModel.Toggle( );
                break;
            case Key.R:
                viewModel.Reset( );
                break;
            case Key.F:
                if (WindowState == WindowState.Maximized)
                {
                    ExitFullscreen( );
                }
                else
                {
                    EnterFullscreen( );
                }

                break;
            case Key.Escape when WindowState == WindowState.Maximized:
                ExitFullscreen( );
                break;
            case Key.Up:
                viewModel.Adjust(TimeSpan.FromMinutes(1));
                break;
            case Key.Down:
                viewModel.Adjust(TimeSpan.FromMinutes(-1));
                break;
            case Key.Right:
                viewModel.Adjust(TimeSpan.FromMinutes(5));
                break;
            case Key.Left:
                viewModel.Adjust(TimeSpan.FromMinutes(-5));
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void DisplayChanged(object? o, EventArgs e)
    {
        SyncDial( );
        SyncFrameClock( );
    }

    private void TimerFinished(object? o, EventArgs e)
    {
        // 归零后进度弧已收尽，用三次明暗提示本轮结束；动画结束回到基础值，不残留状态
        var blink = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(600),
            FillBehavior = FillBehavior.Stop,
        };

        for (var i = 0; i < 3; i++)
        {
            var start = TimeSpan.FromMilliseconds(i * 200);
            blink.KeyFrames.Add(new LinearDoubleKeyFrame(1, start));
            blink.KeyFrames.Add(new LinearDoubleKeyFrame(0.25, start + TimeSpan.FromMilliseconds(100)));
            blink.KeyFrames.Add(new LinearDoubleKeyFrame(1, start + TimeSpan.FromMilliseconds(200)));
        }

        ClockGrid.BeginAnimation(OpacityProperty, blink, HandoffBehavior.SnapshotAndReplace);
    }

    private void FrameTick(object? o, EventArgs e)
    {
        Dial.Progress = CurrentProgress( );
    }

    // 推进期间按渲染帧刷新，暂停、隐藏与关闭时退订
    private void SyncFrameClock( )
    {
        var active = viewModel.IsActive && IsVisible;
        if (active == frameSubscribed)
        {
            return;
        }

        if (active)
        {
            CompositionTarget.Rendering += FrameTick;
        }
        else
        {
            CompositionTarget.Rendering -= FrameTick;
        }

        frameSubscribed = active;
    }

    private void EnterFullscreen( )
    {
        // 由内容定尺寸的窗口最大化会缩回内容大小，全屏期间改回手动
        SizeToContent = SizeToContent.Manual;
        WindowState = WindowState.Maximized;
    }

    private void ExitFullscreen( )
    {
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

    protected override void OnClosed(EventArgs e)
    {
        viewModel.DisplayChanged -= DisplayChanged;
        viewModel.Finished -= TimerFinished;
        viewModel.Stop( );

        if (frameSubscribed)
        {
            CompositionTarget.Rendering -= FrameTick;
            frameSubscribed = false;
        }

        base.OnClosed(e);
    }
}
