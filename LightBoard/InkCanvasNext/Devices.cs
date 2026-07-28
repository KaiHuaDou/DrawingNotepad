using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    private readonly double touchDisplThreshold = 20.0;

    /// <summary>
    /// 升级需注意：此处依赖 .NET Core 3.1+ 内部实现细节：<br />
    /// 1. 未调用 Remove/TrimExcess 时迭代顺序等同于插入顺序。<br />
    /// 2. 调用 Remove 后，只影响所移除元素后面的元素。<br />
    /// 3. 实际情况：防止跳变即可，因此允许 Hack。<br />
    /// 4. 变通方案：OrderDictionary、手动维护前两根手指。
    /// </summary>
    private readonly Dictionary<int, (TouchDevice Device, Point Position)> touches = new(20);
    private readonly Dictionary<int, Point> touchStarts = [];
    private bool releasingCaptures;

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
        }
        finally
        {
            releasingCaptures = false;
        }
    }

    private void CanvasLostTouchCapture(object o, TouchEventArgs e)
    {
        if (releasingCaptures)
        {
            return;
        }

        RemoveDevice(e.TouchDevice);
        UpdateAreaEraser( );
    }

    private void CanvasPreviewTouchDown(object o, TouchEventArgs e)
    {
        var position = e.GetTouchPoint(this).Position;
        TrackTouchDown(e.TouchDevice.Id, e.TouchDevice, position);
        SubscribeDeactivated(e.TouchDevice);
        e.Handled = UpdateState( ) || IsAreaEraserActive(state);

        if (state == TouchState.MultiDraw)
        {
            e.TouchDevice.Capture(Canvas);
            if (!multiTouchStrokes.ContainsKey(e.TouchDevice.Id))
            {
                var canvasPos = e.GetTouchPoint(Canvas).Position;
                StartMultiTouchStroke(e.TouchDevice.Id, canvasPos);
            }

            e.Handled = true;
        }

        UpdateAreaEraser( );
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
            var canvasPos = e.GetTouchPoint(Canvas).Position;
            ContinueMultiTouchStroke(e.TouchDevice.Id, canvasPos);
            e.Handled = true;
            return;
        }

        switch (state)
        {
            case TouchState.EvalDraw: UpdateState( ); break;
            case TouchState.PanZoom: PanZoom( ); e.Handled = true; break;
            case TouchState.Pan: Pan( ); e.Handled = true; break;
        }

        if (IsAreaEraserActive(state))
        {
            e.Handled = true;
        }

        UpdateAreaEraser( );
    }

    private void CanvasPreviewTouchUp(object o, TouchEventArgs e)
    {
        var wasHandled = state is TouchState.PanZoom or TouchState.Pan;
        var wasAreaEraser = IsAreaEraserActive(state);
        var wasMultiTouch = multiTouchStrokes.ContainsKey(e.TouchDevice.Id);

        if (wasMultiTouch)
        {
            EndMultiTouchStroke(e.TouchDevice.Id);
        }

        RemoveDevice(e.TouchDevice);
        e.Handled = wasHandled || wasAreaEraser || wasMultiTouch;
        UpdateAreaEraser( );
    }

    private void CanvasTouchLeave(object o, TouchEventArgs e)
    {
        if (e.TouchDevice.Captured == Canvas)
        {
            return;
        }

        RemoveDevice(e.TouchDevice);
        UpdateAreaEraser( );
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
        if (e.StylusDevice != null || Mode != InkCanvasNextMode.EraseArea)
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
        if (e.StylusDevice != null || !eraser.Active)
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
        if (e.StylusDevice != null || !eraser.Active)
        {
            return;
        }

        EndEraserCycle( );
        e.Handled = true;
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
}
