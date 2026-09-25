using System;
using System.Windows;

namespace InkCanvasNext;

/// <summary>
/// 视口与内容坐标系之间的相似变换（等比缩放 + 平移）
/// Offset 与位置参数一律以视口单位计：内容尺寸乘 Scale 后才是它在视口中的尺寸。
/// </summary>
/// <param name="Scale">内容到视口的缩放倍数。</param>
/// <param name="OffsetX">视口左边缘相对内容原点的偏移。</param>
/// <param name="OffsetY">视口上边缘相对内容原点的偏移。</param>
internal readonly record struct View(double Scale, double OffsetX, double OffsetY)
{
    /// <summary>
    /// 内容坐标转视口坐标。
    /// </summary>
    public Point ToViewport(Point content)
    {
        return new Point(content.X * Scale - OffsetX, content.Y * Scale - OffsetY);
    }

    /// <summary>
    /// 视口坐标转内容坐标。
    /// </summary>
    public Point ToContent(Point viewport)
    {
        return new Point((viewport.X + OffsetX) / Scale, (viewport.Y + OffsetY) / Scale);
    }

    /// <summary>
    /// 以视口锚点缩放：锚点下的内容点在视口中的位置不变。
    /// </summary>
    public View ZoomAt(Point viewportAnchor, double scale)
    {
        var content = ToContent(viewportAnchor);
        return new View(scale, scale * content.X - viewportAnchor.X, scale * content.Y - viewportAnchor.Y);
    }

    /// <summary>
    /// 视口窗口沿视口移动：偏移量增加 delta，内容在屏幕上反向移动 delta。
    /// </summary>
    public View Pan(Vector offsetDelta)
    {
        return this with { OffsetX = OffsetX + offsetDelta.X, OffsetY = OffsetY + offsetDelta.Y };
    }

    /// <summary>
    /// 把偏移夹取到 [0, 上限]。上限由载体给出：当前为 ScrollViewer 的 ScrollableWidth/Height
    /// （等价于内容尺寸 × Scale − 视口尺寸），上限为负时按 0 处理。
    /// </summary>
    public View Clamp(double maxOffsetX, double maxOffsetY)
    {
        var x = Math.Clamp(OffsetX, 0, Math.Max(0, maxOffsetX));
        var y = Math.Clamp(OffsetY, 0, Math.Max(0, maxOffsetY));
        return new View(Scale, x, y);
    }
}
