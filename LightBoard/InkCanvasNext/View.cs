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
    /// 同时夹取缩放与偏移：缩放夹到 [minScale, maxScale]，偏移上限按内容尺寸乘缩放减视口尺寸计算/ 视口为空表示布局尚未完成，只夹缩放，偏移保持原值
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
    /// 把偏移夹取到 [0, 上限]。上限由调用方给出，上限为负时按 0 处理
    /// </summary>
    public View Clamp(double maxOffsetX, double maxOffsetY)
    {
        var x = Math.Clamp(OffsetX, 0, Math.Max(0, maxOffsetX));
        var y = Math.Clamp(OffsetY, 0, Math.Max(0, maxOffsetY));
        return new View(Scale, x, y);
    }
}
