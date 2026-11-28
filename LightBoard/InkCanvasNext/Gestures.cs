using System;
using System.Runtime.CompilerServices;
using System.Windows;

using static InkCanvasNext.Geometry;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    private Point panPoint0;

    private Point panZoomAnchor;

    private double initialScale = 1.0;

    /// <summary>
    /// 本次手势是否已锁定缩放：两指位移或指距越过阈值后只平移不缩放
    /// </summary>
    internal bool ZoomLocked { get; private set; }

    /// <summary>
    /// 当前两指距离，视口 DIP；不足两指时为 0
    /// </summary>
    internal double PinchDistance
    {
        get
        {
            var (first, second) = GetMajorTouches(touches);
            return first is { } a && second is { } b ? Distance(a, b) : 0;
        }
    }

    /// <summary>
    /// 手势基线的两指距离，视口 DIP；建立基线时不足两指为 0
    /// </summary>
    internal double PinchDistance0 { get; private set; }

    /// <summary>
    /// 本次平移的累计手指位移，视口 DIP；从进入平移组起算，方向与手指移动一致
    /// </summary>
    internal Vector PanDisplacement { get; private set; }

    private void InitGesture( )
    {
        (var first, var second) = GetMajorTouches(touches);
        if (first is null)
        {
            return;
        }

        panPoint0 = first.Value;
        PinchDistance0 = second is null ? 0 : Distance(first.Value, second.Value);
        initialScale = CurrentScale;
        panZoomAnchor = second is null ? first.Value : MidPoint(first.Value, second.Value);
        ZoomLocked = false;
    }

    private void PanZoom( )
    {
        (var first, var second) = GetMajorTouches(touches);
        var firstPoint = first!.Value;

        var span2 = second is null ? 0.0 : Distance2(firstPoint, second.Value);
        var displ2 = second is null ? 0.0 : Distance2(MidPoint(firstPoint, second.Value), panZoomAnchor);

        if (!ZoomLocked && (displ2 > options.PanZoomDisplaceThreshold2 || span2 < options.PinchLockDistance2))
        {
            ZoomLocked = true;
        }

        var current = CurrentView;

        double targetScale;
        if (ZoomLocked)
        {
            targetScale = current.Scale;
        }
        else
        {
            var distance = Math.Sqrt(span2);
            var k = PinchDistance0 > 0 && distance > 0
                ? (options.SmoothZoom ? Smooth(distance / PinchDistance0) : distance / PinchDistance0)
                : 1.0;

            targetScale = Math.Clamp(initialScale * k, MinScale, MaxScale);
        }

        // 先把基线指位下的内容点钉住，再按两指位移平移：等价于把基线两指映射到当前两指
        var anchor = new Point(panPoint0.X - viewportOrigin.X, panPoint0.Y - viewportOrigin.Y);
        ApplyView(
            current.ZoomAt(anchor, targetScale).Pan(panPoint0 - firstPoint),
            prefetchRightBottom: false,
            prefetchLeftTop: false);

        panPoint0 = firstPoint;
    }

    private void Pan( )
    {
        (var first, _) = GetMajorTouches(touches);
        if (first is null)
        {
            return;
        }

        // 位移按手指移动方向累计（与 PanBy 的视口增量互为相反数）
        PanDisplacement += first.Value - panPoint0;
        PanBy(panPoint0 - first.Value);
        panPoint0 = first.Value;
    }

    /// <summary>
    /// 平移视图：偏移加 delta，越界部分不保留
    /// </summary>
    private void PanBy(Vector delta)
    {
        ApplyView(CurrentView.Pan(delta), prefetchRightBottom: false, prefetchLeftTop: false);
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
