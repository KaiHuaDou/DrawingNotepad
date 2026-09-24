using System.Windows;
using System.Windows.Media;

using static System.Math;
using static InkCanvasNext.Geometry;

namespace InkCanvasNext;

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

internal enum SelectionGesture
{
    None,
    Move,
    Scale,
    Rotate,
    Lasso,
    Pinch
}

public partial class InkCanvasNext
{
    private SelectionGesture selectionGesture = SelectionGesture.None;
    private SelectionHandle selectionHandle = SelectionHandle.None;

    /// <summary>
    /// 旋转手势中手柄的轨道位置（内容坐标，绕选区中心等半径跟随鼠标角度）；空闲为 null。
    /// </summary>
    internal Point? RotateHandleLive { get; private set; }

    // ---------- 鼠标选择 ----------

    private void BeginMouseSelection(Point p)
    {
        ResetSelectionGesture( );
        selectionStartPoint = p;

        if (selection.HasSelection)
        {
            var handle = HitTestHandle(p);
            if (handle != SelectionHandle.None)
            {
                selectionHandle = handle;
                selectionGesture = handle == SelectionHandle.Rotate ? SelectionGesture.Rotate : SelectionGesture.Scale;
                var b = selection.Bounds;
                selectionAnchor = GetAnchorFor(handle, b);
                selectionCenter = new Point(b.Left + b.Width / 2, b.Top + b.Height / 2);
                selectionTarget = [with(selection.SelectedStrokes)];
                return;
            }

            if (selection.Bounds.Contains(p))
            {
                selectionGesture = SelectionGesture.Move;
                selectionTarget = [with(selection.SelectedStrokes)];
                return;
            }
        }

        // 空白处按下：清空选型并开始套索
        BeginLasso(p);
    }

    private void UpdateMouseSelection(Point p)
    {
        switch (selectionGesture)
        {
            case SelectionGesture.Move:
            case SelectionGesture.Scale:
            case SelectionGesture.Rotate:
                UpdateSelectionTransform(p);
                break;

            case SelectionGesture.Lasso:
                UpdateLassoTrack(p);
                break;
        }
    }

    private void EndMouseSelection( )
    {
        switch (selectionGesture)
        {
            case SelectionGesture.Move:
            case SelectionGesture.Scale:
            case SelectionGesture.Rotate:
                CommitTransform( );
                break;

            case SelectionGesture.Lasso:
                EndLasso( );
                break;
        }

        selectionGesture = SelectionGesture.None;
        selectionTarget = null;
        RotateHandleLive = null;
        InnerCanvas.ReleaseMouseCapture( );
        selection.Invalidate( );
        RaiseViewOrSelectionChanged( );
    }

    // ---------- 触屏选择（配合 TouchState.Selection） ----------

    private void BeginSelectionTouch( )
    {
        ResetSelectionGesture( );
        if (touches.Count == 0)
        {
            return;
        }

        // touches 保插入序：手势起始指在存活期间恒为 First()，抬起后由剩余手指自然接替
        var canvasPos = GetFirstCanvasPoint( );
        selectionStartPoint = canvasPos;

        if (!selection.HasSelection)
        {
            BeginLasso(canvasPos);
            return;
        }

        if (touches.Count >= 2)
        {
            BeginPinch( );
            return;
        }

        BeginHandleOrMove(canvasPos);
    }

    private void UpdateSelectionTouch( )
    {
        switch (selectionGesture)
        {
            case SelectionGesture.Lasso:
                UpdateLassoTrack(GetFirstCanvasPoint( ));
                break;

            case SelectionGesture.Pinch:
                UpdatePinch( );
                break;

            case SelectionGesture.Move:
            case SelectionGesture.Scale:
            case SelectionGesture.Rotate:
                UpdateSelectionTransform(GetFirstCanvasPoint( ));
                break;
        }
    }

