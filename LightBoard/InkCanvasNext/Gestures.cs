using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

using static InkCanvasNext.Geometry;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    private readonly ScaleTransform canvasScaleTransform = new(1.0, 1.0);

    private readonly double distanceThreshold2;

    private const double PanZoomDisplaceThreshold2 = 30.0 * 30.0;
    private const double PinchMinDistance2 = 24.0 * 24.0;

    private Point panPoint0;
    private Point viewportOrigin;
    private Point panZoomAnchor;
    private bool zoomLocked;

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
        panZoomAnchor = second is null ? first.Value : MidPoint(first.Value, second.Value);
        zoomLocked = false;
    }

    private void PanZoom( )
    {
        (var first, var second) = GetMajorTouches( );
        var firstPoint = first!.Value;

        var span2 = second is null ? 0.0 : Distance2(firstPoint, second.Value);
        var displ2 = second is null ? 0.0 : Distance2(MidPoint(firstPoint, second.Value), panZoomAnchor);

        if (!zoomLocked && (displ2 > PanZoomDisplaceThreshold2 || span2 < PinchMinDistance2))
        {
            zoomLocked = true;
        }

        double targetScale;
        if (zoomLocked)
        {
            targetScale = currentScale;
        }
        else
        {
            var distance = Math.Sqrt(span2);
            var k = distance0 > 0 && distance > 0
                ? Smooth(distance / distance0)
                : 1.0;

            targetScale = Math.Clamp(initialScale * k, MinScale, MaxScale);
        }

        canvasScaleTransform.ScaleX = canvasScaleTransform.ScaleY = targetScale;
        eraser.Scale = targetScale;

        var scale = targetScale / currentScale;

        var newOffsetX = CanvasScroll.HorizontalOffset * scale
            + (panPoint0.X - viewportOrigin.X) * scale
            - (firstPoint.X - viewportOrigin.X);
        var newOffsetY = CanvasScroll.VerticalOffset * scale
            + (panPoint0.Y - viewportOrigin.Y) * scale
            - (firstPoint.Y - viewportOrigin.Y);

        CanvasScroll.ScrollToHorizontalOffset(Math.Clamp(newOffsetX, 0, CanvasScroll.ScrollableWidth));
        CanvasScroll.ScrollToVerticalOffset(Math.Clamp(newOffsetY, 0, CanvasScroll.ScrollableHeight));

        currentScale = targetScale;
        panPoint0 = firstPoint;
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

    private void ZoomAtCursor(MouseWheelEventArgs e)
    {
        var factor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
        var target = Math.Clamp(currentScale * factor, MinScale, MaxScale);
        var k = target / currentScale;
        if (Math.Abs(k - 1.0) < 1e-9)
        {
            return;
        }

        var cursor = e.GetPosition(this);
        var newOffsetX = CanvasScroll.HorizontalOffset * k
            + (cursor.X - viewportOrigin.X) * (k - 1);
        var newOffsetY = CanvasScroll.VerticalOffset * k
            + (cursor.Y - viewportOrigin.Y) * (k - 1);

        canvasScaleTransform.ScaleX = canvasScaleTransform.ScaleY = target;
        eraser.Scale = target;
        currentScale = target;

        CanvasScroll.ScrollToHorizontalOffset(Math.Clamp(newOffsetX, 0, CanvasScroll.ScrollableWidth));
        CanvasScroll.ScrollToVerticalOffset(Math.Clamp(newOffsetY, 0, CanvasScroll.ScrollableHeight));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double Smooth(double x)
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
