using System;
using System.Windows;
using System.Windows.Media;

namespace LightBoard;

/// <summary>
/// 倒计时窗口的进度环：轨道加剩余量弧，边长取元素自身
/// </summary>
public sealed class TimerDial : FrameworkElement
{
    internal const double RingThickness = 8;

    // 首尾重合时用整圆画，避免两个端点叠在一起加深颜色
    private const double FullCircleDegrees = 359.9;

    private static readonly Pen TrackPen = FrozenPen("#333333");
    private static readonly Pen RingPen = FrozenPen("#D8FFFFFF");
    private static readonly Pen AlertPen = FrozenPen("#FF5252");

    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress),
        typeof(double),
        typeof(TimerDial),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsAlertingProperty = DependencyProperty.Register(
        nameof(IsAlerting),
        typeof(bool),
        typeof(TimerDial),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsPausedProperty = DependencyProperty.Register(
        nameof(IsPaused),
        typeof(bool),
        typeof(TimerDial),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Progress
    {
        get => (double) GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public bool IsAlerting
    {
        get => (bool) GetValue(IsAlertingProperty);
        set => SetValue(IsAlertingProperty, value);
    }

    public bool IsPaused
    {
        get => (bool) GetValue(IsPausedProperty);
        set => SetValue(IsPausedProperty, value);
    }

    internal static int MinutesAt(Vector offset)
    {
        var degrees = Math.Atan2(offset.X, -offset.Y) * 180 / Math.PI;
        if (degrees < 0)
        {
            degrees += 360;
        }

        return (int) Math.Round(degrees / 6) % 60;
    }

    internal static double SweepDegrees(double progress)
    {
        return Math.Clamp(progress, 0, 1) * 360;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var side = Math.Min(RenderSize.Width, RenderSize.Height);
        if (side <= RingThickness)
        {
            return;
        }

        var center = new Point(RenderSize.Width / 2, RenderSize.Height / 2);
        var radius = (side - RingThickness) / 2;

        drawingContext.DrawEllipse(null, TrackPen, center, radius, radius);

        var progress = Math.Clamp(Progress, 0, 1);
        if (progress <= 0)
        {
            return;
        }

        if (IsPaused)
        {
            drawingContext.PushOpacity(0.5);
        }

        var pen = IsAlerting ? AlertPen : RingPen;
        var sweep = SweepDegrees(progress);

        if (sweep >= FullCircleDegrees)
        {
            drawingContext.DrawEllipse(null, pen, center, radius, radius);
        }
        else
        {
            drawingContext.DrawGeometry(null, pen, ArcGeometry(center, radius, sweep));
        }

        if (IsPaused)
        {
            drawingContext.Pop( );
        }
    }

    private static StreamGeometry ArcGeometry(Point center, double radius, double sweep)
    {
        var start = PointAt(center, radius, -90);
        var end = PointAt(center, radius, -90 + sweep);

        var geometry = new StreamGeometry( );

        using (var context = geometry.Open( ))
        {
            context.BeginFigure(start, isFilled: false, isClosed: false);

            // 扫过角度大于半圈时圆弧要走长边，否则 WPF 取劣弧，端点会落在另一侧
            context.ArcTo(
                end,
                new Size(radius, radius),
                0,
                sweep > 180,
                SweepDirection.Clockwise,
                isStroked: true,
                isSmoothJoin: false);
        }

        geometry.Freeze( );
        return geometry;
    }

    private static Point PointAt(Point center, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180;

        return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
    }

    private static Pen FrozenPen(string hex)
    {
        var brush = new SolidColorBrush((Color) ColorConverter.ConvertFromString(hex));
        brush.Freeze( );

        var pen = new Pen(brush, RingThickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
        };
        pen.Freeze( );

        return pen;
    }
}
