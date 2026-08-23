using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

using static InkCanvasNext.Geometry;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    private readonly ScaleTransform canvasScaleTransform = new(1.0, 1.0);

    private readonly double distanceThreshold2;

    private Point panPoint0;
    private Point viewportOrigin;

    private double distance0;

    private double currentScale = 1.0;
    private double initialScale = 1.0;

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        viewportOrigin = CanvasScroll.TranslatePoint(new Point(0, 0), this);
    }

    private void InitGesture( )
    {
        (var first, var second) = GetMajorTouches( );
        if (first is null)
        {
            return;
        }

        panPoint0 = first.Value;
        distance0 = second is null ? 0 : Distance(first.Value, second.Value);
        initialScale = currentScale;
    }

    private void PanZoom( )
    {
        (var first, var second) = GetMajorTouches( );
        var distance = second is null ? 0 : Distance(first!.Value, second.Value);
        var ratio = distance0 > 0 && distance > 0
            ? distance / distance0
            : 1.0;

        ratio = Smooth(ratio);

        var targetScale = Math.Clamp(initialScale * ratio, 0.1, 10.0);

        canvasScaleTransform.ScaleX = canvasScaleTransform.ScaleY = targetScale;
        eraser.Scale = targetScale;

        var scale = targetScale / currentScale;

        var newOffsetX = CanvasScroll.HorizontalOffset * scale
            + (panPoint0.X - viewportOrigin.X) * scale
            - (first!.Value.X - viewportOrigin.X);
        var newOffsetY = CanvasScroll.VerticalOffset * scale
            + (panPoint0.Y - viewportOrigin.Y) * scale
            - (first!.Value.Y - viewportOrigin.Y);

        CanvasScroll.ScrollToHorizontalOffset(Math.Clamp(newOffsetX, 0, CanvasScroll.ScrollableWidth));
        CanvasScroll.ScrollToVerticalOffset(Math.Clamp(newOffsetY, 0, CanvasScroll.ScrollableHeight));

        currentScale = targetScale;
        panPoint0 = first ?? default;
    }

    private void Pan( )
    {
        (var first, _) = GetMajorTouches( );

        var newOffsetX = CanvasScroll.HorizontalOffset + panPoint0.X - first!.Value.X;
        var newOffsetY = CanvasScroll.VerticalOffset + panPoint0.Y - first!.Value.Y;

        CanvasScroll.ScrollToHorizontalOffset(Math.Clamp(newOffsetX, 0, CanvasScroll.ScrollableWidth));
        CanvasScroll.ScrollToVerticalOffset(Math.Clamp(newOffsetY, 0, CanvasScroll.ScrollableHeight));

        panPoint0 = first!.Value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private (Point? First, Point? Second) GetMajorTouches( )
    {
        var values = touches.Values;
        var count = values.Count;

        if (count == 0)
        {
            return (null, null);
        }

        using var enumerator = values.GetEnumerator( );
        enumerator.MoveNext( );
        var first = enumerator.Current.Position;

        if (count == 1)
        {
            return (first, null);
        }

        enumerator.MoveNext( );
        var second = enumerator.Current.Position;

        return (first, second);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private double Smooth(double x)
    {
        const double T = 0.2;
        const double Q = 0.5;
        const double S = 1;

        var u = x - 1;
        var d = Math.Abs(u);

        if (d <= T)
        {
            return 1;
        }

        if (d >= Q)
        {
            return x;
        }

        var r = (d - T) / (Q - T);

        return 1 + u * r switch
        {
            <= 0 => 0,
            >= 1 => 1,
            _ => SmoothStep(r, S)
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double SmoothStep(double r, double s)
    {
        var rp = Math.Pow(r, s);
        var np = Math.Pow(1.0 - r, s);

        return rp / (rp + np);
    }
}
