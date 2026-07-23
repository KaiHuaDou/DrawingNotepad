using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

using static LightBoard.Geometry;

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
    /// <summary>
    /// 升级需注意：此处依赖 .NET Core 3.1+ 内部实现细节：<br />
    /// 1. 未调用 Remove/TrimExcess 时迭代顺序等同于插入顺序。<br />
    /// 2. 调用 Remove 后，只影响所移除元素后面的元素。<br />
    /// 3. 实际情况：防止跳变即可，因此允许 Hack。<br />
    /// 4. 变通方案：OrderDictionary、手动维护前两根手指。
    /// </summary>
    private readonly Dictionary<int, (TouchDevice Device, Point Position)> activeTouches = new(20);
    private readonly Dictionary<int, Point> touchStartPositions = [];
    private readonly ScaleTransform canvasScaleTransform = new(1.0, 1.0);

    private readonly double touchDisplThreshold = 20.0;
    private readonly double distanceThreshold;
    private readonly double distanceThreshold2;

    private TouchState currentState = TouchState.Idle;
    private InkCanvasEditingMode baseEditingMode = InkCanvasEditingMode.Ink;
    private Point prevMidpoint;
    private Point viewportOrigin;
    private double currentScale = 1.0;
    private double initialScale = 1.0;
    private double initialDistance;
    private bool releasingCaptures;

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
        distanceThreshold = 0.6 * SystemParameters.WorkArea.Width;
        distanceThreshold2 = distanceThreshold * distanceThreshold;

        MainScroll.ScrollToHorizontalOffset(8192);
        MainScroll.ScrollToVerticalOffset(8192);
    }

    private void WindowLoaded(object o, RoutedEventArgs e)
    {
        viewportOrigin = MainScroll.TranslatePoint(new Point(0, 0), this);
    }

    private void MainCanvasPreviewTouchDown(object o, TouchEventArgs e)
    {
        Point position = e.GetTouchPoint(this).Position;
        TrackTouchDown(e.TouchDevice.Id, e.TouchDevice, position);
        SubscribeDeactivated(e.TouchDevice);
        e.Handled = UpdateState( );
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
            case TouchState.EvalDraw: UpdateState( ); break;
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
            UpdateState( );
        }

        e.Handled = wasHandled;
    }

    private void TrackTouchDown(int id, TouchDevice device, Point position)
    {
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
            UpdateState( );
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
            UpdateState( );
        }
    }

    private bool UpdateState( )
    {
        var count = activeTouches.Count;
        var d2 = GetMaxDistance2( );
        var l2 = distanceThreshold2;
        var x2 = Get1stFingerDispl2( );
        var c2 = touchDisplThreshold * touchDisplThreshold;

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
                3 or 4 when d2 <= l2 => TouchState.Pan,
                >= 5 when d2 <= l2 => TouchState.Eraser,
                > 2 when d2 > l2 => TouchState.MultiDraw,
                _ => currentState,
            },

            TouchState.Pan => count switch
            {
                0 => TouchState.Idle,
                >= 5 when d2 <= l2 => TouchState.Eraser,
                > 3 when d2 > l2 => TouchState.MultiDraw,
                _ => currentState,
            },

            TouchState.Eraser => count switch
            {
                0 => TouchState.Idle,
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
                ReleaseAll( );
                MainCanvas.EditingMode = InkCanvasEditingMode.None;
                CaptureAll( );
                InitializeGesture( );
                break;

            case (TouchState.Idle, TouchState.Eraser):
                baseEditingMode = MainCanvas.EditingMode;
                ReleaseAll( );
                MainCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
                break;

            case (TouchState.Idle, TouchState.MultiDraw):
                baseEditingMode = MainCanvas.EditingMode;
                ReleaseAll( );
                MainCanvas.EditingMode = InkCanvasEditingMode.Ink;
                break;

            case (TouchState.EvalDraw, TouchState.Idle):
            case (TouchState.Draw, TouchState.Idle):
            case (TouchState.MultiDraw, TouchState.Idle):
            case (TouchState.PanZoom, TouchState.Idle):
            case (TouchState.Pan, TouchState.Idle):
            case (TouchState.Eraser, TouchState.Idle):
                ReleaseAll( );
                RestoreEditingMode( );
                break;

            case (TouchState.EvalDraw, TouchState.Draw):
                // 保持已画笔画，不切换编辑模式
                break;

            case (TouchState.EvalDraw, TouchState.PanZoom):
            case (TouchState.EvalDraw, TouchState.Pan):
                ReleaseAll( );
                MainCanvas.EditingMode = InkCanvasEditingMode.None;
                CaptureAll( );
                InitializeGesture( );
                break;

            case (TouchState.MultiDraw, TouchState.Draw):
                RestoreEditingMode( );
                break;

            case (TouchState.PanZoom, TouchState.Pan):
                InitializeGesture( );
                break;

            case (TouchState.EvalDraw, TouchState.Eraser):
            case (TouchState.PanZoom, TouchState.Eraser):
            case (TouchState.Pan, TouchState.Eraser):
                ReleaseAll( );
                MainCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
                break;

            case (TouchState.EvalDraw, TouchState.MultiDraw):
            case (TouchState.Draw, TouchState.MultiDraw):
            case (TouchState.PanZoom, TouchState.MultiDraw):
            case (TouchState.Pan, TouchState.MultiDraw):
            case (TouchState.Eraser, TouchState.MultiDraw):
                ReleaseAll( );
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
        foreach ((_, Point position) in activeTouches.Values)
        {
            positions[index++] = position;
        }

        return MaxDistance2(ref positions);
    }

    private double Get1stFingerDispl2( )
    {
        if (activeTouches.Count == 0)
        {
            return 0;
        }

        KeyValuePair<int, (TouchDevice Device, Point Position)> first = activeTouches.First( );
        if (!touchStartPositions.TryGetValue(first.Key, out Point start))
        {
            return 0;
        }

        Point current = first.Value.Position;
        return Distance2(current, start);
    }

    private void CaptureAll( )
    {
        foreach ((TouchDevice Device, _) in activeTouches.Values)
        {
            Device.Capture(MainCanvas);
        }
    }

    private void ReleaseAll( )
    {
        releasingCaptures = true;
        try
        {
            foreach ((TouchDevice Device, _) in activeTouches.Values)
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
            foreach ((TouchDevice Device, _) in activeTouches.Values)
            {
                UnsubscribeDeactivated(Device);
                Device.Capture(null);
            }

            activeTouches.Clear( );
            touchStartPositions.Clear( );
            SetState(TouchState.Idle);
        }
        finally
        {
            releasingCaptures = false;
        }
    }

    private void InitializeGesture( )
    {
        (prevMidpoint, initialDistance) = GetGestureMetrics( );
        initialScale = currentScale;
    }

    private void ProcessPanZoom( )
    {
        (Point midpoint, var distance) = GetGestureMetrics( );

        var ratio = initialDistance > 0 && distance > 0
            ? distance / initialDistance
            : 1.0;

        var scale = Math.Clamp(initialScale * ratio, 0.1, 10.0);

        canvasScaleTransform.ScaleX = canvasScaleTransform.ScaleY = scale;

        ratio = scale / currentScale;

        var newOffsetX = MainScroll.HorizontalOffset * ratio
            + (prevMidpoint.X - viewportOrigin.X) * ratio
            - (midpoint.X - viewportOrigin.X);
        var newOffsetY = MainScroll.VerticalOffset * ratio
            + (prevMidpoint.Y - viewportOrigin.Y) * ratio
            - (midpoint.Y - viewportOrigin.Y);

        MainScroll.ScrollToHorizontalOffset(Math.Clamp(newOffsetX, 0, MainScroll.ScrollableWidth));
        MainScroll.ScrollToVerticalOffset(Math.Clamp(newOffsetY, 0, MainScroll.ScrollableHeight));

        currentScale = scale;
        prevMidpoint = midpoint;
    }

    private void ProcessPan( )
    {
        (Point midpoint, _) = GetGestureMetrics( );

        var newOffsetX = MainScroll.HorizontalOffset + prevMidpoint.X - midpoint.X;
        var newOffsetY = MainScroll.VerticalOffset + prevMidpoint.Y - midpoint.Y;

        MainScroll.ScrollToHorizontalOffset(Math.Clamp(newOffsetX, 0, MainScroll.ScrollableWidth));
        MainScroll.ScrollToVerticalOffset(Math.Clamp(newOffsetY, 0, MainScroll.ScrollableHeight));

        prevMidpoint = midpoint;
    }

#pragma warning disable IDE0008

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private (Point Midpoint, double Distance) GetGestureMetrics( )
    {
        var values = activeTouches.Values;
        var count = values.Count;

        if (count == 0)
        {
            return (new Point(0, 0), 0);
        }

        using var enumerator = values.GetEnumerator( );
        enumerator.MoveNext( );
        Point first = enumerator.Current.Position;

        if (count == 1)
        {
            return (first, 0);
        }

        enumerator.MoveNext( );
        Point second = enumerator.Current.Position;

        Point midpoint = Midpoint(first, second);
        var distance = Distance(first, second);

        return (midpoint, distance);
    }
}
