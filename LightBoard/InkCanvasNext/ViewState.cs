using System;
using System.Windows;
using System.Windows.Media;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    /// <summary>
    /// 获取或设置当前视口；读取屏幕上的视图，写入前夹缩放与偏移并按需补画布
    /// </summary>
    public View CurrentView
    {
        get => new(CanvasScale.ScaleX, -CanvasTranslate.X, -CanvasTranslate.Y);
        set
        {
            var prefetch = CanRebase && !IsEdgeScrolling;
            ApplyView(value, prefetch, prefetch);
        }
    }

    private static bool IsFinite(View value)
    {
        return double.IsFinite(value.Scale)
            && double.IsFinite(value.OffsetX)
            && double.IsFinite(value.OffsetY);
    }

    /// <summary>
    /// 提交视图：夹缩放与偏移、写缩放与橡皮比例、补画布、写偏移，视图有变化时发一次通知
    /// 缩放与偏移都换过之后再比一次，零净变化的提交不通知
    /// </summary>
    private void ApplyView(View view, bool prefetchRightBottom = true, bool prefetchLeftTop = true)
    {
        if (!IsFinite(view))
        {
            return;
        }

        var before = CurrentView;

        view = view.Clamp(CanvasSize, ViewportSize, MinScale, MaxScale);

        if (CanvasScale.ScaleX != view.Scale)
        {
            CanvasScale.ScaleX = CanvasScale.ScaleY = view.Scale;
            eraser.Scale = view.Scale;
        }

        WriteOffset(EnsureCanvasFill(view, prefetchRightBottom, prefetchLeftTop));

        if (CurrentView != before)
        {
            RaiseViewOrSelectionChanged( );
        }
    }

    /// <summary>
    /// 把偏移夹进内容与视口决定的范围后写入变换，不改缩放与画布，也不发通知
    /// 供内容已同量平移的补偿写入使用，调用方保证写入后屏幕位置不变
    /// </summary>
    private void WriteOffset(View view)
    {
        var viewport = ViewportSize;

        // 视口尺寸为零时偏移就是初始位置，不能按内容尺寸夹
        if (viewport.Width > 0 && viewport.Height > 0)
        {
            view = view.Clamp(
                CanvasSize.Width * view.Scale - viewport.Width,
                CanvasSize.Height * view.Scale - viewport.Height);
        }

        if (CanvasTranslate.X == -view.OffsetX && CanvasTranslate.Y == -view.OffsetY)
        {
            return;
        }

        CanvasTranslate.X = -view.OffsetX;
        CanvasTranslate.Y = -view.OffsetY;
    }

    // 视口/选区通知按渲染帧合并：首个请求立即派发，保证孤立视图变化不晚一帧、抬手末帧不丢；
    // 同一帧内后续请求合并到下一帧，回调后立即摘订阅，静止时 WPF 可停帧。
    // 与 SelectionTransform.cs 的帧合并模式对称。

    private bool viewChangedFrameHooked;
    private bool viewChangedFramePending;

    /// <summary>
    /// 标记视口/选区已变化：首个请求立即生效，同一帧内随后的变化合并到下一帧。
    /// 立即生效保证缩放/撤销/翻页等孤立操作不晚一个渲染帧。
    /// </summary>
    private void RequestViewChangedFrame( )
    {
        if (!viewChangedFrameHooked)
        {
            ViewOrSelectionChanged?.Invoke(this, EventArgs.Empty);
            viewChangedFrameHooked = true;
            CompositionTarget.Rendering += ViewChangedFrame;
            return;
        }

        viewChangedFramePending = true;
    }

    private void ViewChangedFrame(object? sender, EventArgs e)
    {
        var apply = viewChangedFramePending;
        UnhookViewChangedFrame( );

        if (apply)
        {
            ViewOrSelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>摘掉渲染订阅并清空脏标记，避免渲染循环持有控件引用</summary>
    private void UnhookViewChangedFrame( )
    {
        if (!viewChangedFrameHooked)
        {
            return;
        }

        viewChangedFrameHooked = false;
        viewChangedFramePending = false;
        CompositionTarget.Rendering -= ViewChangedFrame;
    }
}
