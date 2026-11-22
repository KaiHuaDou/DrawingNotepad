using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

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
    Rectangle,
    Pinch
}

public partial class InkCanvasNext
{
    private SelectionGesture selectionGesture = SelectionGesture.None;
    private SelectionHandle selectionHandle = SelectionHandle.None;

    /// <summary>
    /// 驱动指的触点 id：拖动时取位于选区内（或压在手柄上）的第一根手指，全部在外时退回第一根
    /// </summary>
    private int? SelectionDriverId { get; set; }

    /// <summary>
    /// 路径手势：在途时只累积套索或矩形，不产生变换
    /// </summary>
    private bool IsPathGesture => selectionGesture is SelectionGesture.Lasso or SelectionGesture.Rectangle;

    private bool selectionHaloPending;

    /// <summary>
    /// 旋转手势中手柄的轨道位置（内容坐标，绕选区中心等半径跟随鼠标角度）；空闲为 null
    /// </summary>
    internal Point? RotateHandleLive { get; private set; }

    // 触点与选区位置

    /// <summary>
    /// 触点与选区的位置关系：AllInside 全部落在选区内，Mixed 横跨选区内外；无选区时两者皆 false（全部按选区外处理）
    /// </summary>
    private (bool AllInside, bool Mixed) ClassifyTouchesAgainstSelection( )
    {
        if (!selection.HasSelection)
        {
            return (false, false);
        }

        var bounds = selection.Bounds;
        var inside = 0;
        var fingers = 0;
        foreach (var kv in touches)
        {
            if (kv.Value.IsPalm)
            {
                continue;
            }

            fingers++;

            // 触点存根可视坐标，选区包围盒是内容坐标：经 InnerCanvas 换算后比较
            if (bounds.Contains(kv.Value.Device.GetTouchPoint(InnerCanvas).Position))
            {
                inside++;
            }
        }

        return (inside == fingers, inside > 0 && inside < fingers);
    }

    // 选择手势（触摸与合流后的鼠标共用，配合 TouchState.Selection）

    private void BeginSelectionTouch( )
    {
        ResetSelectionGesture( );

        var fingerCount = CountTouches(touches).Fingers;
        if (fingerCount == 0)
        {
            return;
        }

        if (!selection.HasSelection)
        {
            var canvasPos = GetFirstCanvasPoint(touches, InnerCanvas);
            selectionStartPoint = canvasPos;
            BeginSelectionDrag(canvasPos);
            return;
        }

        // 两指全部落在选区内才是相似变换；横跨边界时由在内的第一根手指拖动
        if (fingerCount >= 2 && ClassifyTouchesAgainstSelection( ).AllInside)
        {
            BeginPinch( );
            return;
        }

        var (driverId, driver) = FindSelectionDriver( );
        SelectionDriverId = driverId;
        selectionStartPoint = driver;
        BeginHandleOrMove(driver);
    }

    private void UpdateSelectionTouch( )
    {
        switch (selectionGesture)
        {
            case SelectionGesture.Lasso:
                UpdateLassoTrack(GetFirstCanvasPoint(touches, InnerCanvas));
                break;

            case SelectionGesture.Rectangle:
                UpdateRectangleTrack(GetFirstCanvasPoint(touches, InnerCanvas));
                break;

            // 变换类手势按帧合并：位移在下一帧统一施加，事件率不参与每帧的重建次数
            case SelectionGesture.Pinch:
            case SelectionGesture.Move:
            case SelectionGesture.Scale:
            case SelectionGesture.Rotate:
                RequestSelectionFrame( );
                break;
        }
    }

    private void EndSelectionTouch( )
    {
        // 迁出可能发生在合并中的位移之后：按当前触点结清，不能只摘钩
        FlushSelectionFrame( );

        var transformed = selectionGesture is SelectionGesture.Move or SelectionGesture.Scale or SelectionGesture.Rotate or SelectionGesture.Pinch;
        var path = IsPathGesture;

        if (transformed)
        {
            CommitTransform( );
        }
        else if (selectionGesture == SelectionGesture.Lasso)
        {
            EndLasso( );
        }
        else if (selectionGesture == SelectionGesture.Rectangle)
        {
            EndRectangle( );
        }

        selectionGesture = SelectionGesture.None;
        selectionTarget = null;
        SelectionDriverId = null;
        RotateHandleLive = null;

        if (transformed)
        {
            selection.Invalidate(halo: false);
        }

        // 逐笔克隆的 halo 留到空闲时补，变换与路径手势同理
        if (transformed || path)
        {
            ScheduleSelectionHalo( );
        }
    }

    /// <summary>
    /// 手势结束后补画 halo：逐笔克隆的代价在 100 笔以上是 10 ms 级，放到空闲优先级，避开抬手这一帧
    /// </summary>
    private void ScheduleSelectionHalo( )
    {
        if (selectionHaloPending)
        {
            return;
        }

        selectionHaloPending = true;
        Dispatcher.InvokeAsync(( ) =>
        {
            selectionHaloPending = false;
            if (selection.HasSelection && selectionGesture == SelectionGesture.None && State != TouchState.Selection)
            {
                selection.Invalidate( );
            }
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// 选区手势中全部触点（含手掌触点）都在选区外时才允许手掌接管
    /// </summary>
    private bool AllContactsOutsideSelection( )
    {
        if (!selection.HasSelection)
        {
            return true;
        }

        var bounds = selection.Bounds;

        foreach (var contact in touches.Values)
        {
            if (bounds.Contains(contact.Device.GetTouchPoint(InnerCanvas).Position))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 单指起点判定：命中手柄 → 缩放手柄/旋转手柄；选区内 → 移动；选区外 → 清空选型并套索
    /// </summary>
    private void BeginHandleOrMove(Point canvasPos)
    {
        var handle = HitTestHandle(canvasPos);
        var inBounds = selection.Bounds.Contains(canvasPos);

        if (handle == SelectionHandle.None && !inBounds)
        {
            BeginSelectionDrag(canvasPos);
            return;
        }

        selectionTarget = selection.SelectedStrokesArray;
        selectionBoundsBase = selection.Bounds;

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

    // 手柄命中

    private SelectionHandle HitTestHandle(Point p)
    {
        var b = selection.Bounds;
        if (b.IsEmpty)
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

    // 手势生命周期

    private void ResetSelectionGesture( )
    {
        selectionGesture = SelectionGesture.None;
        selectionHandle = SelectionHandle.None;
        selectionTarget = null;
        SelectionDriverId = null;
        selectionAbs = Matrix.Identity;
        selectionTotal = Matrix.Identity;
        selectionStartPoint = default;
        RotateHandleLive = null;

        UnhookSelectionFrame( );

        lassoPath.Clear( );
        lassoSelected.Clear( );
        rectangleCorner = default;
        rectangleDragged = false;

        if (lassoTester is not null)
        {
            lassoTester.SelectionChanged -= OnLassoSelectionChanged;
            lassoTester.EndHitTesting( );
            lassoTester = null;
        }
    }

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
