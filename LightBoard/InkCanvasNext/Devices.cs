using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
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

    /// <summary>测试接缝：暴露内部墨迹画布（触摸事件处理目标，供 InkCanvasNext.Tests 上报触摸）。</summary>
    internal InkCanvas InnerCanvasElement => InnerCanvas;

    internal int ActiveTouchCount => touches.Count;

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
        var position = e.GetTouchPoint(this).Position;
        TrackTouchDown(e.TouchDevice.Id, e.TouchDevice, position);
        SubscribeDeactivated(e.TouchDevice);
        UpdateState( );
        // 接管判定：手势接管态（BlocksNativeInput）、面积擦叠加态（IsAreaEraserActive）、
        // 盖章模式的单指上下文（EvalDraw/Draw）都拦下事件，不让原生收笔/移动选区
        e.Handled = BlocksNativeInput(State) || IsAreaEraserActive(State)
            || (StampAction != StampAction.None && State is TouchState.EvalDraw or TouchState.Draw);

        if (State == TouchState.Selection)
        {
            // 选区手势中新落的手指同样要捕获：CaptureAll 只在进入 Selection 时执行一次，
            // 未捕获的手指移出画布边界即触发 TouchLeave 而被移除
            e.TouchDevice.Capture(InnerCanvas);
        }
        else if (State == TouchState.MultiDraw)
        {
            // 形状已由 SetState 钩子取消（离开 EvalDraw/Draw 进入手势接管态），此处无需重复处理
            e.TouchDevice.Capture(InnerCanvas);
            if (!multiTouchStrokes.ContainsKey(e.TouchDevice.Id))
            {
                var canvasPos = e.GetTouchPoint(InnerCanvas).Position;
                StartMultiTouchStroke(e.TouchDevice.Id, canvasPos);
            }

            e.Handled = true;
        }
        else if (IsShapeMode && State == TouchState.EvalDraw && !shapeActive)
        {
            var canvasPos = e.GetTouchPoint(InnerCanvas).Position;
            StartShape(canvasPos);
            e.TouchDevice.Capture(InnerCanvas);
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

        if (multiTouchStrokes.ContainsKey(e.TouchDevice.Id))
        {
            var canvasPos = e.GetTouchPoint(InnerCanvas).Position;
            ContinueMultiTouchStroke(e.TouchDevice.Id, canvasPos);
            e.Handled = true;
            return;
        }

        // 路由只由状态机决定：形状在途时仅当状态仍在单指绘制上下文（EvalDraw/Draw）才更新预览，
        // 一旦状态机迁入手势接管态（PanZoom/Pan/MultiDraw/Eraser/Selection），形状已被 SetState 取消，
        // Move 事件不会再被 shapeActive 抢占。
        switch (State)
        {
            case TouchState.EvalDraw:
                UpdateShapeIfActive(e);
                UpdateState( );
                break;

            case TouchState.Draw:
                UpdateShapeIfActive(e);
                break;

            case TouchState.PanZoom: PanZoom( ); break;
            case TouchState.Pan: Pan( ); break;
            case TouchState.Selection: UpdateSelectionTouch( ); break;
        }

        // EvalDraw/Draw 保留未拦截（InkCanvas 原生收笔）；平移/缩放/选区由状态元数据接管；
        // MultiDraw 的 Move 已在上面多画笔画分支接管；盖章期间单指上下文全程拦截
        if (BlocksNativeInput(State) || IsAreaEraserActive(State)
            || (StampAction != StampAction.None && State is TouchState.EvalDraw or TouchState.Draw))
        {
            e.Handled = true;
        }

        TouchEpilogue( );
    }

    /// <summary>形状模式下的 EvalDraw/Draw 路由：形状在途时用终点触点更新预览并拦截事件；
    /// 形状已被状态机取消（手势接管）或非形状模式时为空操作，让原生 InkCanvas 收笔。</summary>
    private void UpdateShapeIfActive(TouchEventArgs e)
    {
        if (!shapeActive)
        {
            return;
        }

        UpdateShape(GetShapeEndPoint( ));
        e.Handled = true;
    }

    private void CanvasPreviewTouchUp(object o, TouchEventArgs e)
    {
        var wasHandled = State is TouchState.PanZoom or TouchState.Pan;
        var wasAreaEraser = IsAreaEraserActive(State);
        var wasMultiTouch = multiTouchStrokes.ContainsKey(e.TouchDevice.Id);
        var wasManipulating = State == TouchState.Selection;

        if (wasMultiTouch)
        {
            EndMultiTouchStroke(e.TouchDevice.Id);
        }

        // 形状只允许在单指绘制上下文（EvalDraw/Draw）存活：仅当手势未被状态机接管
        // （未迁入平移/缩放/多指等）时才提交，否则由 SetState 已取消、此处直接跳过。
        // 形状只由插入序第一指驱动：其余手指的移动与抬起均忽略，第一指抬起时按其触点提交
        if (shapeActive && State is TouchState.EvalDraw or TouchState.Draw && IsFirstTouch(e.TouchDevice.Id))
        {
            UpdateShape(e.GetTouchPoint(InnerCanvas).Position);
            CommitShape( );
            e.Handled = true;
        }

        // 盖章提交：未升级为手势的单指序列在其抬手位置落章
        if (stampArmed && State is TouchState.EvalDraw or TouchState.Draw
            && TryStamp(e.GetTouchPoint(InnerCanvas).Position))
        {
            e.Handled = true;
        }

        RemoveDevice(e.TouchDevice);
        e.Handled |= wasHandled || wasAreaEraser || wasMultiTouch || wasManipulating;
        TouchEpilogue( );
    }

    private void CanvasTouchLeave(object o, TouchEventArgs e)
    {
        if (releasingCaptures || e.TouchDevice.Captured == InnerCanvas)
        {
            return;
        }

        RemoveDevice(e.TouchDevice);
        TouchEpilogue( );
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void CaptureAll( )
    {
        foreach ((var Device, _) in touches.Values)
        {
            Device.Capture(InnerCanvas);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
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

        if (State is TouchState.Pan or TouchState.PanZoom)
        {
            InitGesture( );
        }
        else if (State == TouchState.Selection)
        {
            RefreshSelectionGesture( );
        }
    }

    private void TrackTouchUp(int id)
    {
        touches.Remove(id);
        touchStarts.Remove(id);

        if (State is TouchState.Pan or TouchState.PanZoom)
        {
            InitGesture( );
        }
        else if (State == TouchState.Selection)
        {
            RefreshSelectionGesture( );
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

        if (TryStamp(e.GetPosition(InnerCanvas)))
        {
            e.Handled = true;
            return;
        }

        if (IsShapeMode && !shapeActive)
        {
            StartShape(e.GetPosition(InnerCanvas));
            e.Handled = true;
            return;
        }

        if (Mode == InkCanvasNextMode.Select)
        {
            BeginMouseSelection(e.GetPosition(InnerCanvas));
            InnerCanvas.CaptureMouse( );
            e.Handled = true;
            return;
        }

        if (Mode != InkCanvasNextMode.EraseArea)
        {
            return;
        }

        var screenPosition = e.GetPosition(this);
        var canvasPosition = e.GetPosition(InnerCanvas);
        eraser.Diameter = EraserDiameter;
        eraser.Scale = CurrentScale;
        eraser.Show(screenPosition);
        eraser.Start(canvasPosition);
        e.Handled = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void CanvasPreviewMouseMove(object o, MouseEventArgs e)
    {
        if (e.StylusDevice != null)
        {
            return;
        }

        if (shapeActive)
        {
            UpdateShape(e.GetPosition(InnerCanvas));
            e.Handled = true;
            return;
        }

        if (Mode == InkCanvasNextMode.Select && selectionGesture != SelectionGesture.None)
        {
            UpdateMouseSelection(e.GetPosition(InnerCanvas));
            e.Handled = true;
            return;
        }

        if (!eraser.Active)
        {
            return;
        }

        var screenPosition = e.GetPosition(this);
        var canvasPosition = e.GetPosition(InnerCanvas);
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
            UpdateShape(e.GetPosition(InnerCanvas));
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

        var step = ScrollStep * CurrentScale;
        CurrentView = CurrentView
            .Pan(new Vector(0, e.Delta > 0 ? -step : step))
            .Clamp(CanvasScroll.ScrollableWidth, CanvasScroll.ScrollableHeight);
    }

    /// <summary>在指定位置落章（克隆/粘贴），返回是否处于盖章模式（调用方据此 Handled）。
    /// 鼠标在按下时落章，触摸由未升级为手势的单指序列在抬手时落章。</summary>
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
