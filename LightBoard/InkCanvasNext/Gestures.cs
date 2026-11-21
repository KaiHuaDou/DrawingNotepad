using System;
using System.Runtime.CompilerServices;
using System.Windows;

using static InkCanvasNext.Geometry;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    private Point panPoint0;
    private double distance0;

    private Point panZoomAnchor;
    private bool zoomLocked;

    private double initialScale = 1.0;

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
        // 缩放仍走 LayoutTransform + 滚动偏移：先把 EvalPan 累积的手势平移落回偏移，避免两套平移叠加
        CommitPan( );

        (var first, var second) = GetMajorTouches( );
        var firstPoint = first!.Value;

        var span2 = second is null ? 0.0 : Distance2(firstPoint, second.Value);
        var displ2 = second is null ? 0.0 : Distance2(MidPoint(firstPoint, second.Value), panZoomAnchor);

        if (!zoomLocked && (displ2 > options.PanZoomDisplaceThreshold2 || span2 < options.PinchLockDistance2))
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
                ? (options.SmoothZoom ? Smooth(distance / distance0) : distance / distance0)
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
        if (first is null)
        {
            return;
        }

        PanBy(panPoint0 - first.Value);
        panPoint0 = first.Value;
    }

    /// <summary>
    /// 手势期平移：只写 TranslateHost 的 RenderTransform，不碰 ScrollViewer 偏移，
    /// </summary>
    private void PanBy(Vector delta)
    {
        var view = CurrentView;
        var viewport = ViewportSize;
        var maxX = Math.Max(0, InnerCanvas.Width * view.Scale - viewport.Width);
        var maxY = Math.Max(0, InnerCanvas.Height * view.Scale - viewport.Height);

        // 已提交偏移 - 手势增量 = 提交后的偏移；夹紧后再反解回增量
        var left = Math.Clamp(view.OffsetX - CanvasPan.X + delta.X, 0, maxX);
        var top = Math.Clamp(view.OffsetY - CanvasPan.Y + delta.Y, 0, maxY);

        CanvasPan.X = view.OffsetX - left;
        CanvasPan.Y = view.OffsetY - top;

        // 手势中不再有 ScrollChanged：选区工具栏等消费方按原来的节奏手动通知一次
        RaiseViewOrSelectionChanged( );
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
