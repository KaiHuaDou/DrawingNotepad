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

    /// <summary>
    /// 获取或设置当前视口；设置时校验有限值、夹取缩放与偏移，再写入缩放与滚动位置
    /// </summary>
    public View CurrentView
    {
        get => new(canvasScaleTransform.ScaleX, CanvasScroll.HorizontalOffset, CanvasScroll.VerticalOffset);
        set
        {
            if (!double.IsFinite(value.Scale) || !double.IsFinite(value.OffsetX) || !double.IsFinite(value.OffsetY))
            {
                return;
            }

            value = value.Clamp(new Size(InnerCanvas.Width, InnerCanvas.Height), ViewportSize, MinScale, MaxScale);
            CurrentScale = value.Scale;
            CanvasScroll.ScrollToHorizontalOffset(value.OffsetX);
            CanvasScroll.ScrollToVerticalOffset(value.OffsetY);
        }
    }

    /// <summary>
    /// 把内容矩形等比装进视口并居中；margin 为视口 DIP
    /// </summary>
    public void FitToView(Rect contentRect, double margin = 0)
    {
        if (contentRect.IsEmpty || contentRect.Width <= 0 || contentRect.Height <= 0
            || !double.IsFinite(margin) || margin < 0)
        {
            return;
        }

        var viewport = ViewportSize;
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        var availableWidth = Math.Max(1, viewport.Width - 2 * margin);
        var availableHeight = Math.Max(1, viewport.Height - 2 * margin);
        var scale = Math.Clamp(
            Math.Min(availableWidth / contentRect.Width, availableHeight / contentRect.Height),
            MinScale,
            MaxScale);

        var center = new Point(
            contentRect.X + contentRect.Width / 2,
            contentRect.Y + contentRect.Height / 2);

        CurrentView = new View(scale, center.X * scale - viewport.Width / 2, center.Y * scale - viewport.Height / 2);
    }

    /// <summary>
    /// 缩放到指定比例；viewportAnchor 默认视口中心，单位视口 DIP
    /// </summary>
    public void ZoomTo(double scale, Point? viewportAnchor = null)
    {
        if (!double.IsFinite(scale))
        {
            return;
        }

        var viewport = ViewportSize;
        var anchor = viewportAnchor ?? new Point(viewport.Width / 2, viewport.Height / 2);
        var target = Math.Clamp(scale, MinScale, MaxScale);
        var content = CurrentView.ToContent(anchor);

        CurrentView = new View(target, content.X * target - anchor.X, content.Y * target - anchor.Y);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        viewportOrigin = CanvasScroll.TranslatePoint(new Point(0, 0), this);
        UpdateEdgeHints( );
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

        if (!zoomLocked && (displ2 > panZoomDisplaceThreshold2 || span2 < pinchLockDistance2))
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
            .Pan(panPoint0 - firstPoint);

        panPoint0 = firstPoint;
    }

    private void Pan( )
    {
        (var first, _) = GetMajorTouches( );

        CurrentView = CurrentView.Pan(panPoint0 - first!.Value);

        panPoint0 = first!.Value;
    }

    /// <summary>
    /// 平移/缩放手势结束时保证视口右/下方至少留有一屏余量：
    /// 距右/下边缘不足一屏（视口单位）则把画布扩展一屏（换算为内容坐标）
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
    /// 保证画布尺寸覆盖全部笔画：文件可能产生自其他分辨率设备，笔画越出画布右/下边界时扩展画布
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
        CanvasGrid.Width = InnerCanvas.Width;
        CanvasGrid.Height = InnerCanvas.Height;
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

        CurrentView = view.ZoomAt(anchor, target);
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
