using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    private readonly double touchDisplThreshold = 20.0;

    private const double ScrollStep = 25;

    /// <summary>
    /// 升级需注意：此处依赖 .NET Core 3.1+ 内部实现细节：<br />
    /// 1. 未调用 Remove/TrimExcess 时迭代顺序等同于插入顺序。<br />
    /// 2. 调用 Remove 后，只影响所移除元素后面的元素。<br />
    /// 3. 实际情况：防止跳变即可，因此允许 Hack。<br />
    /// 4. 变通方案：OrderedDictionary、手动维护前两根手指。
    /// </summary>
    private readonly OrderedDictionary<int, (TouchDevice Device, Point Position)> touches = [with(20)];
    private readonly Dictionary<int, Point> touchStarts = [];
    private bool releasingCaptures;

    /// <summary>
    /// 重置触摸输入状态：释放全部触摸捕获、清空触点并取消进行中的手势。
    /// </summary>
    public void ResetTouchState( )
    {
        releasingCaptures = true;
        try
        {
            foreach ((var Device, _) in touches.Values)
            {
                UnsubscribeDeactivated(Device);
                Device.Capture(null);
            }

            touches.Clear( );
            touchStarts.Clear( );
            SetState(TouchState.Idle);
            CancelSelectionGesture( );
            CancelShape( );
        }
        finally
        {
            releasingCaptures = false;
        }
    }

    /// <summary>触摸事件尾声（Down/Move/Up/LostTouchCapture/TouchLeave 必经）：
    /// 触点事实已更新，橡皮视觉随之刷新；离开擦除态的结算由 SetState 通用钩子完成。</summary>
    private void TouchEpilogue( )
    {
        UpdateAreaEraser( );
    }

    private void CanvasLostTouchCapture(object o, TouchEventArgs e)
    {
        if (releasingCaptures)
        {
            return;
        }

        RemoveDevice(e.TouchDevice);
        TouchEpilogue( );
    }

    private void CanvasPreviewTouchDown(object o, TouchEventArgs e)
    {
        if (TryStamp(e.GetTouchPoint(Canvas).Position))
        {
            e.Handled = true;
            return;
        }

        var position = e.GetTouchPoint(this).Position;
        TrackTouchDown(e.TouchDevice.Id, e.TouchDevice, position);
        SubscribeDeactivated(e.TouchDevice);
        UpdateState( );
        // 由状态元数据判定是否接管（原 UpdateState 返回值 + 区域擦除/选区三条件并联）
        e.Handled = BlocksNativeInput(state) || IsAreaEraserActive(state);

        if (state == TouchState.MultiDraw)
        {
            if (shapeActive)
            {
                CancelShape( );
            }

            e.TouchDevice.Capture(Canvas);
            if (!multiTouchStrokes.ContainsKey(e.TouchDevice.Id))
            {
                var canvasPos = e.GetTouchPoint(Canvas).Position;
                StartMultiTouchStroke(e.TouchDevice.Id, canvasPos);
            }

            e.Handled = true;
        }
        else if (IsShapeMode && state == TouchState.EvalDraw && !shapeActive)
        {
            var canvasPos = e.GetTouchPoint(Canvas).Position;
            StartShape(canvasPos);
            e.TouchDevice.Capture(Canvas);
            e.Handled = true;
        }

        TouchEpilogue( );
    }

    private void CanvasPreviewTouchMove(object o, TouchEventArgs e)
    {
        if (!touches.ContainsKey(e.TouchDevice.Id))
        {
            return;
        }

        touches[e.TouchDevice.Id] = (e.TouchDevice, e.GetTouchPoint(this).Position);

        if (shapeActive)
        {
            var canvasPos = e.GetTouchPoint(Canvas).Position;
            UpdateShape(canvasPos);
            e.Handled = true;
            if (state == TouchState.EvalDraw)
            {
                UpdateState( );
            }

            return;
        }

        if (multiTouchStrokes.ContainsKey(e.TouchDevice.Id))
        {
            var canvasPos = e.GetTouchPoint(Canvas).Position;
            ContinueMultiTouchStroke(e.TouchDevice.Id, canvasPos);
            e.Handled = true;
            return;
        }

        switch (state)
        {
            case TouchState.EvalDraw: UpdateState( ); break;
            case TouchState.PanZoom: PanZoom( ); break;
            case TouchState.Pan: Pan( ); break;
            case TouchState.Selection: UpdateSelectionTouch( ); break;
        }

        // EvalDraw/Draw 保留未拦截（InkCanvas 原生收笔）；平移/缩放/选区由状态元数据接管；
        // MultiDraw 的 Move 已在上面多画笔画分支接管
        if (BlocksNativeInput(state) || IsAreaEraserActive(state))
        {
            e.Handled = true;
        }

        TouchEpilogue( );
    }

    private void CanvasPreviewTouchUp(object o, TouchEventArgs e)
    {
        var wasHandled = state is TouchState.PanZoom or TouchState.Pan;
        var wasAreaEraser = IsAreaEraserActive(state);
        var wasMultiTouch = multiTouchStrokes.ContainsKey(e.TouchDevice.Id);
        var wasManipulating = state == TouchState.Selection;

        if (wasMultiTouch)
        {
            EndMultiTouchStroke(e.TouchDevice.Id);
        }

        if (shapeActive)
        {
            var canvasPos = e.GetTouchPoint(Canvas).Position;
            UpdateShape(canvasPos);
            CommitShape( );
            e.Handled = true;
        }

        RemoveDevice(e.TouchDevice);
        e.Handled = wasHandled || wasAreaEraser || wasMultiTouch || wasManipulating;
        TouchEpilogue( );
    }

    private void CanvasTouchLeave(object o, TouchEventArgs e)
    {
        if (e.TouchDevice.Captured == Canvas)
        {
            return;
        }

        RemoveDevice(e.TouchDevice);
        TouchEpilogue( );
    }

    private void CaptureAll( )
    {
        foreach ((var Device, _) in touches.Values)
        {
            Device.Capture(Canvas);
        }
    }

    private void ReleaseAll( )
    {
        releasingCaptures = true;
        try
        {
            foreach ((var Device, _) in touches.Values)
            {
                Device.Capture(null);
            }
        }
        finally
        {
            releasingCaptures = false;
        }
    }

    private void RemoveDevice(TouchDevice device)
    {
        if (touches.ContainsKey(device.Id))
        {
            TrackTouchUp(device.Id);
            UnsubscribeDeactivated(device);
            UpdateState( );
        }
    }
    private void SubscribeDeactivated(TouchDevice device)
    {
        device.Deactivated -= TouchDeviceDeactivated;
        device.Deactivated += TouchDeviceDeactivated;
    }

    private void TouchDeviceDeactivated(object? o, EventArgs e)
    {
        if (o is not TouchDevice device)
        {
            return;
        }

        RemoveDevice(device);
    }

    private void TrackTouchDown(int id, TouchDevice device, Point position)
    {
        touches[id] = (device, position);
        touchStarts[id] = position;

        if (state is TouchState.Pan or TouchState.PanZoom)
        {
            InitGesture( );
        }
    }

    private void TrackTouchUp(int id)
    {
        touches.Remove(id);
        touchStarts.Remove(id);

        if (state is TouchState.Pan or TouchState.PanZoom)
        {
            InitGesture( );
        }
    }

    private void UnsubscribeDeactivated(TouchDevice device)
    {
        device.Deactivated -= TouchDeviceDeactivated;
    }

    private void CanvasPreviewMouseDown(object o, MouseButtonEventArgs e)
    {
        if (e.StylusDevice != null)
        {
            return;
        }

        if (TryStamp(e.GetPosition(Canvas)))
        {
            e.Handled = true;
            return;
        }

        if (IsShapeMode && !shapeActive)
        {
            StartShape(e.GetPosition(Canvas));
            e.Handled = true;
            return;
        }

        if (Mode == InkCanvasNextMode.Select)
        {
            BeginMouseSelection(e.GetPosition(Canvas));
            Canvas.CaptureMouse( );
            e.Handled = true;
            return;
        }

        if (Mode != InkCanvasNextMode.EraseArea)
        {
            return;
        }

        var screenPosition = e.GetPosition(this);
        var canvasPosition = e.GetPosition(Canvas);
        eraser.Diameter = EraserDiameter;
        eraser.Scale = currentScale;
        eraser.Show(screenPosition);
        eraser.Start(canvasPosition);
        e.Handled = true;
    }

    private void CanvasPreviewMouseMove(object o, MouseEventArgs e)
    {
        if (e.StylusDevice != null)
        {
            return;
        }

        if (shapeActive)
        {
            UpdateShape(e.GetPosition(Canvas));
            e.Handled = true;
            return;
        }

        if (Mode == InkCanvasNextMode.Select && selectionGesture != SelectionGesture.None)
        {
            UpdateMouseSelection(e.GetPosition(Canvas));
            e.Handled = true;
            return;
        }

        if (!eraser.Active)
        {
            return;
        }

        var screenPosition = e.GetPosition(this);
        var canvasPosition = e.GetPosition(Canvas);
        eraser.Show(screenPosition);
        eraser.Move(canvasPosition);
        e.Handled = true;
    }

    private void CanvasPreviewMouseUp(object o, MouseButtonEventArgs e)
    {
        if (e.StylusDevice != null)
        {
            return;
        }

        if (shapeActive)
        {
            UpdateShape(e.GetPosition(Canvas));
            CommitShape( );
            e.Handled = true;
            return;
        }

        if (Mode == InkCanvasNextMode.Select && selectionGesture != SelectionGesture.None)
        {
            EndMouseSelection( );
            e.Handled = true;
            return;
        }

        if (!eraser.Active)
        {
            return;
        }

        EndEraserCycle( );
        e.Handled = true;
    }

    private void CanvasPreviewMouseWheel(object o, MouseWheelEventArgs e)
    {
        if (MouseWheelAction == MouseWheelAction.None)
        {
            return;
        }

        e.Handled = true;

        if (MouseWheelAction == MouseWheelAction.Zoom || Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            ZoomAtCursor(e);
            return;
        }

        var step = ScrollStep * currentScale;
        var newOffset = CanvasScroll.VerticalOffset + (e.Delta > 0 ? -step : step);
        CanvasScroll.ScrollToVerticalOffset(Math.Clamp(newOffset, 0, CanvasScroll.ScrollableHeight));
    }

    private double GetMaxDistance2( )
    {
        var count = touches.Count;
        if (count < 2)
        {
            return 0;
        }

        Span<Point> positions = stackalloc Point[count];
        var index = 0;
        foreach (var kv in touches)
        {
            positions[index++] = kv.Value.Position;
        }

        double max = 0;
        for (var i = 0; i < count - 1; i++)
        {
            for (var j = i + 1; j < count; j++)
            {
                var d2 = Geometry.Distance2(positions[i], positions[j]);
                if (d2 > max)
                {
                    max = d2;
                }
            }
        }

        return max;
    }

    private double Get1stFingerDispl2( )
    {
        if (touches.Count == 0)
        {
            return 0;
        }

        var first = touches.First( );
        if (!touchStarts.TryGetValue(first.Key, out var start))
        {
            return 0;
        }

        var current = first.Value.Position;
        return Geometry.Distance2(current, start);
    }

    /// <summary>盖章模式拦截：命中则盖章并返回 true（调用方据此 Handled 并短路后续路由）。</summary>
    private bool TryStamp(Point point)
    {
        if (StampAction == StampAction.None)
        {
            return false;
        }

        if (StampAction == StampAction.Clone)
        {
            StampCloneAt(point);
        }
        else if (StampAction == StampAction.Paste)
        {
            StampPasteAt(point);
        }

        return true;
    }
}
