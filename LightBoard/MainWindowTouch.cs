using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace LightBoard;

// 触摸实现
// 1. 单指触摸即让 InkCanvas 按 EditingMode 自行处理。
// 2. 第二根指头按下后，追踪两根手指
//     - 两根手指质心的位置的变化对应移动 -> ScrollViewer 实现
//     - 两根手指之间距离的变化对应缩放 -> 对所有 Stroke 实现缩放变换
// 3. 第三根手指按下后只根据质心进行拖动
// 4. 加入第四根手指结束拖动，改为点橡皮擦。
// 5. 五根手指以上则与单指一样。

// 使用显式状态机实现

public partial class MainWindow : Window
{
    private enum TouchState
    {
        Idle,
        PanZoom,
        Pan,
        Eraser,
        MultiDraw
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
            0 or 1 => TouchState.Idle,
            2 => TouchState.PanZoom,
            3 => TouchState.Pan,
            4 => TouchState.Eraser,
            _ => TouchState.MultiDraw
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

        var wasPanning = currentState is TouchState.PanZoom or TouchState.Pan;
        var willPan = newState is TouchState.PanZoom or TouchState.Pan;
        var wasSpecial = wasPanning || currentState == TouchState.Eraser;
        var isSpecial = willPan || newState == TouchState.Eraser;

        if (!wasPanning || !willPan)
        {
            ReleaseAllCaptures( );
        }

        if (wasSpecial && !isSpecial)
        {
            RestoreEditingMode( );
        }
        else if (!wasSpecial && isSpecial)
        {
            baseEditingMode = MainCanvas.EditingMode;
        }

        if (willPan)
        {
            MainCanvas.EditingMode = InkCanvasEditingMode.None;
            if (!wasPanning)
            {
                CaptureAllTouches( );
            }

            InitializeGesture( );
        }
        else if (newState == TouchState.Eraser)
        {
            MainCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
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
        var positions = activeTouches.Values.Select(t => t.Position).ToList( );
        previousCentroid = GetCentroid(positions);
        previousDistance = positions.Count == 2 ? Distance(positions[0], positions[1]) : 0;
    }

    private void ProcessPanZoom( )
    {
        var positions = activeTouches.Values.Select(t => t.Position).ToList( );
        Point centroid = GetCentroid(positions);
        var distance = positions.Count == 2 ? Distance(positions[0], positions[1]) : 0;

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

        MainScroll.ScrollToHorizontalOffset(Clamp(newOffsetX, 0, MainScroll.ScrollableWidth));
        MainScroll.ScrollToVerticalOffset(Clamp(newOffsetY, 0, MainScroll.ScrollableHeight));

        canvasScaleTransform.ScaleX = canvasScaleTransform.ScaleY = currentScale;

        previousCentroid = centroid;
        previousDistance = distance;
    }

    private void ProcessPan( )
    {
        var positions = activeTouches.Values.Select(t => t.Position).ToList( );
        Point centroid = GetCentroid(positions);

        var newOffsetX = MainScroll.HorizontalOffset + previousCentroid.X - centroid.X;
        var newOffsetY = MainScroll.VerticalOffset + previousCentroid.Y - centroid.Y;

        MainScroll.ScrollToHorizontalOffset(Clamp(newOffsetX, 0, MainScroll.ScrollableWidth));
        MainScroll.ScrollToVerticalOffset(Clamp(newOffsetY, 0, MainScroll.ScrollableHeight));

        previousCentroid = centroid;
    }

    private static Point GetCentroid(List<Point> points)
    {
        if (points.Count == 0)
        {
            return new Point(0, 0);
        }

        double x = 0;
        double y = 0;
        foreach (Point point in points)
        {
            x += point.X;
            y += point.Y;
        }

        return new Point(x / points.Count, y / points.Count);
    }

    private static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }
}
