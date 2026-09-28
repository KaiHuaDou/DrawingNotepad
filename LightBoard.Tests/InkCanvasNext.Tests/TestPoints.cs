using System.Windows;

namespace InkCanvasNext.Tests;

/// <summary>
/// 按画布运行时参数推导测试坐标：间距、位移等阈值调整后测试无需改动，
/// 只要状态机行为与参数设定一致即保持通过。
/// </summary>
internal static class TestPoints
{
    /// <summary>状态机使用的触点间距阈值 l（视口 DIP），与画布构造取值同源。</summary>
    public static double Spacing(TouchHost host)
    {
        return Math.Sqrt(host.Canvas.distanceThreshold2);
    }

    /// <summary>单指起笔位移阈值 c（视口 DIP）。</summary>
    public static double Displacement(TouchHost host)
    {
        return Math.Sqrt(host.Canvas.TouchDisplacementThreshold2);
    }

    /// <summary>双指捏合测试的初始间距：捏合锁死阈值的 2 倍，保证初始不触发距离锁死，且不超过指距阈值 l。</summary>
    public static double PinchSpacing(TouchHost host)
    {
        return Math.Sqrt(host.Canvas.PinchLockDistance2) * 2;
    }

    /// <summary>p 右侧的近距点：与 p 间距 l/8，满足 d ≤ l 的捏合/平移判定。</summary>
    public static Point CloseTo(TouchHost host, Point p)
    {
        return new Point(p.X + Spacing(host) / 8, p.Y);
    }

    /// <summary>从 p 起每隔 l/8 横向排布 count 个近距点：任意两点间距不超过 (count-1)·l/8，count ≤ 9 时恒有 d ≤ l。</summary>
    public static Point[] CloseCluster(TouchHost host, Point p, int count)
    {
        var step = Spacing(host) / 8;
        var points = new Point[count];
        for (var i = 0; i < count; i++)
        {
            points[i] = new Point(p.X + i * step, p.Y);
        }

        return points;
    }

    /// <summary>p 右侧的远距点：与 p 间距 2.5l，满足 d &gt; l 的 MultiDraw 判定。</summary>
    public static Point FarFrom(TouchHost host, Point p)
    {
        return new Point(p.X + Spacing(host) * 2.5, p.Y);
    }

    /// <summary>p 右侧的移动目标：位移 1.5c + 1，超过单指起笔阈值（EvalDraw --> Draw）。</summary>
    public static Point BeyondDisplacement(TouchHost host, Point p)
    {
        return new Point(p.X + Displacement(host) * 1.5 + 1, p.Y);
    }
}
