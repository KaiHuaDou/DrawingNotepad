using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace LightBoard;

internal enum TimerMode
{
    Countdown,
    Countup,
}

public partial class TimerWindow : Window
{
    private const string PlayGlyph = "\uE768";
    private const string PauseGlyph = "\uE769";

    private const double NormalFontSize = 96;
    private const double FullscreenFontSize = 192;

    private static readonly TimeSpan AlertSpan = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MaxSpan = TimeSpan.FromSeconds(99 * 3600 + 59 * 60 + 59);

    private static readonly SolidColorBrush AlertBrush = new(Color.FromRgb(0xFF, 0x52, 0x52));

    private readonly DispatcherTimer timer;
    private long startedAt;

    private TimerMode mode = TimerMode.Countdown;

    /// <summary>
    /// 倒计时的剩余时长
    /// </summary>
    private TimeSpan countdownRemaining;

    /// <summary>
    /// 正计时的累计时长
    /// </summary>
    private TimeSpan countupElapsed;

    static TimerWindow( )
    {
        AlertBrush.Freeze( );
    }

    public TimerWindow( )
    {
        InitializeComponent( );

        timer = new(DispatcherPriority.Normal, Dispatcher.CurrentDispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(100),
        };
        timer.Tick += (_, _) => Refresh( );

        Refresh( );
    }

    private void TimerClick(object o, RoutedEventArgs e)
    {
        if (state == TimerState.Idle)
        {
            if (CanStart( ))
            {
                SetState(TimerState.Running);
            }
        }
        else
        {
            SetState(TimerState.Idle);
        }

        Refresh( );
    }

    private void ResetClick(object o, RoutedEventArgs e)
    {
        if (mode == TimerMode.Countdown)
        {
            countdownRemaining = TimeSpan.Zero;
        }
        else
        {
            countupElapsed = TimeSpan.Zero;
        }

        Refresh( );
    }

    private void ModeClick(object o, RoutedEventArgs e)
    {
        if (o is not RadioButton radio)
        {
            return;
        }

        var newMode = radio == CountdownRadio ? TimerMode.Countdown : TimerMode.Countup;
        if (newMode == mode)
        {
            return;
        }

        mode = newMode;
        Title = mode == TimerMode.Countdown ? "倒计时" : "计时器";
        Refresh( );
    }

    private void HourClick(object o, RoutedEventArgs e)
    {
        if (o is not Button button)
        {
            return;
        }

        Adjust(ReferenceEquals(button, HourUpButton) ? TimeSpan.FromHours(1) : TimeSpan.FromHours(-1));
    }

    private void MinuteClick(object o, RoutedEventArgs e)
    {
        if (o is not Button button)
        {
            return;
        }

        Adjust(ReferenceEquals(button, MinuteUpButton) ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(-1));
    }

    private void PresetClick(object o, RoutedEventArgs e)
    {
        if (o is not Button button || button.Tag is not string minutes || !int.TryParse(minutes, out var value))
        {
            return;
        }

        countdownRemaining = TimeSpan.FromMinutes(value);
        Refresh( );
    }

