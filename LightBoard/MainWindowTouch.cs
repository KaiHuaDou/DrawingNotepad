using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace LightBoard;

// 触摸实现
// - 使用显式状态机实现
// 1. 单指触摸即让 InkCanvas 按 EditingMode 自行处理。
// 2. 第二根指头按下后，追踪两根手指
//     - 两根手指质心的位置的变化对应移动 -> ScrollViewer 实现
//     - 两根手指之间距离的变化对应缩放 -> 对所有 Stroke 实现缩放变换
// 3. 第三根手指按下后只根据质心进行拖动
// 4. 加入第四根手指结束拖动，改为点橡皮擦。
// 5. 五根手指及以上则与四指一样。

public partial class MainWindow : Window
{
    private enum TouchState
    {
        Idle,
        PanZoom,
        Pan,
        Eraser
    }

    private readonly Dictionary<int, (TouchDevice Device, Point Position)> activeTouches = [];
    private readonly ScaleTransform canvasScaleTransform = new(1.0, 1.0);

    private TouchState currentState = TouchState.Idle;
    private InkCanvasEditingMode baseEditingMode = InkCanvasEditingMode.Ink;
    private Point previousCentroid;
    private double previousDistance;
    private double currentScale = 1.0;
    private bool isReleasingCaptures;

