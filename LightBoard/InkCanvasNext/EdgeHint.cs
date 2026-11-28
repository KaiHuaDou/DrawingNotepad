using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    private const double EdgeScrollSeconds = 0.18;
    private const double EdgeScrollTickMilliseconds = 16;

    // 目标多走 1 视口 DIP：热区与墨迹恰好相切时浮点误差会让判定仍为相交，提示留在原地
    private const double EdgeScrollClearance = 1;

    private static readonly EdgeScrollSide[] EdgeSides =
    [
        EdgeScrollSide.Top,
        EdgeScrollSide.Bottom,
        EdgeScrollSide.Left,
        EdgeScrollSide.Right
    ];

    private readonly List<Rect> edgeBounds = [];

    /// <summary>
    /// 四侧提示的显示状态，下标为 <see cref="EdgeScrollSide"/> 的取值
    /// </summary>
    private readonly bool[] edgeHintVisible = new bool[4];

    private readonly DispatcherTimer edgeScrollTimer = new(DispatcherPriority.Render);
    private Stopwatch? edgeScrollClock;
    private View edgeScrollFrom;
    private View edgeScrollTo;

    internal bool IsEdgeScrolling => edgeScrollTimer.IsEnabled;

    /// <summary>
    /// 该侧边缘提示是否应显示：热区内有墨迹，且该方向还能平移
    /// </summary>
    public bool IsEdgeHintVisible(EdgeScrollSide side)
    {
        return edgeHintVisible[(int) side];
    }

    /// <summary>
    /// 平移画布直到该侧热区内没有墨迹；没有可平移的位移时不动
    /// </summary>
    public void ScrollEdgeClear(EdgeScrollSide side)
    {
        var view = CurrentView;
        var viewport = ViewportSize;
        var band = EdgeBandContentRect(view, viewport, EdgeThreshold(side), side);
        if (band.IsEmpty)
        {
            return;
        }

        // 左/上扩展会平移内容，手势期间不能改动坐标系
        if (!CanRebase && side is EdgeScrollSide.Top or EdgeScrollSide.Left)
        {
            return;
        }

        CollectEdgeBounds( );

        var clearance = EdgeClearShift(side, band, view);
        if (clearance <= 0)
        {
            return;
        }

        var target = side switch
        {
            EdgeScrollSide.Top => view with { OffsetY = view.OffsetY - clearance },
            EdgeScrollSide.Bottom => view with { OffsetY = view.OffsetY + clearance },
            EdgeScrollSide.Left => view with { OffsetX = view.OffsetX - clearance },
            _ => view with { OffsetX = view.OffsetX + clearance },
        };

        target = ReachEdgeScrollTarget(target, viewport, side, out var shift);

        // 目标越出左/上边界时内容已经平移，起止视图都要用新坐标系中的偏移
        var from = CurrentView.Shifted(shift);

        if (target.OffsetX == from.OffsetX && target.OffsetY == from.OffsetY)
        {
            return;
        }

        if (shift.X != 0 || shift.Y != 0)
        {
            WriteOffset(from);
        }

        StartEdgeScroll(from, target);
    }

    /// <summary>
    /// 按当前视图与笔画刷新四侧提示的显示状态，状态变化时触发 <see cref="EdgeHintsChanged"/>
    /// </summary>
    private void UpdateEdgeHints( )
    {
        var view = CurrentView;
        var viewport = ViewportSize;
        var measurable = viewport.Width > 0 && viewport.Height > 0 && EdgeHintsEnabled( );

        if (measurable)
        {
            CollectEdgeBounds( );
        }

        var changed = false;
        foreach (var side in EdgeSides)
        {
            changed |= SetEdgeHint(side, measurable && EvaluateEdgeHint(side, view, viewport));
        }

        if (changed)
        {
            EdgeHintsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool SetEdgeHint(EdgeScrollSide side, bool visible)
    {
        var index = (int) side;
        if (edgeHintVisible[index] == visible)
        {
            return false;
        }

        edgeHintVisible[index] = visible;
        return true;
    }

    private bool EvaluateEdgeHint(EdgeScrollSide side, View view, Size viewport)
    {
        var band = EdgeBandContentRect(view, viewport, EdgeThreshold(side), side);
        return !band.IsEmpty && BandHasInk(edgeBounds, band) && HasEdgeScrollRoom(side, view, band);
    }

    /// <summary>
    /// 该侧仍存在可用的平移位移；四侧都可以在需要时扩展画布
    /// </summary>
    private bool HasEdgeScrollRoom(EdgeScrollSide side, View view, Rect band)
    {
        return EdgeClearShift(side, band, view) > 0;
    }

    /// <summary>
    /// 让该侧热区脱离墨迹所需的最小视口位移
    /// </summary>
    private double EdgeClearShift(EdgeScrollSide side, Rect band, View view)
    {
        var delta = BandClearDelta(edgeBounds, band, side);
        return delta > 0 ? delta * view.Scale + EdgeScrollClearance : 0;
    }

    private void CollectEdgeBounds( )
    {
        edgeBounds.Clear( );
        foreach (var stroke in InnerCanvas.Strokes)
        {
            edgeBounds.Add(stroke.GetBounds( ));
        }
    }

    private bool EdgeHintsEnabled( )
    {
        return options.EdgeScrollTopThreshold > 0 || options.EdgeScrollBottomThreshold > 0
            || options.EdgeScrollLeftThreshold > 0 || options.EdgeScrollRightThreshold > 0;
    }

    private double EdgeThreshold(EdgeScrollSide side)
    {
        return side switch
        {
            EdgeScrollSide.Top => options.EdgeScrollTopThreshold,
            EdgeScrollSide.Bottom => options.EdgeScrollBottomThreshold,
            EdgeScrollSide.Left => options.EdgeScrollLeftThreshold,
            _ => options.EdgeScrollRightThreshold,
        };
    }

    /// <summary>
    /// 目标偏移超出画布时先沿该轴扩展到够用；左/上扩展会平移内容，
    /// 平移量经 <paramref name="shift"/> 带回，目标偏移同步换成新坐标系中的值
    /// 过渡终点必须落在画布内，越界部分不能交给写入时的夹取截断，所以这里自己扩画布
    /// </summary>
    private View ReachEdgeScrollTarget(View target, Size viewport, EdgeScrollSide side, out Vector shift)
    {
        shift = default;
        var scale = target.Scale;

        if (side is EdgeScrollSide.Top or EdgeScrollSide.Bottom)
        {
            var max = InnerCanvas.Height * scale - viewport.Height;
            if (target.OffsetY > max)
            {
                ApplyGrowth(new CanvasGrowth(0, 0, 0, (target.OffsetY - max) / scale));
                max = InnerCanvas.Height * scale - viewport.Height;
            }
            else if (target.OffsetY < 0)
            {
                shift = ApplyGrowth(new CanvasGrowth(-target.OffsetY / scale, 0, 0, 0));
                target = target with { OffsetY = target.OffsetY + shift.Y * scale };
                max = InnerCanvas.Height * scale - viewport.Height;
            }

            return target with { OffsetY = Math.Clamp(target.OffsetY, 0, Math.Max(0, max)) };
        }

        var maxX = InnerCanvas.Width * scale - viewport.Width;
        if (target.OffsetX > maxX)
        {
            ApplyGrowth(new CanvasGrowth(0, 0, (target.OffsetX - maxX) / scale, 0));
            maxX = InnerCanvas.Width * scale - viewport.Width;
        }
        else if (target.OffsetX < 0)
        {
            shift = ApplyGrowth(new CanvasGrowth(-target.OffsetX / scale, 0, 0, 0));
            target = target with { OffsetX = target.OffsetX + shift.X * scale };
            maxX = InnerCanvas.Width * scale - viewport.Width;
        }

        return target with { OffsetX = Math.Clamp(target.OffsetX, 0, Math.Max(0, maxX)) };
    }

    private void StartEdgeScroll(View from, View target)
    {
        edgeScrollFrom = from;
        edgeScrollTo = target;
        edgeScrollClock = Stopwatch.StartNew( );
        edgeScrollTimer.Start( );
    }

    private void EdgeScrollTick(object? o, EventArgs e)
    {
        if (edgeScrollClock is not { } clock)
        {
            edgeScrollTimer.Stop( );
            return;
        }

        var t = Math.Min(1, clock.Elapsed.TotalSeconds / EdgeScrollSeconds);
        var k = t * t * (3 - 2 * t);

        CurrentView = new View(
            edgeScrollTo.Scale,
            edgeScrollFrom.OffsetX + (edgeScrollTo.OffsetX - edgeScrollFrom.OffsetX) * k,
            edgeScrollFrom.OffsetY + (edgeScrollTo.OffsetY - edgeScrollFrom.OffsetY) * k);

        if (t >= 1)
        {
            CancelEdgeScroll( );
        }
    }

    private void CancelEdgeScroll( )
    {
        edgeScrollTimer.Stop( );
        edgeScrollClock = null;
    }
}
