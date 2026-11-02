using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LightBoard;

public partial class TimerWindow : Window
{
    private const int MinuteStep = 5;
    private const int MinuteSteps = 60 / MinuteStep;
    private const int MaxMinute = (MinuteSteps - 1) * MinuteStep;
    private readonly TimerViewModel viewModel;

    /// <summary>
    /// 把手直径，与条高一致
    /// </summary>
    private double HandleWidth => MinuteSlider.ActualHeight;

    /// <summary>
    /// 把手在条内可移动的宽度
    /// </summary>
    private double UsableWidth => Math.Max(0, MinuteSlider.ActualWidth - HandleWidth);

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

    private void PreviousHourClick(object o, RoutedEventArgs e)
    {
        viewModel.Adjust(TimeSpan.FromHours(-1));
    }

    private void NextHourClick(object o, RoutedEventArgs e)
    {
        viewModel.Adjust(TimeSpan.FromHours(1));
    }

    private void MinuteTickClick(object o, RoutedEventArgs e)
    {
        if (o is Button { Tag: string minutes } && int.TryParse(minutes, out var value))
        {
            viewModel.SetMinutes(value);
        }
    }

    private void MinuteSliderMouseLeftButtonDown(object o, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        MinuteSlider.CaptureMouse( );
        SetMinutesFromPosition(e.GetPosition(MinuteSlider).X);
    }

    private void MinuteSliderMouseMove(object o, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        SetMinutesFromPosition(e.GetPosition(MinuteSlider).X);
    }

    private void MinuteSliderMouseLeftButtonUp(object o, MouseButtonEventArgs e)
    {
        MinuteSlider.ReleaseMouseCapture( );
    }

    private void MinuteSliderSizeChanged(object o, SizeChangedEventArgs e)
    {
        SyncMinuteBar( );
        SyncMinuteTicks( );
    }

    private void WindowMouseLeftButtonDown(object o, MouseButtonEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
        }
    }

    private void DisplayChanged(object? o, EventArgs e)
    {
        SyncMinuteBar( );
        ResizeToContent( );
    }

    private void SetMinutesFromPosition(double x)
    {
        var usable = UsableWidth;
        if (usable <= 0)
        {
            return;
        }

        var ratio = Math.Clamp((x - HandleWidth / 2) / usable, 0, 1);
        viewModel.SetMinutes((int) Math.Round(ratio * (MinuteSteps - 1)) * MinuteStep);
    }

    private void SyncMinuteBar( )
    {
        var ratio = viewModel.ShowsProgress
            ? viewModel.Progress
            : Math.Min(viewModel.Minutes / (double) MaxMinute, 1);

        // 填充末端圆角即把手：把手中心随比例走过可用宽度，落点 0 与 1 都在轨道两端
        MinuteFill.Width = UsableWidth * ratio + HandleWidth;
    }

    private void SyncMinuteTicks( )
    {
        var usable = UsableWidth;

        PositionTick(MinuteFiveButton, 5, usable);
        PositionTick(MinuteTenButton, 10, usable);
        PositionTick(MinuteFifteenButton, 15, usable);
        PositionTick(MinuteThirtyButton, 30, usable);
        PositionTick(MinuteFortyFiveButton, 45, usable);
    }

    private void PositionTick(Button tick, int minutes, double usable)
    {
        var center = HandleWidth / 2 + usable * minutes / MaxMinute;
        Canvas.SetLeft(tick, center - tick.Width / 2);
    }

    protected override void OnClosed(EventArgs e)
    {
        viewModel.DisplayChanged -= DisplayChanged;
        viewModel.Stop( );

        base.OnClosed(e);
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);

        // 状态切换后的尺寸变化直接落到目标值，不播过渡动画
        fullscreenTransition = true;
        ResizeToContent( );
    }
}