    public MainWindow( )
    {
        InitializeComponent( );

        if(!string.IsNullOrWhiteSpace(App.PendingOpen))
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

    private void MainCanvasPreviewTouchDown(object sender, TouchEventArgs e)
    {
        Point position = e.GetTouchPoint(this).Position;
        activeTouches[e.TouchDevice.Id] = (e.TouchDevice, position);
        SubscribeDeactivated(e.TouchDevice);
        e.Handled = UpdateTouchState( );
    }

    private void MainCanvasPreviewTouchMove(object sender, TouchEventArgs e)
    {
        if (!activeTouches.ContainsKey(e.TouchDevice.Id))
        {
            return;
        }

        activeTouches[e.TouchDevice.Id] = (e.TouchDevice, e.GetTouchPoint(this).Position);

        switch (currentState)
        {
            case TouchState.PanZoom:
                e.Handled = true;
                ProcessPanZoom( );
                break;
            case TouchState.Pan:
                e.Handled = true;
                ProcessPan( );
                break;
        }
    }

    private void MainCanvasPreviewTouchUp(object sender, TouchEventArgs e)
    {
        var wasHandled = currentState is TouchState.PanZoom or TouchState.Pan;
        if (activeTouches.Remove(e.TouchDevice.Id))
        {
            UnsubscribeDeactivated(e.TouchDevice);
        }

        UpdateTouchState( );
        e.Handled = wasHandled;
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

    private void MainCanvasTouchLeave(object sender, TouchEventArgs e)
    {
        if (e.TouchDevice.Captured == MainCanvas)
        {
            return;
        }

        if (activeTouches.Remove(e.TouchDevice.Id))
        {
            UnsubscribeDeactivated(e.TouchDevice);
            UpdateTouchState( );
        }
    }

    private void MainCanvasLostTouchCapture(object sender, TouchEventArgs e)
    {
        if (isReleasingCaptures)
        {
            return;
        }

        if (activeTouches.Remove(e.TouchDevice.Id))
        {
            UnsubscribeDeactivated(e.TouchDevice);
            UpdateTouchState( );
        }
    }

    private bool UpdateTouchState( )
    {
        TouchState newState = activeTouches.Count switch
        {
            <= 1 => TouchState.Idle,
            2 => TouchState.PanZoom,
            3 => TouchState.Pan,
            >= 4 => TouchState.Eraser,
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
            case (TouchState.Idle, TouchState.PanZoom):
            case (TouchState.Idle, TouchState.Pan):
                ReleaseAllCaptures( );
                baseEditingMode = MainCanvas.EditingMode;
                MainCanvas.EditingMode = InkCanvasEditingMode.None;
                CaptureAllTouches( );
                InitializeGesture( );
                break;

            case (TouchState.Idle, TouchState.Eraser):
                ReleaseAllCaptures( );
                baseEditingMode = MainCanvas.EditingMode;
                MainCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
                break;

            case (TouchState.PanZoom, TouchState.Idle):
            case (TouchState.Pan, TouchState.Idle):
            case (TouchState.Eraser, TouchState.Idle):
                ReleaseAllCaptures( );
                RestoreEditingMode( );
                break;

            case (TouchState.PanZoom, TouchState.Pan):
            case (TouchState.Pan, TouchState.PanZoom):
                InitializeGesture( );
                break;

            case (TouchState.PanZoom, TouchState.Eraser):
            case (TouchState.Pan, TouchState.Eraser):
                ReleaseAllCaptures( );
                MainCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
                break;

            case (TouchState.Eraser, TouchState.PanZoom):
            case (TouchState.Eraser, TouchState.Pan):
                ReleaseAllCaptures( );
                MainCanvas.EditingMode = InkCanvasEditingMode.None;
                CaptureAllTouches( );
                InitializeGesture( );
                break;

            default:
                throw new InvalidOperationException($"未处理的触摸状态过渡: {currentState} -> {newState}");
        }

        currentState = newState;
    }

    private void RestoreEditingMode( )
    {
        if (MainCanvas.EditingMode is InkCanvasEditingMode.None or InkCanvasEditingMode.EraseByPoint)
        {
            MainCanvas.EditingMode = baseEditingMode;
        }
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
        isReleasingCaptures = true;
        try
        {
            foreach ((TouchDevice Device, Point Position) in activeTouches.Values)
            {
                Device.Capture(null);
            }
        }
        finally
        {
            isReleasingCaptures = false;
        }
    }

    private void TouchDeviceDeactivated(object sender, EventArgs e)
    {
        if (sender is not TouchDevice device)
        {
            return;
        }

        if (activeTouches.Remove(device.Id))
        {
            UnsubscribeDeactivated(device);
            UpdateTouchState( );
        }
    }

    private void WindowDeactivated(object sender, EventArgs e)
    {
        ResetTouchState( );
    }

    private void ResetTouchState( )
    {
        isReleasingCaptures = true;
        try
        {
            foreach ((TouchDevice Device, Point Position) in activeTouches.Values)
            {
                UnsubscribeDeactivated(Device);
                Device.Capture(null);
            }

            activeTouches.Clear( );
            SetState(TouchState.Idle);
        }
        finally
        {
            isReleasingCaptures = false;
        }
    }

    private void InitializeGesture( )
    {
        (previousCentroid, previousDistance) = GetGestureMetrics( );
    }

    private void ProcessPanZoom( )
    {
        (Point centroid, var distance) = GetGestureMetrics( );

        var deltaScale = previousDistance > 0 && distance > 0
            ? distance / previousDistance
            : 1.0;

        var newScale = Math.Clamp(currentScale * deltaScale, 0.1, 10.0);
        deltaScale = currentScale > 0 ? newScale / currentScale : 1.0;
        currentScale = newScale;

        Point viewportOrigin = MainScroll.TranslatePoint(new Point(0, 0), this);

        var newOffsetX = MainScroll.HorizontalOffset * deltaScale
            + (previousCentroid.X - viewportOrigin.X) * deltaScale
            - (centroid.X - viewportOrigin.X);
        var newOffsetY = MainScroll.VerticalOffset * deltaScale
            + (previousCentroid.Y - viewportOrigin.Y) * deltaScale
            - (centroid.Y - viewportOrigin.Y);

        MainScroll.ScrollToHorizontalOffset(Math.Clamp(newOffsetX, 0, MainScroll.ScrollableWidth));
        MainScroll.ScrollToVerticalOffset(Math.Clamp(newOffsetY, 0, MainScroll.ScrollableHeight));

        canvasScaleTransform.ScaleX = canvasScaleTransform.ScaleY = currentScale;

        previousCentroid = centroid;
        previousDistance = distance;
    }

    private void ProcessPan( )
    {
        (Point centroid, var _) = GetGestureMetrics( );

        var newOffsetX = MainScroll.HorizontalOffset + previousCentroid.X - centroid.X;
        var newOffsetY = MainScroll.VerticalOffset + previousCentroid.Y - centroid.Y;

        MainScroll.ScrollToHorizontalOffset(Math.Clamp(newOffsetX, 0, MainScroll.ScrollableWidth));
        MainScroll.ScrollToVerticalOffset(Math.Clamp(newOffsetY, 0, MainScroll.ScrollableHeight));

        previousCentroid = centroid;
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
