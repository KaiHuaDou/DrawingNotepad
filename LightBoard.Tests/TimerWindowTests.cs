using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Threading;

using LightBoard.Tests.Toolbar;

namespace LightBoard.Tests;

/// <summary>倒计时窗口：主题样式与绑定在构造时全部解析，尺寸跟随内容</summary>
[Collection("Toolbar")]
public class TimerWindowTests
{
    [Fact]
    public void Window_LoadsBindings( )
    {
        UiThread.Run(( ) =>
        {
            var window = new TimerWindow( );
            try
            {
                // 首次绑定在 DataBind 优先级处理
                window.Dispatcher.Invoke(( ) => { }, DispatcherPriority.Background);

                Assert.Equal("倒计时", window.Title);

                var timerButton = Assert.IsType<Button>(window.FindName("TimerButton"));
                Assert.False(timerButton.IsEnabled);
                Assert.Equal("\uE768", timerButton.Tag);

                var resetButton = Assert.IsType<Button>(window.FindName("ResetButton"));
                Assert.True(resetButton.IsEnabled);

                var hourUp = Assert.IsType<Button>(window.FindName("HourUpButton"));
                Assert.Equal(Visibility.Visible, hourUp.Visibility);

                var ring = Assert.IsType<Canvas>(window.FindName("RingCanvas"));
                Assert.Equal(Visibility.Visible, ring.Visibility);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void Ring_HiddenInCountup( )
    {
        UiThread.Run(( ) =>
        {
            var window = new TimerWindow( );
            try
            {
                window.Dispatcher.Invoke(( ) => { }, DispatcherPriority.Background);

                var timer = Assert.IsType<TimerViewModel>(window.DataContext);
                var ring = Assert.IsType<Canvas>(window.FindName("RingCanvas"));

                timer.SetMode(TimerMode.Countup);
                window.Dispatcher.Invoke(( ) => { }, DispatcherPriority.Background);

                Assert.Equal(Visibility.Collapsed, ring.Visibility);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void Ring_TakesDialWidth( )
    {
        UiThread.Run(( ) =>
        {
            var window = new TimerWindow( );
            try
            {
                window.Show( );
                window.UpdateLayout( );

                var dial = Assert.IsType<Border>(window.FindName("DialBorder"));
                var ring = Assert.IsType<Canvas>(window.FindName("RingCanvas"));

                Assert.Equal(Math.Ceiling(dial.ActualWidth), ring.Width);
                Assert.Equal(ring.Width, ring.Height);

                // 窗口按内容定尺寸，环不能溢出客户区
                Assert.True(window.ActualWidth > ring.Width);
                Assert.True(window.ActualHeight > ring.Height);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void Ring_EndsAtThreeOClockForFifteenMinutes( )
    {
        UiThread.Run(( ) =>
        {
            var window = new TimerWindow( );
            try
            {
                window.Show( );
                window.UpdateLayout( );

                var timer = Assert.IsType<TimerViewModel>(window.DataContext);
                var ring = Assert.IsType<Canvas>(window.FindName("RingCanvas"));
                var progress = Assert.IsType<Path>(window.FindName("RingProgressPath"));

                timer.SetMinutes(15);

                // 12 点方向起顺时针扫过四分之一圈，止于 3 点
                var bounds = progress.Data.Bounds;
                Assert.Equal(ring.Width / 2, bounds.Left, 1);
                Assert.Equal(ring.Width / 2, bounds.Bottom, 1);
                Assert.True(bounds.Right > ring.Width / 2);
                Assert.True(bounds.Top < ring.Width / 2);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void Ring_FullAtRoundStart( )
    {
        UiThread.Run(( ) =>
        {
            var window = new TimerWindow( );
            try
            {
                window.Show( );
                window.UpdateLayout( );

                var timer = Assert.IsType<TimerViewModel>(window.DataContext);
                var ring = Assert.IsType<Canvas>(window.FindName("RingCanvas"));
                var progress = Assert.IsType<Path>(window.FindName("RingProgressPath"));

                timer.SetMinutes(55);
                timer.Toggle( );

                // 本轮刚开始时剩余量满圈，弧的外接盒只比正方形小一圈描边
                var bounds = progress.Data.Bounds;
                Assert.Equal(ring.Width - progress.StrokeThickness, bounds.Width, 1);
                Assert.Equal(ring.Width - progress.StrokeThickness, bounds.Height, 1);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void Start_ExpandsWindowToFitContent( )
    {
        UiThread.Run(( ) =>
        {
            var window = new TimerWindow( );
            try
            {
                window.Show( );
                Pump(TimeSpan.FromMilliseconds(200));

                var timer = Assert.IsType<TimerViewModel>(window.DataContext);
                var content = Assert.IsType<FrameworkElement>(window.Content, exactMatch: false);
                var before = window.ActualHeight;

                timer.SetMinutes(5);
                timer.Toggle( );
                Pump(TimeSpan.FromMilliseconds(400));

                // 秒位展开后内容变高，窗口跟着长；原本停在最小高度，内容被裁掉
                Assert.True(window.ActualHeight > before);
                Assert.True(window.ActualHeight >= content.DesiredSize.Height);
                Assert.True(window.ActualWidth >= content.DesiredSize.Width);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void Fullscreen_TogglesSizeToContent( )
    {
        UiThread.Run(( ) =>
        {
            var window = new TimerWindow( );
            try
            {
                window.Show( );
                window.UpdateLayout( );

                var size = window.RenderSize;

                var button = Assert.IsType<Button>(window.FindName("FullscreenButton"));
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(TimeSpan.FromMilliseconds(200));

                // 由内容定尺寸的窗口最大化会缩回内容大小，全屏期间必须是手动尺寸
                Assert.Equal(WindowState.Maximized, window.WindowState);
                Assert.Equal(SizeToContent.Manual, window.SizeToContent);
                Assert.True(window.ActualWidth > size.Width);

                window.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                {
                    RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                });
                Pump(TimeSpan.FromMilliseconds(400));

                Assert.Equal(WindowState.Normal, window.WindowState);
                Assert.Equal(SizeToContent.WidthAndHeight, window.SizeToContent);
                Assert.Equal(size.Width, window.ActualWidth, 1);
                Assert.Equal(size.Height, window.ActualHeight, 1);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void Fullscreen_RoundTrip_KeepsRunningSize( )
    {
        UiThread.Run(( ) =>
        {
            var window = new TimerWindow( );
            try
            {
                window.Show( );

                var timer = Assert.IsType<TimerViewModel>(window.DataContext);
                timer.SetMinutes(5);
                timer.Toggle( );
                Pump(TimeSpan.FromMilliseconds(400));

                var running = window.RenderSize;

                var button = Assert.IsType<Button>(window.FindName("FullscreenButton"));
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(TimeSpan.FromMilliseconds(200));

                window.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                {
                    RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                });
                Pump(TimeSpan.FromMilliseconds(400));

                // 全屏期间字号放大、调整按钮与环收起，退出后要一并回到全屏前，否则窗口比进入前大一圈
                Assert.Equal(running.Width, window.ActualWidth, 1);
                Assert.Equal(running.Height, window.ActualHeight, 1);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    private static void Pump(TimeSpan duration)
    {
        var frame = new DispatcherFrame( );
        var stop = new DispatcherTimer(duration, DispatcherPriority.Background, (_, _) => frame.Continue = false, Dispatcher.CurrentDispatcher);
        stop.Start( );
        Dispatcher.PushFrame(frame);
        stop.Stop( );
    }
}
