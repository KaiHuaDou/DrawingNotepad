using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

using static LightBoard.Geometry;

namespace LightBoard;

public partial class MainWindow
{
    /// <summary>
    /// 升级需注意：此处依赖 .NET Core 3.1+ 内部实现细节：<br />
    /// 1. 未调用 Remove/TrimExcess 时迭代顺序等同于插入顺序。<br />
    /// 2. 调用 Remove 后，只影响所移除元素后面的元素。<br />
    /// 3. 实际情况：防止跳变即可，因此允许 Hack。<br />
    /// 4. 变通方案：OrderDictionary、手动维护前两根手指。
    /// </summary>
    private readonly Dictionary<int, (TouchDevice Device, Point Position)> touches = new(20);
    private readonly Dictionary<int, Point> touchStarts = [];

    private readonly double touchDisplThreshold = 20.0;
    private bool releasingCaptures;

    private void MainCanvasPreviewTouchDown(object o, TouchEventArgs e)
    {
        Point position = e.GetTouchPoint(this).Position;
        TrackTouchDown(e.TouchDevice.Id, e.TouchDevice, position);
        SubscribeDeactivated(e.TouchDevice);
        e.Handled = UpdateState( );
    }

    private void MainCanvasPreviewTouchMove(object o, TouchEventArgs e)
    {
        if (!touches.ContainsKey(e.TouchDevice.Id))
        {
            return;
        }

        touches[e.TouchDevice.Id] = (e.TouchDevice, e.GetTouchPoint(this).Position);

        switch (currentState)
        {
            case TouchState.EvalDraw: UpdateState( ); break;
            case TouchState.PanZoom: PanZoom( ); e.Handled = true; break;
            case TouchState.Pan: Pan( ); e.Handled = true; break;
        }
    }

    private void MainCanvasPreviewTouchUp(object o, TouchEventArgs e)
    {
        var wasHandled = currentState is TouchState.PanZoom or TouchState.Pan;
        if (touches.ContainsKey(e.TouchDevice.Id))
        {
            TrackTouchUp(e.TouchDevice.Id);
            UnsubscribeDeactivated(e.TouchDevice);
            UpdateState( );
        }

        e.Handled = wasHandled;
    }

    private void MainCanvasTouchLeave(object o, TouchEventArgs e)
    {
        if (e.TouchDevice.Captured == MainCanvas)
        {
            return;
        }

        if (touches.ContainsKey(e.TouchDevice.Id))
        {
            TrackTouchUp(e.TouchDevice.Id);
            UnsubscribeDeactivated(e.TouchDevice);
            UpdateState( );
        }
    }

    private void MainCanvasLostTouchCapture(object o, TouchEventArgs e)
    {
        if (releasingCaptures)
        {
            return;
        }

        if (touches.ContainsKey(e.TouchDevice.Id))
        {
            TrackTouchUp(e.TouchDevice.Id);
            UnsubscribeDeactivated(e.TouchDevice);
            UpdateState( );
        }
    }

    private void TrackTouchDown(int id, TouchDevice device, Point position)
    {
        touches[id] = (device, position);
        touchStarts[id] = position;

        if (currentState is TouchState.Pan or TouchState.PanZoom)
        {
            InitGesture( );
        }
    }

    private void TrackTouchUp(int id)
    {
        touches.Remove(id);
        touchStarts.Remove(id);

        if (currentState is TouchState.Pan or TouchState.PanZoom)
        {
            InitGesture( );
        }
    }

    private void SubscribeDeactivated(TouchDevice device)
    {
        device.Deactivated -= TouchDeviceDeactivated;
        device.Deactivated += TouchDeviceDeactivated;
    }

    private void UnsubscribeDeactivated(TouchDevice device)
    {
        device.Deactivated -= TouchDeviceDeactivated;
    }

    private void CaptureAll( )
    {
        foreach ((TouchDevice Device, _) in touches.Values)
        {
            Device.Capture(MainCanvas);
        }
    }

    private void ReleaseAll( )
    {
        releasingCaptures = true;
        try
        {
            foreach ((TouchDevice Device, _) in touches.Values)
            {
                Device.Capture(null);
            }
        }
        finally
        {
            releasingCaptures = false;
        }
    }

    private void TouchDeviceDeactivated(object? o, EventArgs e)
    {
        if (o is not TouchDevice device)
        {
            return;
        }

        if (touches.ContainsKey(device.Id))
        {
            TrackTouchUp(device.Id);
            UnsubscribeDeactivated(device);
            UpdateState( );
        }
    }

    private void WindowDeactivated(object o, EventArgs e)
    {
        ResetTouchState( );
    }

    private void ResetTouchState( )
    {
        releasingCaptures = true;
        try
        {
            foreach ((TouchDevice Device, _) in touches.Values)
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

    private double GetMaxDistance2( )
    {
        var count = touches.Count;
        if (count < 2)
        {
            return 0;
        }

        Span<Point> positions = stackalloc Point[count];
        var index = 0;
        foreach ((_, Point position) in touches.Values)
        {
            positions[index++] = position;
        }

        return MaxDistance2(ref positions);
    }

#pragma warning disable IDE0008
    private double Get1stFingerDispl2( )
    {
        if (touches.Count == 0)
        {
            return 0;
        }

        var first = touches.First( );
        if (!touchStarts.TryGetValue(first.Key, out Point start))
        {
            return 0;
        }

        Point current = first.Value.Position;
        return Distance2(current, start);
    }
}
