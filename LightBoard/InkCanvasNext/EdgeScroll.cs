using System.Collections.Generic;
using System.Windows;

namespace InkCanvasNext;

/// <summary>
/// 视口四侧的边缘热区
/// </summary>
public enum EdgeScrollSide
{
    /// <summary>
    /// 上侧
    /// </summary>
    Top,

    /// <summary>
    /// 下侧
    /// </summary>
    Bottom,

    /// <summary>
    /// 左侧
    /// </summary>
    Left,

    /// <summary>
    /// 右侧
    /// </summary>
    Right
}

public partial class InkCanvasNext
{
    /// <summary>
    /// 热区在内容坐标中的矩形：深度按视口 DIP 折算，随缩放变化
    /// </summary>
    internal static Rect EdgeBandContentRect(View view, Size viewport, double threshold, EdgeScrollSide side)
    {
        var rect = view.ContentRect(viewport);
        if (rect.IsEmpty || threshold <= 0)
        {
            return Rect.Empty;
        }

        var depth = threshold / view.Scale;

        return side switch
        {
            EdgeScrollSide.Top => new Rect(rect.Left, rect.Top, rect.Width, depth),
            EdgeScrollSide.Bottom => new Rect(rect.Left, rect.Top + rect.Height - depth, rect.Width, depth),
            EdgeScrollSide.Left => new Rect(rect.Left, rect.Top, depth, rect.Height),
            _ => new Rect(rect.Left + rect.Width - depth, rect.Top, depth, rect.Height),
        };
    }

    /// <summary>
    /// 包围盒与热区严格相交，边界相接不算
    /// </summary>
    internal static bool BandHasInk(IReadOnlyList<Rect> bounds, Rect band)
    {
        foreach (var b in bounds)
        {
            if (b.Left < band.Right && b.Right > band.Left && b.Top < band.Bottom && b.Bottom > band.Top)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 沿该侧方向把热区移到无墨迹处所需的最小内容位移，0 表示热区本来就空/ 只统计在另一轴上与热区相交的包围盒：上下热区横跨视口宽度，视口左右之外的笔画不参与
    /// </summary>
    internal static double BandClearDelta(IReadOnlyList<Rect> bounds, Rect band, EdgeScrollSide side)
    {
        if (band.IsEmpty)
        {
            return 0;
        }

        var alongX = side is EdgeScrollSide.Left or EdgeScrollSide.Right;
        var forward = side is EdgeScrollSide.Bottom or EdgeScrollSide.Right;
        var start = alongX ? band.Left : band.Top;
        var length = alongX ? band.Width : band.Height;
        var crossStart = alongX ? band.Top : band.Left;
        var crossEnd = alongX ? band.Bottom : band.Right;

        // 取所有与热区相交的笔画所需位移的最大值：位移到最远那条笔画的边界时，
        // 其余笔画都在反方向之外，一次遍历即可，结果与逐笔推进到不再相交相同
        var delta = 0.0;
        foreach (var b in bounds)
        {
            var crossFrom = alongX ? b.Top : b.Left;
            var crossTo = alongX ? b.Bottom : b.Right;
            if (crossFrom >= crossEnd || crossTo <= crossStart)
            {
                continue;
            }

            var from = alongX ? b.Left : b.Top;
            var to = alongX ? b.Right : b.Bottom;
            if (from >= start + length || to <= start)
            {
                continue;
            }

            var shift = forward ? to - start : start + length - from;
            if (shift > delta)
            {
                delta = shift;
            }
        }

        return delta;
    }
}
