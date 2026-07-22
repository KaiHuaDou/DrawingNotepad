using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace LightBoard;
public enum TouchState
{
    Idle,
    EvalDraw,
    Draw,
    PanZoom,
    Pan,
    Eraser,
    MultiDraw
}

public partial class MainWindow : Window
{
    private const double TouchDisplThreshold = 15.0;

    private readonly Dictionary<int, (TouchDevice Device, Point Position)> activeTouches = [];
    private readonly Dictionary<int, Point> touchStartPositions = [];
    private readonly ScaleTransform canvasScaleTransform = new(1.0, 1.0);
    private double DistanceThreshold => 0.6 * ActualWidth;

    private TouchState currentState = TouchState.Idle;
    private InkCanvasEditingMode baseEditingMode = InkCanvasEditingMode.Ink;
    private Point prevCentroid;
    private double prevDistance;
    private double currentScale = 1.0;
    private bool releasingCaptures;
    private int firstTouchId;

    public MainWindow( )
    {
        InitializeComponent( );

        if (!string.IsNullOrWhiteSpace(App.PendingOpen))
        {
            OpenStrokes(App.PendingOpen);
        }

        MainCanvas.EraserShape = new RectangleStylusShape(100, 160);
        MainCanvas.LayoutTransform = canvasScaleTransform;

        MainCanvas.Strokes.StrokesChanged += OnStrokesChanged;

        baseEditingMode = MainCanvas.EditingMode;

        MainScroll.ScrollToHorizontalOffset(8192);
        MainScroll.ScrollToVerticalOffset(8192);
    }

    private void MainCanvasPreviewTouchDown(object o, TouchEventArgs e)
    {
        Point position = e.GetTouchPoint(this).Position;
        TrackTouchDown(e.TouchDevice.Id, e.TouchDevice, position);
        SubscribeDeactivated(e.TouchDevice);
        e.Handled = UpdateTouchState( );
    }

    private void MainCanvasPreviewTouchMove(object o, TouchEventArgs e)
    {
        if (!activeTouches.ContainsKey(e.TouchDevice.Id))
        {
            return;
        }

        activeTouches[e.TouchDevice.Id] = (e.TouchDevice, e.GetTouchPoint(this).Position);

        switch (currentState)
        {
            case TouchState.EvalDraw: UpdateTouchState( ); break;
            case TouchState.PanZoom: ProcessPanZoom( ); e.Handled = true; break;
            case TouchState.Pan: ProcessPan( ); e.Handled = true; break;
        }
    }

    private void MainCanvasPreviewTouchUp(object o, TouchEventArgs e)
    {
        var wasHandled = currentState is TouchState.PanZoom or TouchState.Pan;
        if (activeTouches.ContainsKey(e.TouchDevice.Id))
        {
            TrackTouchUp(e.TouchDevice.Id);
            UnsubscribeDeactivated(e.TouchDevice);
            UpdateTouchState( );
        }

        e.Handled = wasHandled;
    }

    private void TrackTouchDown(int id, TouchDevice device, Point position)
    {
        if (activeTouches.Count == 0)
        {
            firstTouchId = id;
        }

        activeTouches[id] = (device, position);
        touchStartPositions[id] = position;

        if (currentState is TouchState.Pan or TouchState.PanZoom)
        {
            InitializeGesture( );
        }
    }

