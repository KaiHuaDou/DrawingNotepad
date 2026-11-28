using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace InkCanvasNext;

// 选择手势的入口与状态：触点分类、开始/更新/结束与 halo 调度

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
    /// 驱动指的触点 id
    /// </summary>
    private int? SelectionDriverId { get; set; }

    private bool IsPathGesture => selectionGesture is SelectionGesture.Lasso or SelectionGesture.Rectangle;

    private bool selectionHaloPending;

    /// <summary>
    /// 旋转手柄的轨道位置，内容坐标；空闲为 null
    /// </summary>
    internal Point? RotateHandleLive { get; private set; }

    // 触点与选区位置

    /// <summary>
    /// 触点与选区的位置关系；无选区时两者皆 false
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

            // 触点存可视坐标，包围盒是内容坐标：经 InnerCanvas 换算后比较
            if (bounds.Contains(kv.Value.Device.GetTouchPoint(InnerCanvas).Position))
            {
                inside++;
            }
        }

        return (inside == fingers, inside > 0 && inside < fingers);
    }

    // 选择手势：触摸与合成触点共用

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

        // 两指全部落在选区内才是相似变换，横跨边界时由在内的第一根手指拖动
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

            // 变换类手势按帧合并，事件率不参与每帧重建次数
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

        if (transformed || path)
        {
            ScheduleSelectionHalo( );
        }
    }

    /// <summary>
    /// 逐笔克隆的 halo 放到空闲优先级，避开抬手这一帧
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
    /// 含手掌触点在内，全部在选区外才允许手掌接管
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

    // 起手与点选

    private void BeginSelectionDrag(Point p)
    {
        if (options.SelectionDragMode == SelectionDragMode.Rectangle)
        {
            BeginRectangle(p);
        }
        else
        {
            BeginLasso(p);
        }
    }

    private void HandlePointSelect(Point p, bool halo = true)
    {
        var hit = InnerCanvas.Strokes.HitTest(p);
        if (hit.Count > 0)
        {
            selection.SetStrokes(hit, halo);
        }
        else
        {
            selection.Clear( );
        }
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