    private void EndSelectionTouch( )
    {
        if (selectionGesture is SelectionGesture.Move or SelectionGesture.Scale or SelectionGesture.Rotate or SelectionGesture.Pinch)
        {
            CommitTransform( );
        }
        else if (selectionGesture == SelectionGesture.Lasso)
        {
            EndLasso( );
        }

        selectionGesture = SelectionGesture.None;
        selectionTarget = null;
        RotateHandleLive = null;
        selection.Invalidate( );
    }

    /// <summary>
    /// 单指起点判定：命中手柄 → 缩放手柄/旋转手柄；选区内 → 移动；选区外 → 清空选型并套索。
    /// </summary>
    private void BeginHandleOrMove(Point canvasPos)
    {
        var handle = HitTestHandle(canvasPos);
        var inBounds = selection.Bounds.Contains(canvasPos);

        if (handle == SelectionHandle.None && !inBounds)
        {
            BeginLasso(canvasPos);
            return;
        }

        selectionTarget = [with(selection.SelectedStrokes)];

        if (handle == SelectionHandle.None)
        {
            selectionGesture = SelectionGesture.Move;
            return;
        }

        selectionGesture = handle == SelectionHandle.Rotate ? SelectionGesture.Rotate : SelectionGesture.Scale;
        selectionHandle = handle;
        var bounds = selection.Bounds;
        selectionAnchor = GetAnchorFor(handle, bounds);
        selectionCenter = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
    }

    // ---------- 手柄命中 ----------

    private SelectionHandle HitTestHandle(Point p)
    {
        var b = selection.Bounds;
        if (b.IsEmpty)
        {
            return SelectionHandle.None;
        }

        if (Near(p, SelectionVisual.RotateHandleCenter(b, currentScale), RotateHitRadius / Max(currentScale, 1e-6)))
        {
            return SelectionHandle.Rotate;
        }
        else if (Near(p, b.TopLeft, HandleHitRadius))
        {
            return SelectionHandle.TL;
        }
        else if (Near(p, b.TopRight, HandleHitRadius))
        {
            return SelectionHandle.TR;
        }
        else if (Near(p, b.BottomLeft, HandleHitRadius))
        {
            return SelectionHandle.BL;
        }
        else if (Near(p, b.BottomRight, HandleHitRadius))
        {
            return SelectionHandle.BR;
        }
        else if (Near(p, new Point(b.Left + b.Width / 2, b.Top), HandleHitRadius))
        {
            return SelectionHandle.T;
        }
        else if (Near(p, new Point(b.Left + b.Width / 2, b.Bottom), HandleHitRadius))
        {
            return SelectionHandle.B;
        }
        else if (Near(p, new Point(b.Left, b.Top + b.Height / 2), HandleHitRadius))
        {
            return SelectionHandle.L;
        }
        else if (Near(p, new Point(b.Right, b.Top + b.Height / 2), HandleHitRadius))
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

    // ---------- 手势生命周期 ----------

    private void ResetSelectionGesture( )
    {
        selectionGesture = SelectionGesture.None;
        selectionHandle = SelectionHandle.None;
        selectionTarget = null;
        selectionAbs = Matrix.Identity;
        selectionTotal = Matrix.Identity;
        selectionStartPoint = default;
        RotateHandleLive = null;

        lassoPath.Clear( );
        lassoSelected.Clear( );
        lassoDragged = false;

        if (lassoTester is not null)
        {
            lassoTester.SelectionChanged -= OnLassoSelectionChanged;
            lassoTester.EndHitTesting( );
            lassoTester = null;
        }
    }

    /// <summary>取消进行中的鼠标选择手势（不记录变换），并释放捕获。用于离开 Select 模式/重置输入。</summary>
    private void CancelSelectionGesture( )
    {
        if (selectionGesture == SelectionGesture.None)
        {
            return;
        }

        ResetSelectionGesture( );
        InnerCanvas.ReleaseMouseCapture( );
        selection.Invalidate( );
    }
}
