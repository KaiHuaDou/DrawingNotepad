using System.Windows;

using InkCanvasNext;

namespace LightBoard;

/// <summary>
/// 画布可见内容区域的一次快照：视图参数与对应内容盒
/// </summary>
internal readonly record struct CanvasView(double Scale, double OffsetX, double OffsetY, Rect Box);

/// <summary>
/// 画布坐标系基础与视口捕获；文档层只接收快照，不再回头访问宿主窗口
/// </summary>
internal static class CanvasViewport
{
    public static readonly Size CanvasSize = new(
        4 * SystemParameters.PrimaryScreenWidth,
        16 * SystemParameters.PrimaryScreenHeight);

    public static readonly Point InitialOffset = new(
        1.5 * SystemParameters.PrimaryScreenWidth,
        1.5 * SystemParameters.PrimaryScreenHeight);

    /// <summary>
    /// 捕获画布当前视图：背景盒取可见内容区域，背景随之落在视口内、画布无需移动；各文档页视图取捕获视图，
    /// 换页后外观一致。极小缩放下可见区域大于画布，盒子夹回画布内。视口尺寸未知时按 1x 整块主屏估算
    /// （无边框最大化窗口的视口即整块主屏）。
    /// </summary>
    public static CanvasView Capture(InkCanvasNext.InkCanvasNext? canvas)
    {
        if (canvas is null || canvas.ViewportSize is not { Width: > 0, Height: > 0 } viewport)
        {
            var width = SystemParameters.PrimaryScreenWidth;
            var height = SystemParameters.PrimaryScreenHeight;
            return new(1.0, InitialOffset.X, InitialOffset.Y, new Rect(InitialOffset.X, InitialOffset.Y, width, height));
        }

        var scale = canvas.CurrentScale;
        var offsetX = canvas.OffsetX;
        var offsetY = canvas.OffsetY;

        var box = new Rect(
            offsetX / scale,
            offsetY / scale,
            viewport.Width / scale,
            viewport.Height / scale);
        box.Intersect(new Rect(0, 0, CanvasSize.Width, CanvasSize.Height));

        return new(scale, offsetX, offsetY, box);
    }
}
