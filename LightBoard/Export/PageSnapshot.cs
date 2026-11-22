using System.Linq;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;

namespace LightBoard;

/// <summary>
/// 导出用页快照：在 UI 线程取一次，之后的渲染可脱离 UI 线程；新的导出器（如 SVG）复用同一入口
/// </summary>
internal readonly record struct PageSnapshot(
    int Number,
    StrokeCollection Strokes,
    ImageSource? Background,
    Rect? Box,
    Color? Canvas)
{
    /// <summary>
    /// 必须在 UI 线程调用：克隆墨迹并解码背景位图
    /// </summary>
    public static PageSnapshot Capture(Page page)
    {
        var background = page.Background;
        var image = background is null ? null : DocumentRegistry.Image(background);

        return new(
            page.Number,
            [with([.. page.Strokes.Select(s => s.Clone( ))])],
            image,
            background?.Box,
            BoardState.Underlay.ToColor( ));
    }
}
