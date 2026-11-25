using System;
using System.Windows;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    /// <summary>
    /// 已提交到控件的视图，手势期间不含 <c>CanvasPan</c> 的增量
    /// 偏移与缩放分别由 ScrollViewer 与 LayoutTransform 持有，这里是同值的缓存
    /// </summary>
    internal View GestureView { get; private set; }

    /// <summary>
    /// 屏幕上实际可见的视图：已提交视图叠加上手势平移
    /// </summary>
    internal View EffectiveView => GestureView with
    {
        OffsetX = GestureView.OffsetX - CanvasPan.X,
        OffsetY = GestureView.OffsetY - CanvasPan.Y
    };

    /// <summary>
    /// 获取或设置当前视口；读取含手势期间的未提交增量，写入会作废该增量
    /// </summary>
    public View CurrentView
    {
        get => EffectiveView;
        set
        {
            var canPrefetch = CanRebase && !IsEdgeScrolling;
            var resolved = ResolveView(value, canPrefetch, canPrefetch);
            if (resolved is null)
            {
                return;
            }

            // 外部换视图时手势增量作废，避免旧变换叠加到新偏移上
            ClearPan( );
            WriteViewOffsets(resolved.Value);
        }
    }

    private static bool IsFinite(View value)
    {
        return double.IsFinite(value.Scale)
            && double.IsFinite(value.OffsetX)
            && double.IsFinite(value.OffsetY);
    }

    /// <summary>
    /// 限幅视图、写入缩放并按余量扩展画布，返回可直接写入偏移的视图；非有限值返回 null
    /// </summary>
    private View? ResolveView(View value, bool prefetchRightBottom, bool prefetchLeftTop)
    {
        if (!IsFinite(value))
        {
            return null;
        }

        value = value.Clamp(
            new Size(InnerCanvas.Width, InnerCanvas.Height),
            ViewportSize,
            MinScale,
            MaxScale);

        SetScale(value.Scale);
        return EnsureCanvasFill(value, prefetchRightBottom, prefetchLeftTop);
    }

    /// <summary>
    /// 把偏移夹到内容可滚动范围后更新缓存；偏移有变化才写入控件，避免未变的基准在每次触摸报文里重复排队
    /// </summary>
    private View WriteViewOffsets(View value)
    {
        // 视口尺寸为零时偏移就是初始位置，不能按内容尺寸夹
        if (ViewportSize.Width > 0 && ViewportSize.Height > 0)
        {
            value = value.Clamp(
                InnerCanvas.Width * value.Scale - ViewportSize.Width,
                InnerCanvas.Height * value.Scale - ViewportSize.Height);
        }

        // 与缓存的上次请求值比较：ScrollTo* 只入队，控件读数是布局前的旧值，不能作为是否变化的依据
        var previous = GestureView;
        GestureView = value;

        if (previous.OffsetX != value.OffsetX)
        {
            CanvasScroll.ScrollToHorizontalOffset(value.OffsetX);
        }

        if (previous.OffsetY != value.OffsetY)
        {
            CanvasScroll.ScrollToVerticalOffset(value.OffsetY);
        }

        return value;
    }

    private void SetScale(double value)
    {
        if (!double.IsFinite(value))
        {
            return;
        }

        var scale = Math.Clamp(value, MinScale, MaxScale);
        CanvasScale.ScaleX = CanvasScale.ScaleY = scale;
        eraser.Scale = scale;
        GestureView = GestureView with { Scale = scale };
    }

    private void ClearPan( )
    {
        CanvasPan.X = 0;
        CanvasPan.Y = 0;
    }

    /// <summary>
    /// 把手势期累积的平移增量提交回 ScrollViewer 偏移：
    /// ScrollTo* 只入队且一次布局只执行一条，逐轮排空布局让两轴都落地后再清零 RenderTransform，
    /// 避免抬手时内容先回起点再跳到终点的一帧回弹
    /// </summary>
    private void CommitPan( )
    {
        if (CanvasPan.X == 0 && CanvasPan.Y == 0)
        {
            return;
        }

        var target = WriteViewOffsets(EffectiveView);

        // 未变的偏移不会入队，正常只有本次两条命令；两轮内两轴都能落地，已落地时不再触发布局
        for (var i = 0; i < 2; i++)
        {
            if (CanvasScroll.HorizontalOffset == target.OffsetX
                && CanvasScroll.VerticalOffset == target.OffsetY)
            {
                break;
            }

            CanvasScroll.UpdateLayout( );
        }

        ClearPan( );
    }
}
