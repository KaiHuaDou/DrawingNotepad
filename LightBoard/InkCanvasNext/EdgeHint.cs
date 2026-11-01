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

    /// <summary>测试接缝：过渡是否进行中</summary>
    internal bool IsEdgeScrolling => edgeScrollTimer.IsEnabled;

    /// <summary>
    /// 该侧边缘提示是否应显示：热区内有墨迹，且该方向还能平移
    /// </summary>
    public bool IsEdgeHintVisible(EdgeScrollSide side)
    {
        return edgeHintVisible[(int) side];
    }

    /// <summary>
    /// 平移画布直到该侧热区内没有墨迹；没有可平移的余量时不动
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

        CollectEdgeBounds( );

        var shift = EdgeClearShift(side, band, view);
        if (shift <= 0)
        {
            return;
        }

        if (side is EdgeScrollSide.Top or EdgeScrollSide.Left)
        {
            var room = side == EdgeScrollSide.Top ? view.OffsetY : view.OffsetX;
            if (shift > room)
            {
                return;
            }
        }

        var target = side switch
        {
            EdgeScrollSide.Top => view with { OffsetY = view.OffsetY - shift },
            EdgeScrollSide.Bottom => view with { OffsetY = view.OffsetY + shift },
            EdgeScrollSide.Left => view with { OffsetX = view.OffsetX - shift },
            _ => view with { OffsetX = view.OffsetX + shift },
        };

        target = ReachEdgeScrollTarget(target, viewport, side);
        if (target.OffsetX == view.OffsetX && target.OffsetY == view.OffsetY)
        {
            return;
        }

        StartEdgeScroll(target);
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
    /// 上/左没有负方向余量，位移超出当前偏移时按钮点了也没有效果；下/右在点击时按需扩展画布
    /// </summary>
    private bool HasEdgeScrollRoom(EdgeScrollSide side, View view, Rect band)
    {
        var shift = EdgeClearShift(side, band, view);
        if (side is EdgeScrollSide.Bottom or EdgeScrollSide.Right)
        {
            return shift > 0;
        }

        return shift > 0 && shift <= (side == EdgeScrollSide.Top ? view.OffsetY : view.OffsetX);
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
    /// 下/右的目标偏移超出画布时先把画布沿该轴扩展到够用，再夹到有效范围
    /// </summary>
    private View ReachEdgeScrollTarget(View target, Size viewport, EdgeScrollSide side)
    {
        var scale = target.Scale;

        if (side is EdgeScrollSide.Top or EdgeScrollSide.Bottom)
        {
            var max = InnerCanvas.Height * scale - viewport.Height;
            if (target.OffsetY > max)
            {
                ExtendCanvas(0, (target.OffsetY - max) / scale);
                max = InnerCanvas.Height * scale - viewport.Height;
            }

            return target with { OffsetY = Math.Clamp(target.OffsetY, 0, Math.Max(0, max)) };
        }

        var maxX = InnerCanvas.Width * scale - viewport.Width;
        if (target.OffsetX > maxX)
        {
            ExtendCanvas((target.OffsetX - maxX) / scale, 0);
            maxX = InnerCanvas.Width * scale - viewport.Width;
        }

        return target with { OffsetX = Math.Clamp(target.OffsetX, 0, Math.Max(0, maxX)) };
    }

    private void StartEdgeScroll(View target)
    {
        edgeScrollFrom = CurrentView;
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
