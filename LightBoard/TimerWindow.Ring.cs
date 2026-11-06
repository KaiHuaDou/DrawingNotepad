using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LightBoard;

/// <summary>
/// 环绕数字的进度环：0 位在 12 点方向，顺时针增长，一分钟对应钟面 6 度
/// </summary>
public partial class TimerWindow
{
    private const int MinuteStep = 5;

    /// <summary>
    /// 环的外接方边长，等于数字区的宽度
    /// </summary>
    private double ringSide;

    private void DialSizeChanged(object o, SizeChangedEventArgs e)
    {
        SetRingSide(Math.Ceiling(e.NewSize.Width));
    }

    private void ClockMouseLeftButtonDown(object o, MouseButtonEventArgs e)
    {
        if (!viewModel.IsAdjustable || RingCanvas.Visibility != Visibility.Visible || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        ClockGrid.CaptureMouse( );
        SetMinutesFromAngle(e.GetPosition(RingCanvas));
    }

    private void ClockMouseMove(object o, MouseEventArgs e)
    {
        if (ClockGrid.IsMouseCaptured)
        {
            SetMinutesFromAngle(e.GetPosition(RingCanvas));
        }
    }

    private void ClockMouseLeftButtonUp(object o, MouseButtonEventArgs e)
    {
        ClockGrid.ReleaseMouseCapture( );
    }

    private void SetMinutesFromAngle(Point p)
    {
        if (ringSide <= 0)
        {
            return;
        }

        var dx = p.X - ringSide / 2;
        var dy = p.Y - ringSide / 2;

        // 圆外不响应，点角落不改分钟
        if (Math.Sqrt(dx * dx + dy * dy) > ringSide / 2)
        {
            return;
        }

        // 12 点方向为 0，顺时针取角
        var degrees = Math.Atan2(dx, -dy) * 180 / Math.PI;
        if (degrees < 0)
        {
            degrees += 360;
        }

        viewModel.SetMinutes((int) Math.Round(degrees / 30) * MinuteStep % 60);
    }

    /// <summary>
    /// 边长取数字区宽度，数字区的内边距让出描边
    /// </summary>
    private void SetRingSide(double side)
    {
        if (side <= 0 || Math.Abs(side - ringSide) < 0.5)
        {
            return;
        }

        ringSide = side;
        RingCanvas.Width = side;
        RingCanvas.Height = side;
        RingTrackPath.Data = RingCircle( );

        SyncRing( );
    }

    /// <summary>
    /// 本轮内按剩余量收缩，调整态按已设分钟落在钟面位置
    /// </summary>
    private void SyncRing( )
    {
        var ratio = viewModel.ShowsProgress
            ? 1 - viewModel.Progress
            : viewModel.Minutes / 60.0;

        RingProgressPath.Data = RingArc(Math.Clamp(ratio, 0, 1));
    }

    private EllipseGeometry RingCircle( )
    {
        var radius = (ringSide - RingTrackPath.StrokeThickness) / 2;

        return new EllipseGeometry(new Point(ringSide / 2, ringSide / 2), radius, radius);
    }

    private Geometry RingArc(double ratio)
    {
        if (ratio <= 0)
        {
            return Geometry.Empty;
        }

        var center = new Point(ringSide / 2, ringSide / 2);
        var radius = (ringSide - RingTrackPath.StrokeThickness) / 2;
        var sweep = ratio * 360;

        // 整圈时起点与终点重合，拆成两段才画得出来
        var figure = new PathFigure { StartPoint = RingPoint(center, radius, -90) };
        figure.Segments.Add(RingArcSegment(center, radius, -90 + sweep / 2));
        figure.Segments.Add(RingArcSegment(center, radius, -90 + sweep));

        return new PathGeometry { Figures = { figure } };
    }

    private static ArcSegment RingArcSegment(Point center, double radius, double degrees)
    {
        return new ArcSegment(RingPoint(center, radius, degrees), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true);
    }

    private static Point RingPoint(Point center, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180;

        return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
    }
}
