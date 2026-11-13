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

        ClockGrid.CaptureMouse( );
        SetMinutesAt(e.GetPosition(Dial));
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

    // 圆外不响应，点角落不改分钟
    private void SetMinutesAt(Point point)
    {
        var side = Dial.ActualWidth;
        if (side <= 0)
        {
            return;
        }

        var offset = point - new Point(side / 2, Dial.ActualHeight / 2);
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
