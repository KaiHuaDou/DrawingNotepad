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

    private Point viewportOrigin;

    private Point panPoint0;
    private double distance0;

    private Point panZoomAnchor;
    private bool zoomLocked;

    private double initialScale = 1.0;

    private View CurrentView
    {
        get => new(canvasScaleTransform.ScaleX, CanvasScroll.HorizontalOffset, CanvasScroll.VerticalOffset);
        set
        {
            CurrentScale = value.Scale;
            CanvasScroll.ScrollToHorizontalOffset(value.OffsetX);
            CanvasScroll.ScrollToVerticalOffset(value.OffsetY);
        }
    }

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
        initialScale = CurrentView.Scale;
        panZoomAnchor = second is null ? first.Value : MidPoint(first.Value, second.Value);
        zoomLocked = false;
    }

    private void PanZoom( )
    {
        (var first, var second) = GetMajorTouches( );
        var firstPoint = first!.Value;

        var span2 = second is null ? 0.0 : Distance2(firstPoint, second.Value);
        var displ2 = second is null ? 0.0 : Distance2(MidPoint(firstPoint, second.Value), panZoomAnchor);

        if (!zoomLocked && (displ2 > PanZoomDisplaceThreshold2 || span2 < PinchLockDistance2))
        {
            zoomLocked = true;
        }

        var view = CurrentView;

        double targetScale;
        if (zoomLocked)
        {
            targetScale = view.Scale;
        }
        else
        {
            var distance = Math.Sqrt(span2);
            var k = distance0 > 0 && distance > 0
                ? Smooth(distance / distance0)
                : 1.0;

            targetScale = Math.Clamp(initialScale * k, MinScale, MaxScale);
        }

        // 先把基线指位下的内容点钉住，再按两指位移平移：等价于把基线两指映射到当前两指
        var anchor = new Point(panPoint0.X - viewportOrigin.X, panPoint0.Y - viewportOrigin.Y);

        CurrentView = view
            .ZoomAt(anchor, targetScale)
            .Pan(panPoint0 - firstPoint)
            .Clamp(CanvasScroll.ScrollableWidth, CanvasScroll.ScrollableHeight);

        panPoint0 = firstPoint;
    }

    private void Pan( )
    {
        (var first, _) = GetMajorTouches( );

        CurrentView = CurrentView
            .Pan(panPoint0 - first!.Value)
            .Clamp(CanvasScroll.ScrollableWidth, CanvasScroll.ScrollableHeight);

        panPoint0 = first!.Value;
    }

    /// <summary>
    /// 平移/缩放手势结束时保证视口右/下方至少留有一屏余量：
    /// 距右/下边缘不足一屏（视口单位）则把画布扩展一屏（换算为内容坐标）。
    /// </summary>
    private void EnsureEdgeMargin( )
    {
        var scale = CurrentView.Scale;
        var extendWidth = CanvasScroll.ScrollableWidth - CanvasScroll.HorizontalOffset < CanvasScroll.ViewportWidth
            ? CanvasScroll.ViewportWidth / scale
            : 0;
        var extendHeight = CanvasScroll.ScrollableHeight - CanvasScroll.VerticalOffset < CanvasScroll.ViewportHeight
            ? CanvasScroll.ViewportHeight / scale
            : 0;

        ExtendCanvas(extendWidth, extendHeight);
    }

    /// <summary>
    /// 保证画布尺寸覆盖全部笔画：文件可能产生自其他分辨率设备，笔画越出画布右/下边界时扩展画布。
    /// </summary>
    public void EnsureStrokesFit( )
    {
        var bounds = InnerCanvas.Strokes.GetBounds( );
        if (bounds.IsEmpty)
        {
            return;
        }

        ExtendCanvas(bounds.Right - InnerCanvas.Width, bounds.Bottom - InnerCanvas.Height);
    }

    private void ExtendCanvas(double extendWidth, double extendHeight)
    {
        extendWidth = Math.Max(0, extendWidth);
        extendHeight = Math.Max(0, extendHeight);

        if (extendWidth == 0 && extendHeight == 0)
        {
            return;
        }

        InnerCanvas.Width += extendWidth;
        InnerCanvas.Height += extendHeight;
        selectionVisual.Resize(new Size(InnerCanvas.Width, InnerCanvas.Height));
    }

    private void ZoomAtCursor(MouseWheelEventArgs e)
    {
        var view = CurrentView;
        var factor = e.Delta > 0 ? WheelZoomFactor : 1.0 / WheelZoomFactor;
        var target = Math.Clamp(view.Scale * factor, MinScale, MaxScale);
        if (Math.Abs(target / view.Scale - 1.0) < 1e-9)
        {
            return;
        }

        var cursor = e.GetPosition(this);
        var anchor = new Point(cursor.X - viewportOrigin.X, cursor.Y - viewportOrigin.Y);

        CurrentView = view
            .ZoomAt(anchor, target)
            .Clamp(CanvasScroll.ScrollableWidth, CanvasScroll.ScrollableHeight);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double Smooth(double x)
    {
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
