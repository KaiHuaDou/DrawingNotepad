using System;

using System.Windows;
using System.Windows.Input;

namespace InkCanvasNext;

internal enum WheelCommand
{
    None,
    Zoom,
    Horizontal,
    Vertical
}

// 鼠标输入路径：自定义工具下经 SyntheticTouchDevice 进入触摸状态机，原生工具下放行给 InkCanvas，
// 另有滚轮缩放与横滚；落章统一在抬手位置发生（与触摸一致）
public partial class InkCanvasNext
{
    internal SyntheticTouchDevice MousePointer { get; }

    private void CanvasPreviewMouseDown(object o, MouseButtonEventArgs e)
    {
        CancelEdgeScroll( );
        HidePenCursor( );

        if (UseDefaultCommands)
        {
            Focus( );
        }

        // 提升为笔事件的鼠标由 CanvasPreviewStylusDown 处理
        if (e.StylusDevice != null)
        {
            return;
        }

        BeginMouseContact(e.GetPosition(this), e);
    }

    /// <summary>
    /// 鼠标按下入口：真实鼠标与设备号为 0 的笔事件共用，自定义工具下转成合成触点
    /// </summary>
    private void BeginMouseContact(Point position, InputEventArgs e)
    {
        if (!arbiter.Accepts(InputKind.Mouse))
        {
            arbiter.Begin(InputKind.Mouse);
            e.Handled = true;
            return;
        }

        CancelLower(InputKind.Mouse);
        arbiter.Begin(InputKind.Mouse);

        if (!Snapshot.IsCustomTool)
        {
            return;
        }

        // 触摸手势进行中不接受鼠标序列；单指针会话由合成触点进入状态机
        if (State != TouchState.Idle || touches.Count > 0)
        {
            e.Handled = true;
            return;
        }

        MousePointer.Down(position);
        e.Handled = true;

        // 选区沿用拖出窗口继续的既定行为
        if (Snapshot.IsSelect)
        {
            InnerCanvas.CaptureMouse( );
        }
    }

    private void CanvasPreviewMouseMove(object o, MouseEventArgs e)
    {
        if (e.StylusDevice != null || !arbiter.MouseAccepted || !MousePointer.IsDown)
        {
            return;
        }

        MousePointer.Move(e.GetPosition(this));
        e.Handled = true;
    }

    private void CanvasPreviewMouseUp(object o, MouseButtonEventArgs e)
    {
        if (e.StylusDevice != null)
        {
            return;
        }

        if (!MousePointer.IsDown)
        {
            arbiter.End(InputKind.Mouse);
            return;
        }

        MousePointer.Up(e.GetPosition(this));
        arbiter.End(InputKind.Mouse);

        if (InnerCanvas.IsMouseCaptured)
        {
            InnerCanvas.ReleaseMouseCapture( );
        }

        e.Handled = true;
    }

    private void CanvasLostMouseCapture(object o, MouseEventArgs e)
    {
        if (!arbiter.MouseDown)
        {
            return;
        }

        arbiter.End(InputKind.Mouse);

        if (MousePointer.IsDown)
        {
            MousePointer.Reset( );
            ResetTouchState( );
        }
    }

    private void CanvasPreviewMouseWheel(object o, MouseWheelEventArgs e)
    {
        var command = ResolveWheel(options.WheelAction, Keyboard.Modifiers);
        CancelEdgeScroll( );

        if (command == WheelCommand.None)
        {
            return;
        }

        e.Handled = true;

        switch (command)
        {
            case WheelCommand.Zoom:
                ZoomAtCursor(e);
                break;
            case WheelCommand.Horizontal:
                ScrollBy(e.Delta, horizontal: true);
                break;
            case WheelCommand.Vertical:
                ScrollBy(e.Delta, horizontal: false);
                break;
        }
    }

    /// <summary>
    /// 按滚轮格数平移视图；高精度滚轮的小增量按比例折算
    /// </summary>
    private void ScrollBy(int delta, bool horizontal)
    {
        var view = CurrentView;
        var distance = delta / WheelDelta * ScrollStep * view.Scale;

        if (distance == 0)
        {
            return;
        }

        CurrentView = horizontal
            ? view with { OffsetX = view.OffsetX - distance }
            : view with { OffsetY = view.OffsetY - distance };
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

        return action == MouseWheelAction.Zoom ? WheelCommand.Zoom : WheelCommand.Vertical;
    }

    /// <summary>
    /// 在指定位置落章，返回是否处于盖章模式
    /// </summary>
    private bool TryStamp(Point point)
    {
        if (!Snapshot.IsStamp)
        {
            return false;
        }

        if (Snapshot.Stamp == StampAction.Clone)
        {
            StampCloneAt(point);
        }
        else if (Snapshot.Stamp == StampAction.Paste)
        {
            StampPasteAt(point);
        }

        return true;
    }
}