    private void WindowMouseLeftButtonDown(object o, MouseButtonEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
        }
    }

    /// <summary>当前显示的时长：倒计时为剩余，正计时为累计。</summary>
    private TimeSpan DisplayValue( )
    {
        var used = Elapsed( );

        return mode == TimerMode.Countdown
            ? Clamp(countdownRemaining - used, TimeSpan.Zero, MaxSpan)
            : Clamp(countupElapsed + used, TimeSpan.Zero, MaxSpan);
    }

    /// <summary>本轮已运行的时长；取单调时钟，不受系统时间调整影响。</summary>
    private TimeSpan Elapsed( )
    {
        return state == TimerState.Idle ? TimeSpan.Zero : Stopwatch.GetElapsedTime(startedAt);
    }

    private bool CanStart( )
    {
        return mode == TimerMode.Countup ? countupElapsed < MaxSpan : countdownRemaining > TimeSpan.Zero;
    }

    /// <summary>调整当前模式的数值：倒计时调剩余量，正计时调累计量。</summary>
    private void Adjust(TimeSpan delta)
    {
        if (state != TimerState.Idle)
        {
            return;
        }

        if (mode == TimerMode.Countdown)
        {
            countdownRemaining = Clamp(countdownRemaining + delta, TimeSpan.Zero, MaxSpan);
        }
        else
        {
            countupElapsed = Clamp(countupElapsed + delta, TimeSpan.Zero, MaxSpan);
        }

        Refresh( );
    }

    private void Refresh( )
    {
        UpdateState( );

        var totalSeconds = (int) DisplayValue( ).TotalSeconds;

        UpdateControls(totalSeconds);
        ShowValue(totalSeconds);
        ResizeToContent( );
    }

    /// <summary>按状态、模式与显示值刷新控件的可见性、可用性与外观；秒区显隐由 ResizeToContent 负责</summary>
    private void UpdateControls(int totalSeconds)
    {
        var idle = state == TimerState.Idle;
        var fullscreen = WindowState == WindowState.Maximized;

        TimerButton.Tag = idle ? PlayGlyph : PauseGlyph;

        var fontSize = fullscreen ? FullscreenFontSize : NormalFontSize;

        HourText.FontSize = fontSize;
        MinuteText.FontSize = fontSize;
        Separator1Text.FontSize = fontSize;
        Separator2Text.FontSize = fontSize;
        SecondText.FontSize = fontSize;

        var adjustable = mode == TimerMode.Countdown && idle && !fullscreen;

        // 非全屏时调整区隐藏也占位，开始与暂停不改变窗口尺寸；全屏窗口尺寸固定，收起让数字垂直居中
        var adjustVisibility = adjustable
            ? Visibility.Visible
            : fullscreen ? Visibility.Collapsed : Visibility.Hidden;

        HourUpButton.Visibility = adjustVisibility;
        HourDownButton.Visibility = adjustVisibility;
        MinuteUpButton.Visibility = adjustVisibility;
        MinuteDownButton.Visibility = adjustVisibility;
        PresetRow.Visibility = adjustVisibility;

        var frameVisibility = fullscreen ? Visibility.Collapsed : Visibility.Visible;

        ModeRow.Visibility = frameVisibility;
        TimerButton.Visibility = frameVisibility;

        HourDownButton.IsEnabled = totalSeconds >= 3600;
        MinuteDownButton.IsEnabled = totalSeconds % 3600 >= 60;
        CountdownRadio.IsEnabled = idle;
        CountupRadio.IsEnabled = idle;
        ResetButton.IsEnabled = idle;
        TimerButton.IsEnabled = !idle || CanStart( );
    }

    private void ShowValue(int totalSeconds)
    {
        HourText.Text = (totalSeconds / 3600).ToString("00");
        MinuteText.Text = (totalSeconds % 3600 / 60).ToString("00");
        SecondText.Text = (totalSeconds % 60).ToString("00");
    }

    private void ApplyAlertForeground( )
    {
        HourText.Foreground = AlertBrush;
        MinuteText.Foreground = AlertBrush;
        Separator1Text.Foreground = AlertBrush;
        Separator2Text.Foreground = AlertBrush;
        SecondText.Foreground = AlertBrush;
    }

    /// <summary>清除本地值以恢复从 Window 继承的配色</summary>
    private void RestoreForeground( )
    {
        HourText.ClearValue(TextBlock.ForegroundProperty);
        MinuteText.ClearValue(TextBlock.ForegroundProperty);
        Separator1Text.ClearValue(TextBlock.ForegroundProperty);
        Separator2Text.ClearValue(TextBlock.ForegroundProperty);
        SecondText.ClearValue(TextBlock.ForegroundProperty);
    }

    private static TimeSpan Clamp(TimeSpan value, TimeSpan min, TimeSpan max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        timer.Stop( );
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);

        // 状态切换后的尺寸变化直接落到目标值，不播过渡动画
        fullscreenTransition = true;
        Refresh( );
    }
}
