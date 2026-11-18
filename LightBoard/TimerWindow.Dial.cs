using System;
using System.Windows;
using System.Windows.Input;

namespace LightBoard;

public partial class TimerWindow
{
    // 钟面

    private void ClockMouseLeftButtonDown(object o, MouseButtonEventArgs e)
    {
        if (!viewModel.IsAdjustable || Dial.Visibility != Visibility.Visible || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        // 只有按在环带上才开始调整；拖拽过程中按角度继续，不再要求留在环带内
        var position = e.GetPosition(Dial);
        var side = Math.Min(Dial.ActualWidth, Dial.ActualHeight);
        var offset = position - new Point(Dial.ActualWidth / 2, Dial.ActualHeight / 2);
        if (side <= 0 || !TimerDial.IsOnRing(offset, side))
        {
            return;
        }

        ClockGrid.CaptureMouse( );
        SetMinutesAt(position);
    }

    private void ClockMouseMove(object o, MouseEventArgs e)
    {
        if (ClockGrid.IsMouseCaptured)
        {
            SetMinutesAt(e.GetPosition(Dial));
        }
    }

    private void ClockMouseLeftButtonUp(object o, MouseButtonEventArgs e)
    {
        ClockGrid.ReleaseMouseCapture( );
    }

    // 圆外不响应，拖出圆外时分钟停在当前档
    private void SetMinutesAt(Point point)
    {
        var side = Math.Min(Dial.ActualWidth, Dial.ActualHeight);
        if (side <= 0)
        {
            return;
        }

        var offset = point - new Point(Dial.ActualWidth / 2, Dial.ActualHeight / 2);
        if (offset.Length > side / 2)
        {
            return;
        }

        viewModel.SetMinutes(TimerDial.MinutesAt(offset));
    }

    private void SyncDial( )
    {
        Dial.Progress = CurrentProgress( );
    }

    // 未开始时按设置的分钟落在钟面上，本轮内按剩余量收缩
    private double CurrentProgress( )
    {
        return viewModel.ShowsProgress ? 1 - viewModel.Progress : viewModel.Minutes / 60.0;
    }
}
