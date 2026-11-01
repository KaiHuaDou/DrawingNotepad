using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace InkCanvasNext;

internal enum WheelCommand
{
    None,
    Zoom,
    Horizontal
}

/// <summary>
/// 真实鼠标的合成触点设备：自定义工具下，把鼠标操作上报为触摸序列。原生工具不激活。
/// 位置以控件坐标系存储，GetTouchPoint 按布局变换与滚动平移换算到目标元素
/// </summary>
internal sealed class MouseTouchDevice : TouchDevice
{
    private static int NextId;
    private readonly InkCanvasNext owner;
    private bool active;

    internal MouseTouchDevice(InkCanvasNext owner) : base(Interlocked.Increment(ref NextId))
    {
        this.owner = owner;
    }

    internal Point Position { get; private set; }

    internal bool IsDown { get; private set; }

    public override TouchPoint GetTouchPoint(IInputElement? relativeTo)
    {
        var position = relativeTo is UIElement element ? owner.TranslatePoint(Position, element) : Position;
        return new TouchPoint(this, position, new Rect(position, new Size(1, 1)), IsDown ? TouchAction.Move : TouchAction.Up);
    }

    public override TouchPointCollection GetIntermediateTouchPoints(IInputElement? relativeTo)
    {
        return [];
    }

    internal void Down(Point rootPosition)
    {
        if (IsDown)
        {
            Reset( );
        }

        if (!active)
        {
            SetActiveSource(PresentationSource.FromVisual(owner));
            Activate( );
            active = true;
        }

        Position = rootPosition;
        IsDown = true;
        Capture(owner.InnerCanvasElement);
        ReportDown( );
    }

    internal void Move(Point rootPosition)
    {
        if (!IsDown)
        {
            return;
        }

        Position = rootPosition;
        ReportMove( );
    }

    internal void Up(Point rootPosition)
    {
        if (!IsDown)
        {
            return;
        }

        Position = rootPosition;
        IsDown = false;
        ReportUp( );
        Release( );
    }

    /// <summary>
    /// 清掉没有收到抬起事件的序列（窗口失活、捕获被抢走），下次按下才能重新开始
    /// </summary>
    internal void Reset( )
    {
        IsDown = false;
        Release( );
    }

    private void Release( )
    {
        if (!active)
        {
            return;
        }

        active = false;
        Deactivate( );
    }
}

// 鼠标输入路径：自定义工具下经 MouseTouchDevice 进入触摸状态机，原生工具下放行给 InkCanvas，
// 另有滚轮缩放与横滚；落章统一在抬手位置发生（与触摸一致）
public partial class InkCanvasNext
{
    internal MouseTouchDevice MousePointer { get; }

    private void CanvasPreviewMouseDown(object o, MouseButtonEventArgs e)
    {
        CancelEdgeScroll( );

        if (UseDefaultCommands)
        {
            Focus( );
        }

        if (e.StylusDevice != null || !Tool.IsCustomTool)
        {
            return;
        }

        // 触摸手势进行中不接受鼠标序列；单指针会话由合成触点进入状态机
        if (State != TouchState.Idle || touches.Count > 0)
        {
            return;
        }

        MousePointer.Down(e.GetPosition(this));
        e.Handled = true;

        // 选区沿用拖出窗口继续的既定行为
        if (Tool.IsSelect)
        {
            InnerCanvas.CaptureMouse( );
        }
    }

    private void CanvasPreviewMouseMove(object o, MouseEventArgs e)
    {
        if (e.StylusDevice != null || !MousePointer.IsDown)
        {
            return;
        }

        MousePointer.Move(e.GetPosition(this));
        e.Handled = true;
    }

    private void CanvasPreviewMouseUp(object o, MouseButtonEventArgs e)
    {
        if (e.StylusDevice != null || !MousePointer.IsDown)
        {
            return;
        }

        MousePointer.Up(e.GetPosition(this));

        if (InnerCanvas.IsMouseCaptured)
        {
            InnerCanvas.ReleaseMouseCapture( );
        }

        e.Handled = true;
    }

    private void CanvasPreviewMouseWheel(object o, MouseWheelEventArgs e)
    {
        var command = ResolveWheel(options.WheelAction, Keyboard.Modifiers);

        if (command == WheelCommand.None)
        {
            // 普通滚轮交给内层 ScrollViewer；仍要中断边缘过渡
            CancelEdgeScroll( );
            return;
        }

        e.Handled = true;
        CancelEdgeScroll( );

        if (command == WheelCommand.Zoom)
        {
            ZoomAtCursor(e);
            return;
        }

        ScrollHorizontally(e.Delta);
    }

    private void ScrollHorizontally(int delta)
    {
        var step = ScrollStep * CurrentScale;
        var x = CanvasScroll.HorizontalOffset + (delta > 0 ? -step : step);
        CurrentView = new View(CurrentScale, x, CanvasScroll.VerticalOffset);
    }

    private void ZoomAtCursor(MouseWheelEventArgs e)
    {
        var view = CurrentView;
        var factor = e.Delta > 0 ? WheelZoomFactor : 1.0 / WheelZoomFactor;
        var target = Math.Clamp(view.Scale * factor, MinScale, MaxScale);
        if (Math.Abs(target / view.Scale - 1.0) < 1e-9)
        {
            return;
        }

        var cursor = e.GetPosition(this);
        var anchor = new Point(cursor.X - viewportOrigin.X, cursor.Y - viewportOrigin.Y);

        CurrentView = view.ZoomAt(anchor, target);
    }

    internal static WheelCommand ResolveWheel(MouseWheelAction action, ModifierKeys modifiers)
    {
        if (action == MouseWheelAction.None)
        {
            return WheelCommand.None;
        }

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            return WheelCommand.Zoom;
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            return WheelCommand.Horizontal;
        }

        return action == MouseWheelAction.Zoom ? WheelCommand.Zoom : WheelCommand.None;
    }

    /// <summary>在指定位置落章（克隆/粘贴），返回是否处于盖章模式（调用方据此 Handled）
    /// 触摸与合流后的鼠标均由未升级为手势的单指序列在抬手位置落章（CanvasPreviewTouchUp）</summary>
    private bool TryStamp(Point point)
    {
        if (!Tool.IsStamp)
        {
            return false;
        }

        if (Tool.Stamp == StampAction.Clone)
        {
            StampCloneAt(point);
        }
        else if (Tool.Stamp == StampAction.Paste)
        {
            StampPasteAt(point);
        }

        return true;
    }
}
