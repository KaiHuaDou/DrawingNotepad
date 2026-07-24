using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

using static InkCanvasNext.Geometry;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    private readonly Dictionary<int, (TouchDevice Device, Point Position)> touches = new(20);
    private readonly Dictionary<int, Point> touchStarts = [ ];

    private readonly double touchDisplThreshold = 20.0;
    private bool releasingCaptures;

    private void CanvasPreviewTouchDown(object o, TouchEventArgs e)
    {
        Point position = e.GetTouchPoint(this).Position;
        TrackTouchDown(e.TouchDevice.Id, e.TouchDevice, position);
        SubscribeDeactivated(e.TouchDevice);
        e.Handled = UpdateState( );
    }

    private void CanvasPreviewTouchMove(object o, TouchEventArgs e)
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

    private void CanvasPreviewTouchUp(object o, TouchEventArgs e)
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

    private void CanvasTouchLeave(object o, TouchEventArgs e)
    {
        if (e.TouchDevice.Captured == Canvas)
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

    private void CanvasLostTouchCapture(object o, TouchEventArgs e)
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
            Device.Capture(Canvas);
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

    public void ResetTouchState( )
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
