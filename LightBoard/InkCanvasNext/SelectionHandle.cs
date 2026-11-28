using System.Windows;

using static System.Math;
using static InkCanvasNext.Geometry;

namespace InkCanvasNext;

// 手柄标识、命中半径与缩放锚点

internal enum SelectionHandle
{
    None,
    TL,
    TR,
    BL,
    BR,
    T,
    B,
    L,
    R,
    Rotate
}

public partial class InkCanvasNext
{
    /// <summary>
    /// 严格内部：Rect.Contains 含边界，而手柄压在边界上，边界与外侧留给手柄
    /// </summary>
    private static bool IsStrictlyInside(Rect b, Point p)
    {
        return p.X > b.Left && p.X < b.Right && p.Y > b.Top && p.Y < b.Bottom;
    }

    private SelectionHandle HitTestHandle(Point p)
    {
        var b = selection.Bounds;
        if (b.IsEmpty || IsStrictlyInside(b, p))
        {
            return SelectionHandle.None;
        }

        // 命中半径为视口 DIP：除以缩放转内容坐标比较，保持屏幕绝对大小
        var scale = Max(CurrentScale, 1e-6);
        if (Near(p, SelectionVisual.RotateHandleCenter(b, scale), RotateHitScreenRadius / scale))
        {
            return SelectionHandle.Rotate;
        }
        else if (Near(p, b.TopLeft, HandleHitRadius / scale))
        {
            return SelectionHandle.TL;
        }
        else if (Near(p, b.TopRight, HandleHitRadius / scale))
        {
            return SelectionHandle.TR;
        }
        else if (Near(p, b.BottomLeft, HandleHitRadius / scale))
        {
            return SelectionHandle.BL;
        }
        else if (Near(p, b.BottomRight, HandleHitRadius / scale))
        {
            return SelectionHandle.BR;
        }
        else if (Near(p, new Point(b.Left + b.Width / 2, b.Top), HandleHitRadius / scale))
        {
            return SelectionHandle.T;
        }
        else if (Near(p, new Point(b.Left + b.Width / 2, b.Bottom), HandleHitRadius / scale))
        {
            return SelectionHandle.B;
        }
        else if (Near(p, new Point(b.Left, b.Top + b.Height / 2), HandleHitRadius / scale))
        {
            return SelectionHandle.L;
        }
        else if (Near(p, new Point(b.Right, b.Top + b.Height / 2), HandleHitRadius / scale))
        {
            return SelectionHandle.R;
        }
        else
        {
            return SelectionHandle.None;
        }
    }

    private static Point GetAnchorFor(SelectionHandle handle, Rect b)
    {
        var cx = b.Left + b.Width / 2;
        var cy = b.Top + b.Height / 2;
        return handle switch
        {
            SelectionHandle.TL => b.BottomRight,
            SelectionHandle.TR => b.BottomLeft,
            SelectionHandle.BL => b.TopRight,
            SelectionHandle.BR => b.TopLeft,
            SelectionHandle.T => new Point(cx, b.Bottom),
            SelectionHandle.B => new Point(cx, b.Top),
            SelectionHandle.L => new Point(b.Right, cy),
            SelectionHandle.R => new Point(b.Left, cy),
            _ => new Point(cx, cy)
        };
    }
}
