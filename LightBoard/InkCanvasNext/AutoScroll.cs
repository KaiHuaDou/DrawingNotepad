using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    private const double AutoScrollDurationSeconds = 0.1;

    private static readonly DependencyProperty AutoScrollProgressProperty = DependencyProperty.Register(
        "AutoScrollProgress",
        typeof(double),
        typeof(InkCanvasNext),
        new PropertyMetadata(OnAutoScrollProgressChanged));

    private View autoScrollFrom;
    private View autoScrollTo;
    private bool autoScrolling;

    private static void OnAutoScrollProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        (d as InkCanvasNext)?.AutoScrollTick((double) e.NewValue);
    }

    /// <summary>
    /// 抬手贴边自动滚动的目标视图（纯函数）：把落点沿贴边方向推到距对应边缘恰为阈值处，
    /// 上下与左右两轴独立判定，目标偏移按 [0, maxOffset] 截停。
    /// </summary>
    internal static View EdgeAutoScrollTarget(
        View view,
        Point touch,
        Size viewport,
        double verticalThreshold,
        double horizontalThreshold,
        double maxOffsetX,
        double maxOffsetY)
    {
        var offsetX = view.OffsetX;
        var offsetY = view.OffsetY;

        if (touch.Y < verticalThreshold)
        {
            offsetY -= verticalThreshold - touch.Y;
        }
        else if (viewport.Height - touch.Y < verticalThreshold)
        {
            offsetY += verticalThreshold - (viewport.Height - touch.Y);
        }

        if (touch.X < horizontalThreshold)
        {
            offsetX -= horizontalThreshold - touch.X;
        }
        else if (viewport.Width - touch.X < horizontalThreshold)
        {
            offsetX += horizontalThreshold - (viewport.Width - touch.X);
        }

        return new View(view.Scale, offsetX, offsetY).Clamp(maxOffsetX, maxOffsetY);
    }

    /// <summary>
    /// 抬手后无剩余触点且落点贴边时，把落点推离边缘至阈值处（0.1 秒 CubicEase EaseInOut 动画）。
    /// </summary>
    private void AutoScrollFromEdge(Point touch)
    {
        var view = CurrentView;
        var scale = view.Scale;

        // ScrollableWidth 在 EnsureEdgeMargin 刚扩展画布后是未经布局的陈旧值，上限改由内容尺寸直接换算
        var maxOffsetX = Math.Max(0, InnerCanvas.Width * scale - CanvasScroll.ViewportWidth);
        var maxOffsetY = Math.Max(0, InnerCanvas.Height * scale - CanvasScroll.ViewportHeight);

        var target = EdgeAutoScrollTarget(
            view,
            touch,
            ViewportSize,
            AutoScrollVerticalThreshold,
            AutoScrollHorizontalThreshold,
            maxOffsetX,
            maxOffsetY);

        if (target == view)
        {
            return;
        }

        autoScrollFrom = view;
        autoScrollTo = target;
        autoScrolling = true;

        var animation = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(AutoScrollDurationSeconds))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
        };

        // 动画默认 HoldEnd 把属性钉在终点；完成后清掉动画，清空时属性回退到基值 0 会再触发一次回调，由 autoScrolling 屏蔽
        animation.Completed += (_, _) =>
        {
            autoScrolling = false;
            BeginAnimation(AutoScrollProgressProperty, null);
        };
        BeginAnimation(AutoScrollProgressProperty, animation);
    }

    private void AutoScrollTick(double progress)
    {
        if (!autoScrolling)
        {
            return;
        }

        CurrentView = new View(
            autoScrollFrom.Scale,
            autoScrollFrom.OffsetX + (autoScrollTo.OffsetX - autoScrollFrom.OffsetX) * progress,
            autoScrollFrom.OffsetY + (autoScrollTo.OffsetY - autoScrollFrom.OffsetY) * progress);
    }

    private void CancelAutoScroll( )
    {
        if (!autoScrolling)
        {
            return;
        }

        autoScrolling = false;
        BeginAnimation(AutoScrollProgressProperty, null);
    }
}
