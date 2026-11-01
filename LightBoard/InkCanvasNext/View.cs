using System;
using System.Windows;
using System.Windows.Media;

namespace InkCanvasNext;

/// <summary>
/// 视口与内容坐标系之间的相似变换（等比缩放 + 平移）
/// Offset 与位置参数一律以视口单位计：内容尺寸乘 Scale 后才是它在视口中的尺寸
/// </summary>
/// <param name="Scale">内容到视口的缩放倍数</param>
/// <param name="OffsetX">视口左边缘相对内容原点的偏移</param>
/// <param name="OffsetY">视口上边缘相对内容原点的偏移</param>
public readonly record struct View(double Scale, double OffsetX, double OffsetY)
{
    /// <summary>
    /// 内容坐标转视口坐标
    /// </summary>
    public Point ToViewport(Point content)
    {
        return new Point(content.X * Scale - OffsetX, content.Y * Scale - OffsetY);
    }

    /// <summary>
    /// 视口坐标转内容坐标
    /// </summary>
    public Point ToContent(Point viewport)
    {
        return new Point((viewport.X + OffsetX) / Scale, (viewport.Y + OffsetY) / Scale);
    }

    /// <summary>
    /// 以视口锚点缩放：锚点下的内容点在视口中的位置不变
    /// </summary>
    public View ZoomAt(Point viewportAnchor, double scale)
    {
        var content = ToContent(viewportAnchor);
        return new View(scale, scale * content.X - viewportAnchor.X, scale * content.Y - viewportAnchor.Y);
    }

    /// <summary>
    /// 视口窗口沿视口移动：偏移量增加 delta，内容在屏幕上反向移动 delta
    /// </summary>
    public View Pan(Vector offsetDelta)
    {
        return this with { OffsetX = OffsetX + offsetDelta.X, OffsetY = OffsetY + offsetDelta.Y };
    }

    /// <summary>
    /// 把缩放与偏移收进有效区间：缩放夹到 [minScale, maxScale]，偏移不超过内容可滚动范围
    /// 视口尺寸为 0 时只夹缩放，偏移保持原值
    /// </summary>
    public View Clamp(Size contentSize, Size viewport, double minScale, double maxScale)
    {
        var scale = Math.Clamp(Scale, minScale, maxScale);
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return this with { Scale = scale };
        }

        return new View(scale, OffsetX, OffsetY).Clamp(
            contentSize.Width * scale - viewport.Width,
            contentSize.Height * scale - viewport.Height);
    }

    /// <summary>
    /// 把偏移夹到 [0, 上限]；上限为负时按 0 处理
    /// </summary>
    public View Clamp(double maxOffsetX, double maxOffsetY)
    {
        var x = Math.Clamp(OffsetX, 0, Math.Max(0, maxOffsetX));
        var y = Math.Clamp(OffsetY, 0, Math.Max(0, maxOffsetY));
        return new View(Scale, x, y);
    }
}

public partial class InkCanvasNext
{
    private readonly ScaleTransform canvasScaleTransform = new(1.0, 1.0);

    /// <summary>
    /// 视口左上角在控件坐标系中的位置；控件根坐标换算到视口坐标时使用
    /// </summary>
    private Point viewportOrigin;

    /// <summary>
    /// 获取或设置当前视口；设置时把缩放与偏移夹到有效范围，非有限值被忽略
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
    /// 获取或设置画布缩放比例，超出范围时夹到 <see cref="MinScale"/> 与 <see cref="MaxScale"/> 之间
    /// </summary>
    public double CurrentScale
    {
        get => CurrentView.Scale;
        set
        {
            if (!double.IsFinite(value))
            {
                return;
            }

            var scale = Math.Clamp(value, MinScale, MaxScale);
            canvasScaleTransform.ScaleX = canvasScaleTransform.ScaleY = scale;
            eraser.Scale = scale;
        }
    }

    /// <summary>
    /// 获取或设置视口左边缘相对内容原点的水平偏移，视口 DIP；写入在布局完成后生效
    /// </summary>
    public double OffsetX
    {
        get => CanvasScroll.HorizontalOffset;
        set => CanvasScroll.ScrollToHorizontalOffset(value);
    }

    /// <summary>
    /// 获取或设置视口上边缘相对内容原点的垂直偏移，视口 DIP；写入在布局完成后生效
    /// </summary>
    public double OffsetY
    {
        get => CanvasScroll.VerticalOffset;
        set => CanvasScroll.ScrollToVerticalOffset(value);
    }

    /// <summary>
    /// 获取当前视口尺寸，视口 DIP
    /// </summary>
    public Size ViewportSize => new(CanvasScroll.ViewportWidth, CanvasScroll.ViewportHeight);

    /// <summary>
    /// 把内容矩形等比装进视口并居中；空矩形与非法边距被忽略
    /// </summary>
    /// <param name="contentRect">要装进视口的内容矩形</param>
    /// <param name="margin">视口四边留白，视口 DIP</param>
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
    /// 缩放到指定比例，超出有效范围时收进范围
    /// </summary>
    /// <param name="scale">目标缩放比例</param>
    /// <param name="viewportAnchor">缩放锚点，视口 DIP；默认视口中心，锚点下的内容位置保持不变</param>
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
}