    private void TrackTouchUp(int id)
    {
        activeTouches.Remove(id);
        touchStartPositions.Remove(id);

        if (id == firstTouchId && activeTouches.Count > 0)
        {
            firstTouchId = activeTouches.Keys.Min( );
        }

        if (currentState is TouchState.Pan or TouchState.PanZoom)
        {
            InitializeGesture( );
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

    private void MainCanvasTouchLeave(object o, TouchEventArgs e)
    {
        if (e.TouchDevice.Captured == MainCanvas)
        {
            return;
        }

        if (activeTouches.ContainsKey(e.TouchDevice.Id))
        {
            TrackTouchUp(e.TouchDevice.Id);
            UnsubscribeDeactivated(e.TouchDevice);
            UpdateTouchState( );
        }
    }

    private void MainCanvasLostTouchCapture(object o, TouchEventArgs e)
    {
        if (releasingCaptures)
        {
            return;
        }

        if (activeTouches.ContainsKey(e.TouchDevice.Id))
        {
            TrackTouchUp(e.TouchDevice.Id);
            UnsubscribeDeactivated(e.TouchDevice);
            UpdateTouchState( );
        }
    }

    private bool UpdateTouchState( )
    {
        var count = activeTouches.Count;
        var d2 = GetMaxDistance2( );
        var l = DistanceThreshold;
        var l2 = l * l;
        var x2 = Get1stFingerDispl2( );
        var c2 = TouchDisplThreshold * TouchDisplThreshold;

        TouchState newState = currentState switch
        {
            TouchState.Idle => count switch
            {
                0 => TouchState.Idle,
                1 => TouchState.EvalDraw,
                2 when d2 <= l2 => TouchState.PanZoom,
                3 or 4 when d2 <= l2 => TouchState.Pan,
                >= 5 when d2 <= l2 => TouchState.Eraser,
                >= 2 when d2 > l2 => TouchState.MultiDraw,
                _ => currentState,
            },

            TouchState.EvalDraw => count switch
            {
                0 => TouchState.Idle,
                1 when x2 > c2 => TouchState.Draw,
                2 when d2 <= l2 => TouchState.PanZoom,
                3 or 4 when d2 <= l2 => TouchState.Pan,
                >= 5 when d2 <= l2 => TouchState.Eraser,
                >= 2 when d2 > l2 => TouchState.MultiDraw,
                _ => currentState,
            },

            TouchState.Draw => count switch
            {
                0 => TouchState.Idle,
                >= 2 when d2 > l2 => TouchState.MultiDraw,
                _ => currentState,
            },

            TouchState.MultiDraw => count switch
            {
                0 => TouchState.Idle,
                1 => TouchState.Draw,
                _ => currentState,
            },

            TouchState.PanZoom => count switch
            {
                0 => TouchState.Idle,
                1 => TouchState.Draw,
                3 or 4 when d2 <= l2 => TouchState.Pan,
                >= 5 when d2 <= l2 => TouchState.Eraser,
                > 2 when d2 > l2 => TouchState.MultiDraw,
                _ => currentState,
            },

            TouchState.Pan => count switch
            {
                0 => TouchState.Idle,
                1 => TouchState.Draw,
                >= 5 when d2 <= l2 => TouchState.Eraser,
                > 3 when d2 > l2 => TouchState.MultiDraw,
                _ => currentState,
            },

            TouchState.Eraser => count switch
            {
                0 => TouchState.Idle,
                1 => TouchState.EvalDraw,
                > 5 when d2 > l2 => TouchState.MultiDraw,
                _ => currentState,
            },

            _ => currentState,
        };

        SetState(newState);
        return newState is TouchState.PanZoom or TouchState.Pan;
    }

    private void SetState(TouchState newState)
    {
        if (currentState == newState)
        {
            return;
        }

        switch ((currentState, newState))
        {
            case (TouchState.Idle, TouchState.EvalDraw):
                baseEditingMode = MainCanvas.EditingMode;
                break;

            case (TouchState.Idle, TouchState.PanZoom):
            case (TouchState.Idle, TouchState.Pan):
                baseEditingMode = MainCanvas.EditingMode;
                ReleaseAllCaptures( );
                MainCanvas.EditingMode = InkCanvasEditingMode.None;
                CaptureAllTouches( );
                InitializeGesture( );
                break;

            case (TouchState.Idle, TouchState.Eraser):
                baseEditingMode = MainCanvas.EditingMode;
                ReleaseAllCaptures( );
                MainCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
                break;

            case (TouchState.Idle, TouchState.MultiDraw):
                baseEditingMode = MainCanvas.EditingMode;
                ReleaseAllCaptures( );
                MainCanvas.EditingMode = InkCanvasEditingMode.Ink;
                break;

            case (TouchState.EvalDraw, TouchState.Idle):
            case (TouchState.Draw, TouchState.Idle):
            case (TouchState.MultiDraw, TouchState.Idle):
            case (TouchState.PanZoom, TouchState.Idle):
            case (TouchState.Pan, TouchState.Idle):
            case (TouchState.Eraser, TouchState.Idle):
                ReleaseAllCaptures( );
                RestoreEditingMode( );
                break;

            case (TouchState.EvalDraw, TouchState.Draw):
                // 保持已画笔画，不切换编辑模式
                break;

            case (TouchState.EvalDraw, TouchState.PanZoom):
            case (TouchState.EvalDraw, TouchState.Pan):
                ReleaseAllCaptures( );
                MainCanvas.EditingMode = InkCanvasEditingMode.None;
                CaptureAllTouches( );
                InitializeGesture( );
                break;

            case (TouchState.EvalDraw, TouchState.Eraser):
                ReleaseAllCaptures( );
                MainCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
                break;

            case (TouchState.EvalDraw, TouchState.MultiDraw):
                ReleaseAllCaptures( );
                MainCanvas.EditingMode = InkCanvasEditingMode.Ink;
                break;

            case (TouchState.Draw, TouchState.MultiDraw):
                ReleaseAllCaptures( );
                MainCanvas.EditingMode = InkCanvasEditingMode.Ink;
                break;

            case (TouchState.MultiDraw, TouchState.Draw):
                RestoreEditingMode( );
                break;

            case (TouchState.PanZoom, TouchState.Draw):
            case (TouchState.Pan, TouchState.Draw):
                ReleaseAllCaptures( );
                RestoreEditingMode( );
                break;

            case (TouchState.PanZoom, TouchState.Pan):
                InitializeGesture( );
                break;

            case (TouchState.PanZoom, TouchState.Eraser):
            case (TouchState.Pan, TouchState.Eraser):
                ReleaseAllCaptures( );
                MainCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
                break;

            case (TouchState.PanZoom, TouchState.MultiDraw):
            case (TouchState.Pan, TouchState.MultiDraw):
                ReleaseAllCaptures( );
                MainCanvas.EditingMode = InkCanvasEditingMode.Ink;
                break;

            case (TouchState.Eraser, TouchState.EvalDraw):
                RestoreEditingMode( );
                break;

            case (TouchState.Eraser, TouchState.MultiDraw):
                ReleaseAllCaptures( );
                MainCanvas.EditingMode = InkCanvasEditingMode.Ink;
                break;

            default:
                // 未显式处理的状态转换不执行额外动作，由方法末尾统一更新 currentState。
                break;
        }

        currentState = newState;
    }

    private void RestoreEditingMode( )
    {
        if (MainCanvas.EditingMode != baseEditingMode)
        {
            MainCanvas.EditingMode = baseEditingMode;
        }
    }

    private double GetMaxDistance2( )
    {
        var count = activeTouches.Count;
        if (count < 2)
        {
            return 0;
        }

        Span<Point> positions = stackalloc Point[count];
        var index = 0;
        foreach ((TouchDevice _, Point position) in activeTouches.Values)
        {
            positions[index++] = position;
        }

        double max2 = 0;
        for (var i = 0; i < positions.Length; i++)
        {
            Point first = positions[i];
            for (var j = i + 1; j < positions.Length; j++)
            {
                Point second = positions[j];
                var dx = first.X - second.X;
                var dy = first.Y - second.Y;
                var d2 = dx * dx + dy * dy;
                if (d2 > max2)
                {
                    max2 = d2;
                }
            }
        }

        return max2;
    }

    private double Get1stFingerDispl2( )
    {
        if (activeTouches.Count == 0)
        {
            return 0;
        }

        Point current = activeTouches[firstTouchId].Position;
        Point start = touchStartPositions[firstTouchId];
        var dx = current.X - start.X;
        var dy = current.Y - start.Y;
        return dx * dx + dy * dy;
    }

    private void CaptureAllTouches( )
    {
        foreach ((TouchDevice Device, Point Position) in activeTouches.Values)
        {
            Device.Capture(MainCanvas);
        }
    }

    private void ReleaseAllCaptures( )
    {
        releasingCaptures = true;
        try
        {
            foreach ((TouchDevice Device, Point Position) in activeTouches.Values)
            {
                Device.Capture(null);
            }
        }
        finally
        {
            releasingCaptures = false;
        }
    }

    private void TouchDeviceDeactivated(object o, EventArgs e)
    {
        if (o is not TouchDevice device)
        {
            return;
        }

        if (activeTouches.ContainsKey(device.Id))
        {
            TrackTouchUp(device.Id);
            UnsubscribeDeactivated(device);
            UpdateTouchState( );
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
            foreach ((TouchDevice Device, Point Position) in activeTouches.Values)
            {
                UnsubscribeDeactivated(Device);
                Device.Capture(null);
            }

            activeTouches.Clear( );
            touchStartPositions.Clear( );
            firstTouchId = 0;
            SetState(TouchState.Idle);
        }
        finally
        {
            releasingCaptures = false;
        }
    }

    private void InitializeGesture( )
    {
        (prevCentroid, prevDistance) = GetGestureMetrics( );
    }

    private void ProcessPanZoom( )
    {
        (Point centroid, var distance) = GetGestureMetrics( );

        var deltaScale = prevDistance > 0 && distance > 0
            ? distance / prevDistance
            : 1.0;

        var newScale = Math.Clamp(currentScale * deltaScale, 0.1, 10.0);
        deltaScale = newScale / currentScale;
        currentScale = newScale;

        Point viewportOrigin = MainScroll.TranslatePoint(new Point(0, 0), this);

        var newOffsetX = MainScroll.HorizontalOffset * deltaScale
            + (prevCentroid.X - viewportOrigin.X) * deltaScale
            - (centroid.X - viewportOrigin.X);
        var newOffsetY = MainScroll.VerticalOffset * deltaScale
            + (prevCentroid.Y - viewportOrigin.Y) * deltaScale
            - (centroid.Y - viewportOrigin.Y);

        MainScroll.ScrollToHorizontalOffset(Math.Clamp(newOffsetX, 0, MainScroll.ScrollableWidth));
        MainScroll.ScrollToVerticalOffset(Math.Clamp(newOffsetY, 0, MainScroll.ScrollableHeight));

        canvasScaleTransform.ScaleX = canvasScaleTransform.ScaleY = currentScale;

        prevCentroid = centroid;
        prevDistance = distance;
    }

    private void ProcessPan( )
    {
        (Point centroid, var _) = GetGestureMetrics( );

        var newOffsetX = MainScroll.HorizontalOffset + prevCentroid.X - centroid.X;
        var newOffsetY = MainScroll.VerticalOffset + prevCentroid.Y - centroid.Y;

        MainScroll.ScrollToHorizontalOffset(Math.Clamp(newOffsetX, 0, MainScroll.ScrollableWidth));
        MainScroll.ScrollToVerticalOffset(Math.Clamp(newOffsetY, 0, MainScroll.ScrollableHeight));

        prevCentroid = centroid;
    }

    private (Point Centroid, double Distance) GetGestureMetrics( )
    {
        double sumX = 0;
        double sumY = 0;
        Point first = default;
        Point second = default;
        var count = 0;

        foreach ((_, Point position) in activeTouches.Values)
        {
            sumX += position.X;
            sumY += position.Y;

            if (count == 0)
            {
                first = position;
            }
            else if (count == 1)
            {
                second = position;
            }

            count++;
        }

        Point centroid = count > 0 ? new Point(sumX / count, sumY / count) : new Point(0, 0);

        var distance = count == 2 ? Distance(first, second) : 0;

        return (centroid, distance);
    }

    private static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
