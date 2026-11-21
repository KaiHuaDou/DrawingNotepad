using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

using LightBoard.Tests.Toolbar;

namespace LightBoard.Tests;

/// <summary>倒计时窗口：绑定在构造时全部解析，窗口尺寸不随开始与暂停变化</summary>
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

                var dial = Assert.IsType<TimerDial>(window.FindName("Dial"));
                Assert.Equal(Visibility.Visible, dial.Visibility);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void Dial_HiddenInCountup( )
    {
        UiThread.Run(( ) =>
        {
            var window = new TimerWindow( );
            try
            {
                window.Dispatcher.Invoke(( ) => { }, DispatcherPriority.Background);

                var timer = Assert.IsType<TimerViewModel>(window.DataContext);
                var dial = Assert.IsType<TimerDial>(window.FindName("Dial"));

                timer.Mode = TimerMode.Countup;
                window.Dispatcher.Invoke(( ) => { }, DispatcherPriority.Background);

                Assert.Equal(Visibility.Collapsed, dial.Visibility);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void Dial_TakesDigitWidth( )
    {
        UiThread.Run(( ) =>
        {
            var window = new TimerWindow( );
            try
            {
                window.Show( );
                window.UpdateLayout( );

                var frame = Assert.IsType<Border>(window.FindName("DialBorder"));
                var dial = Assert.IsType<TimerDial>(window.FindName("Dial"));

                Assert.Equal(frame.ActualWidth, dial.ActualWidth, 1);
                Assert.Equal(dial.ActualWidth, dial.ActualHeight, 1);

                // 窗口按内容定尺寸，环不能溢出客户区
                Assert.True(window.ActualWidth > dial.ActualWidth);
                Assert.True(window.ActualHeight > dial.ActualHeight);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void Dial_ProgressFollowsMinutes( )
    {
        UiThread.Run(( ) =>
        {
            var window = new TimerWindow( );
            try
            {
                window.Show( );
                window.UpdateLayout( );

                var timer = Assert.IsType<TimerViewModel>(window.DataContext);
                var dial = Assert.IsType<TimerDial>(window.FindName("Dial"));

                timer.Minutes = 15;

                Assert.Equal(0.25, dial.Progress, 3);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void Dial_FullAtRoundStart( )
    {
        UiThread.Run(( ) =>
        {
            var window = new TimerWindow( );
            try
            {
                window.Show( );
                window.UpdateLayout( );

                var timer = Assert.IsType<TimerViewModel>(window.DataContext);
                var dial = Assert.IsType<TimerDial>(window.FindName("Dial"));

                timer.Minutes = 55;
                timer.Toggle( );

                // 本轮刚开始时剩余量满圈
                Assert.Equal(1.0, dial.Progress, 3);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void RoundStateChanges_KeepWindowSize( )
    {
        UiThread.Run(( ) =>
        {
            var window = new TimerWindow( );
            try
            {
                window.Show( );
                UiThread.Pump(TimeSpan.FromMilliseconds(200));

                var timer = Assert.IsType<TimerViewModel>(window.DataContext);
                var idle = window.RenderSize;

                timer.Minutes = 5;
                timer.Toggle( );
                UiThread.Pump(TimeSpan.FromMilliseconds(400));

                // 秒位不参与宽度：两个状态各自居中，开始计时不改变窗口尺寸
                Assert.Equal(idle.Width, window.RenderSize.Width, 1);
                Assert.Equal(idle.Height, window.RenderSize.Height, 1);

                var shortLayer = Assert.IsType<Grid>(window.FindName("ShortLayer"));
                var fullLayer = Assert.IsType<StackPanel>(window.FindName("FullLayer"));
                Assert.Equal(Visibility.Hidden, shortLayer.Visibility);
                Assert.Equal(Visibility.Visible, fullLayer.Visibility);

                timer.Toggle( );
                UiThread.Pump(TimeSpan.FromMilliseconds(200));

                var paused = window.RenderSize;

                timer.Reset( );
                UiThread.Pump(TimeSpan.FromMilliseconds(200));

                Assert.True(timer.IsIdle);
                Assert.Equal(paused.Width, window.RenderSize.Width, 1);
                Assert.Equal(paused.Height, window.RenderSize.Height, 1);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void Paused_DimsDial( )
    {
        UiThread.Run(( ) =>
        {
            var window = new TimerWindow( );
            try
            {
                window.Show( );
                window.UpdateLayout( );

                var timer = Assert.IsType<TimerViewModel>(window.DataContext);
                var dial = Assert.IsType<TimerDial>(window.FindName("Dial"));

                timer.Minutes = 5;
                timer.Toggle( );
                timer.Toggle( );
                window.Dispatcher.Invoke(( ) => { }, DispatcherPriority.Background);

                Assert.True(timer.IsPaused);
                Assert.True(dial.IsPaused);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void Fullscreen_BackgroundClickRestores( )
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
                WaitFor(( ) => window.WindowState == WindowState.Maximized, TimeSpan.FromSeconds(2));

                // 由内容定尺寸的窗口最大化会缩回内容大小，全屏期间必须是手动尺寸
                Assert.Equal(WindowState.Maximized, window.WindowState);
                Assert.Equal(SizeToContent.Manual, window.SizeToContent);
                Assert.True(window.ActualWidth > size.Width);

                Click(window);
                WaitFor(( ) => window.WindowState == WindowState.Normal, TimeSpan.FromSeconds(2));
                UiThread.Pump(TimeSpan.FromMilliseconds(400));

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
    public void Key_SpaceTogglesRound( )
    {
        UiThread.Run(( ) =>
        {
            var window = new TimerWindow( );
            try
            {
                window.Show( );
                window.UpdateLayout( );

                var timer = Assert.IsType<TimerViewModel>(window.DataContext);
                timer.Minutes = 5;

                Press(window, Key.Space);
                Assert.True(timer.IsActive);

                Press(window, Key.Space);
                Assert.True(timer.IsPaused);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void Key_ArrowsAdjustMinutes( )
    {
        UiThread.Run(( ) =>
        {
            var window = new TimerWindow( );
            try
            {
                window.Show( );
                window.UpdateLayout( );

                var timer = Assert.IsType<TimerViewModel>(window.DataContext);
                timer.Minutes = 10;

                Press(window, Key.Right);
                Assert.Equal(15, timer.Minutes);

                Press(window, Key.Left);
                Assert.Equal(10, timer.Minutes);

                Press(window, Key.Up);
                Assert.Equal(11, timer.Minutes);

                Press(window, Key.Down);
                Assert.Equal(10, timer.Minutes);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void Key_ResetsPausedRound( )
    {
        UiThread.Run(( ) =>
        {
            var window = new TimerWindow( );
            try
            {
                window.Show( );
                window.UpdateLayout( );

                var timer = Assert.IsType<TimerViewModel>(window.DataContext);
                timer.Minutes = 5;

                Press(window, Key.Space);
                Press(window, Key.Space);
                Press(window, Key.R);

                Assert.True(timer.IsIdle);
                Assert.Equal(5, timer.Minutes);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void Fullscreen_EscapeRestores( )
    {
        UiThread.Run(( ) =>
        {
            var window = new TimerWindow( );
            try
            {
                window.Show( );
                window.UpdateLayout( );

                var button = Assert.IsType<Button>(window.FindName("FullscreenButton"));
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                WaitFor(( ) => window.WindowState == WindowState.Maximized, TimeSpan.FromSeconds(2));
                Assert.Equal(WindowState.Maximized, window.WindowState);

                Press(window, Key.Escape);
                WaitFor(( ) => window.WindowState == WindowState.Normal, TimeSpan.FromSeconds(2));

                Assert.Equal(WindowState.Normal, window.WindowState);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void Fullscreen_SpaceStartsRound( )
    {
        UiThread.Run(( ) =>
        {
            var window = new TimerWindow( );
            try
            {
                window.Show( );
                window.UpdateLayout( );

                var timer = Assert.IsType<TimerViewModel>(window.DataContext);
                var radio = Assert.IsType<RadioButton>(window.FindName("CountdownRadio"));
                radio.Focus( );
                Assert.True(radio.IsKeyboardFocused);

                timer.Minutes = 5;

                var button = Assert.IsType<Button>(window.FindName("FullscreenButton"));
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                WaitFor(( ) => window.WindowState == WindowState.Maximized, TimeSpan.FromSeconds(2));

                // 焦点仍停在已折叠的单选框上，空格要落到窗口
                Press(window, Key.Space);

                Assert.True(timer.IsActive);
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
                timer.Minutes = 5;
                timer.Toggle( );
                UiThread.Pump(TimeSpan.FromMilliseconds(400));

                var running = window.RenderSize;

                var button = Assert.IsType<Button>(window.FindName("FullscreenButton"));
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                WaitFor(( ) => window.WindowState == WindowState.Maximized, TimeSpan.FromSeconds(2));

                Click(window);
                WaitFor(( ) => window.WindowState == WindowState.Normal, TimeSpan.FromSeconds(2));
                UiThread.Pump(TimeSpan.FromMilliseconds(400));

                // 全屏期间字号放大、调整按钮与环收起，退出后要一并回到全屏前
                Assert.Equal(running.Width, window.ActualWidth, 1);
                Assert.Equal(running.Height, window.ActualHeight, 1);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    private static void Click(Window window)
    {
        window.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
        {
            RoutedEvent = UIElement.MouseLeftButtonDownEvent,
        });
    }

    // 窗口状态由系统消息循环落地，固定时长会漏拍，按条件推进
    private static void WaitFor(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = Environment.TickCount64 + (long) timeout.TotalMilliseconds;

        while (!condition( ) && Environment.TickCount64 < deadline)
        {
            UiThread.Pump(TimeSpan.FromMilliseconds(20));
        }
    }

    private static void Press(Window window, Key key)
    {
        window.RaiseEvent(new KeyEventArgs(
            Keyboard.PrimaryDevice,
            PresentationSource.FromVisual(window),
            0,
            key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        });
    }
}
