using System;
using System.Windows;

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
    /// 视口在内容坐标系中覆盖的矩形；视口尺寸非正或缩放非正时返回空矩形
    /// </summary>
    public Rect ContentRect(Size viewport)
    {
        if (viewport.Width <= 0 || viewport.Height <= 0 || !(Scale > 0))
        {
            return Rect.Empty;
        }

        return new Rect(
            OffsetX / Scale,
            OffsetY / Scale,
            viewport.Width / Scale,
            viewport.Height / Scale);
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
    /// 内容整体平移后的视图：偏移同量抬高，屏幕位置不变
    /// </summary>
    public View Shifted(Vector contentShift)
    {
        return this with
        {
            OffsetX = OffsetX + contentShift.X * Scale,
            OffsetY = OffsetY + contentShift.Y * Scale
        };
    }

    /// <summary>
    /// 把缩放与偏移收进有效区间：缩放夹到 [minScale, maxScale]，偏移不超过内容与视口决定的范围
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
    /// <summary>
    /// 视口左上角在控件坐标系中的位置；控件根坐标换算到视口坐标时使用
    /// </summary>
    private Point viewportOrigin;

    /// <summary>
    /// 画布在视口右/下不足一屏、左/上不足半屏时按一屏步进补足：缩放、拖动与翻页恢复视图都经过这里，
    /// 保证视口落在画布内并能继续拖动；右/下最多补四屏
    /// prefetchRightBottom 为 false 时只在画布盖不住视口时补，不做右/下方向留一屏余量的预取
    /// prefetchLeftTop 为 false 时不做左/上方向留余量的预取，用于手势起点保留已有余量
    /// 左/上补足会平移内容并抬高偏移，返回值带回补偿后的视图
    /// </summary>
    private View EnsureCanvasFill(View view, bool prefetchRightBottom = true, bool prefetchLeftTop = true)
    {
        var viewport = ViewportSize;
        if (viewport.Width <= 0 || viewport.Height <= 0 || !double.IsFinite(view.Scale) || view.Scale <= 0)
        {
            return view;
        }

        var growth = CanvasGrowth.FromView(
            view,
            viewport,
            CanvasSize,
            prefetchRightBottom,
            prefetchLeftTop);

        if (growth.IsEmpty)
        {
            return view;
        }

        var shift = ApplyGrowth(growth);
        return view with
        {
            OffsetX = view.OffsetX + shift.X * view.Scale,
            OffsetY = view.OffsetY + shift.Y * view.Scale
        };
    }

    /// <summary>
    /// 获取或设置画布缩放比例，超出范围时夹到 <see cref="MinScale"/> 与 <see cref="MaxScale"/> 之间
    /// 偏移随缩放收进有效范围，画布随之补到盖住视口
    /// </summary>
    public double CurrentScale
    {
        get => CanvasScale.ScaleX;
        set => ApplyView(CurrentView with { Scale = value }, prefetchRightBottom: false, prefetchLeftTop: false);
    }

    /// <summary>
    /// 获取或设置视口左边缘相对内容原点的水平偏移，视口 DIP
    /// </summary>
    public double OffsetX
    {
        get => -CanvasTranslate.X;
        set
        {
            if (double.IsFinite(value))
            {
                ApplyView(CurrentView with { OffsetX = value }, prefetchRightBottom: false, prefetchLeftTop: false);
            }
        }
    }

    /// <summary>
    /// 获取或设置视口上边缘相对内容原点的垂直偏移，视口 DIP
    /// </summary>
    public double OffsetY
    {
        get => -CanvasTranslate.Y;
        set
        {
            if (double.IsFinite(value))
            {
                ApplyView(CurrentView with { OffsetY = value }, prefetchRightBottom: false, prefetchLeftTop: false);
            }
        }
    }

    /// <summary>
    /// 获取画布尺寸，内容 DIP
    /// </summary>
    public Size CanvasSize => new(InnerCanvas.Width, InnerCanvas.Height);

    /// <summary>
    /// 获取当前视口尺寸，视口 DIP
    /// </summary>
    public Size ViewportSize => new(ViewportHost.ActualWidth, ViewportHost.ActualHeight);

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
        viewportOrigin = ViewportHost.TranslatePoint(new Point(0, 0), this);
        UpdateEdgeHints( );
    }
}
