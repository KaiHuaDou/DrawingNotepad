using System;
using System.Runtime.CompilerServices;
using System.Windows;

using static InkCanvasNext.Geometry;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    private readonly double distanceThreshold;
    private readonly double distanceThreshold2;

    private Point prevMidpoint;
    private Point viewportOrigin;
    private double currentScale = 1.0;
    private double initialScale = 1.0;
    private double initialDistance;

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        viewportOrigin = CanvasScroll.TranslatePoint(new Point(0, 0), this);
    }

    private void InitGesture( )
    {
        (Point? first, Point? second) = GetMajorTouches( );
        prevMidpoint = second is null ? first!.Value : Midpoint(first!.Value, second.Value);
        initialDistance = second is null ? 0 : Distance(first!.Value, second.Value);
        initialScale = currentScale;
    }

    private void PanZoom( )
    {
        (Point? first, Point? second) = GetMajorTouches( );
        Point midpoint = second is null ? first!.Value : Midpoint(first!.Value, second.Value);
        var distance = second is null ? 0 : Distance(first!.Value, second.Value);

        var ratio = initialDistance > 0 && distance > 0
            ? distance / initialDistance
            : 1.0;

        var scale = Math.Clamp(initialScale * ratio, 0.1, 10.0);

        canvasScaleTransform.ScaleX = canvasScaleTransform.ScaleY = scale;

        ratio = scale / currentScale;

        var newOffsetX = CanvasScroll.HorizontalOffset * ratio
            + (prevMidpoint.X - viewportOrigin.X) * ratio
            - (midpoint.X - viewportOrigin.X);
        var newOffsetY = CanvasScroll.VerticalOffset * ratio
            + (prevMidpoint.Y - viewportOrigin.Y) * ratio
            - (midpoint.Y - viewportOrigin.Y);

        CanvasScroll.ScrollToHorizontalOffset(Math.Clamp(newOffsetX, 0, CanvasScroll.ScrollableWidth));
        CanvasScroll.ScrollToVerticalOffset(Math.Clamp(newOffsetY, 0, CanvasScroll.ScrollableHeight));

        currentScale = scale;
        prevMidpoint = midpoint;
    }

    private void Pan( )
    {
        (Point? first, Point? second) = GetMajorTouches( );
        Point midpoint = second is null ? first!.Value : Midpoint(first!.Value, second.Value);

        var newOffsetX = CanvasScroll.HorizontalOffset + prevMidpoint.X - midpoint.X;
        var newOffsetY = CanvasScroll.VerticalOffset + prevMidpoint.Y - midpoint.Y;

        CanvasScroll.ScrollToHorizontalOffset(Math.Clamp(newOffsetX, 0, CanvasScroll.ScrollableWidth));
        CanvasScroll.ScrollToVerticalOffset(Math.Clamp(newOffsetY, 0, CanvasScroll.ScrollableHeight));

        prevMidpoint = midpoint;
    }

#pragma warning disable IDE0008

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
        Point first = enumerator.Current.Position;

        if (count == 1)
        {
            return (first, null);
        }

        enumerator.MoveNext( );
        Point second = enumerator.Current.Position;

        return (first, second);
    }
}
