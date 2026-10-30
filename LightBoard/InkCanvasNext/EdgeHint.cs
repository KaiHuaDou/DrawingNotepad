using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    private const double EdgeScrollSeconds = 0.18;
    private const double EdgeScrollTickMilliseconds = 16;

    private readonly List<Rect> edgeBounds = [];

    private readonly DispatcherTimer edgeScrollTimer = new(DispatcherPriority.Render);
    private Stopwatch? edgeScrollClock;
    private View edgeScrollFrom;
    private View edgeScrollTo;

    /// <summary>测试接缝：过渡是否进行中。</summary>
    internal bool IsEdgeScrolling => edgeScrollTimer.IsEnabled;

    /// <summary>
    /// 按当前视图与笔画刷新四个提示按钮：热区有墨迹且该方向可平移才显示，按钮贴视口边缘。
    /// </summary>
    private void UpdateEdgeHints( )
    {
        var view = CurrentView;
        var viewport = ViewportSize;

        if (viewport.Width <= 0 || viewport.Height <= 0 || !EdgeHintsEnabled( ))
        {
            HideEdgeHints( );
            return;
        }

        CollectEdgeBounds( );

        UpdateEdgeHint(EdgeScrollSide.Top, view, viewport);
        UpdateEdgeHint(EdgeScrollSide.Bottom, view, viewport);
        UpdateEdgeHint(EdgeScrollSide.Left, view, viewport);
        UpdateEdgeHint(EdgeScrollSide.Right, view, viewport);
    }

    private void UpdateEdgeHint(EdgeScrollSide side, View view, Size viewport)
    {
        var button = EdgeHintButton(side);
        var band = EdgeBandContentRect(view, viewport, EdgeThreshold(side), side);

        if (band.IsEmpty || !BandHasInk(edgeBounds, band) || !HasEdgeScrollRoom(side, view, band))
        {
            button.Visibility = Visibility.Collapsed;
            return;
        }

        PlaceEdgeHint(side, button, viewport);
        button.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// 上/左没有负方向余量，位移超出当前偏移时按钮点了也没有效果；下/右在点击时按需扩展画布。
    /// </summary>
    private bool HasEdgeScrollRoom(EdgeScrollSide side, View view, Rect band)
    {
        if (side is EdgeScrollSide.Bottom or EdgeScrollSide.Right)
        {
            return true;
        }

        var shift = BandClearDelta(edgeBounds, band, side) * view.Scale;
        return shift > 0 && shift <= (side == EdgeScrollSide.Top ? view.OffsetY : view.OffsetX);
    }

    /// <summary>
    /// 按钮贴视口四边摆放，沿该侧居中；边距由 <see cref="EdgeHintMargin"/> 给出，供宿主让开常驻界面。
    /// </summary>
    private void PlaceEdgeHint(EdgeScrollSide side, FrameworkElement button, Size viewport)
    {
        var margin = EdgeHintMargin;
        var width = button.Width;
        var height = button.Height;

        var x = side switch
        {
            EdgeScrollSide.Left => margin.Left,
            EdgeScrollSide.Right => viewport.Width - margin.Right - width,
            _ => (viewport.Width - width) / 2,
        };

        var y = side switch
        {
            EdgeScrollSide.Top => margin.Top,
            EdgeScrollSide.Bottom => viewport.Height - margin.Bottom - height,
            _ => (viewport.Height - height) / 2,
        };

        Canvas.SetLeft(button, Math.Clamp(x, 0, Math.Max(0, viewport.Width - width)));
        Canvas.SetTop(button, Math.Clamp(y, 0, Math.Max(0, viewport.Height - height)));
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
        return EdgeScrollTopThreshold > 0 || EdgeScrollBottomThreshold > 0
            || EdgeScrollLeftThreshold > 0 || EdgeScrollRightThreshold > 0;
    }

    private double EdgeThreshold(EdgeScrollSide side)
    {
        return side switch
        {
            EdgeScrollSide.Top => EdgeScrollTopThreshold,
            EdgeScrollSide.Bottom => EdgeScrollBottomThreshold,
            EdgeScrollSide.Left => EdgeScrollLeftThreshold,
            _ => EdgeScrollRightThreshold,
        };
    }

    private Button EdgeHintButton(EdgeScrollSide side)
    {
        return side switch
        {
            EdgeScrollSide.Top => TopEdgeHint,
            EdgeScrollSide.Bottom => BottomEdgeHint,
            EdgeScrollSide.Left => LeftEdgeHint,
            _ => RightEdgeHint,
        };
    }

    private void HideEdgeHints( )
    {
        TopEdgeHint.Visibility = Visibility.Collapsed;
        BottomEdgeHint.Visibility = Visibility.Collapsed;
        LeftEdgeHint.Visibility = Visibility.Collapsed;
        RightEdgeHint.Visibility = Visibility.Collapsed;
    }

    private void TopEdgeHintClick(object o, RoutedEventArgs e)
    {
        ScrollEdgeClear(EdgeScrollSide.Top);
    }

    private void BottomEdgeHintClick(object o, RoutedEventArgs e)
    {
        ScrollEdgeClear(EdgeScrollSide.Bottom);
    }

    private void LeftEdgeHintClick(object o, RoutedEventArgs e)
    {
        ScrollEdgeClear(EdgeScrollSide.Left);
    }

    private void RightEdgeHintClick(object o, RoutedEventArgs e)
    {
        ScrollEdgeClear(EdgeScrollSide.Right);
    }

    /// <summary>
    /// 平移画布直到该侧热区内没有墨迹；没有可平移的余量时不动。
    /// </summary>
    private void ScrollEdgeClear(EdgeScrollSide side)
    {
        var view = CurrentView;
        var viewport = ViewportSize;
        var band = EdgeBandContentRect(view, viewport, EdgeThreshold(side), side);
        if (band.IsEmpty)
        {
            return;
        }

        CollectEdgeBounds( );

        var shift = BandClearDelta(edgeBounds, band, side) * view.Scale;
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
    /// 下/右的目标偏移超出画布时先把画布沿该轴扩展到够用，再夹到有效范围。
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
