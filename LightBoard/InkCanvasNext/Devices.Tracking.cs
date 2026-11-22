using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void CaptureAll( )
    {
        foreach (var contact in touches.Values)
        {
            contact.Device.Capture(InnerCanvas);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void ReleaseAll( )
    {
        releasingCaptures = true;
        try
        {
            foreach (var contact in touches.Values)
            {
                contact.Device.Capture(null);
            }
        }
        finally
        {
            releasingCaptures = false;
        }
    }

    private void RemoveDevice(TouchDevice device)
    {
        if (device is SyntheticTouchDevice pointer)
        {
            EndPointerContact(pointer);
        }
        else
        {
            arbiter.End(InputKind.Touch, device.Id);
        }

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

    private void TrackTouchDown(int id, TouchDevice device, Point position, Size size, bool isPalm)
    {
        touches[id] = new TouchContact(device, position, size, isPalm ? TouchKind.Palm : TouchKind.Finger);

        if (isPalm)
        {
            return;
        }

        touchStarts[id] = position;

        if (State is TouchState.EvalPan or TouchState.Pan or TouchState.PanZoom)
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
        FlushSelectionFrame( );

        var wasFinger = touches.TryGetValue(id, out var contact) && !contact.IsPalm;
        var wasDriver = SelectionDriverId == id;

        touches.Remove(id);
        touchStarts.Remove(id);

        // 手掌触点不参与手势基线；其移除只影响面积通道，由 UpdateState/UpdateAreaEraser 处理
        if (!wasFinger)
        {
            return;
        }

        if (State is TouchState.EvalPan or TouchState.Pan or TouchState.PanZoom)
        {
            InitGesture( );
        }
        else if (State == TouchState.Selection)
        {
            RefreshSelectionGesture( );

            if (wasDriver)
            {
                RebaseSelectionDriver( );
            }
        }
    }

    private void UnsubscribeDeactivated(TouchDevice device)
    {
        device.Deactivated -= TouchDeviceDeactivated;
    }
}
