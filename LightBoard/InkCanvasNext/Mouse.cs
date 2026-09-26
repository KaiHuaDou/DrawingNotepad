using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace InkCanvasNext;

/// <summary>
/// 鼠标输入路径：形状/选区/落章/橡皮的按下-移动-抬起处理与滚轮。
/// 触点状态与手势接管由触摸路径（Devices.cs）与状态机负责。
/// </summary>
public partial class InkCanvasNext
{
    private void CanvasPreviewMouseDown(object o, MouseButtonEventArgs e)
    {
        if (e.StylusDevice != null)
        {
            return;
        }

        if (TryStamp(e.GetPosition(InnerCanvas)))
        {
            e.Handled = true;
            return;
        }

        if (IsShapeMode && !shapeActive)
        {
            StartShape(e.GetPosition(InnerCanvas));
            e.Handled = true;
            return;
        }

        if (Mode == InkCanvasNextMode.Select)
        {
            BeginMouseSelection(e.GetPosition(InnerCanvas));
            InnerCanvas.CaptureMouse( );
            e.Handled = true;
            return;
        }

        if (Mode != InkCanvasNextMode.EraseArea)
        {
            return;
        }

        var screenPosition = e.GetPosition(this);
        var canvasPosition = e.GetPosition(InnerCanvas);
        eraser.Diameter = EraserDiameter;
        eraser.Scale = CurrentScale;
        eraser.Show(screenPosition);
        eraser.Start(canvasPosition);
        e.Handled = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void CanvasPreviewMouseMove(object o, MouseEventArgs e)
    {
        if (e.StylusDevice != null)
        {
            return;
        }

        if (shapeActive)
        {
            UpdateShape(e.GetPosition(InnerCanvas));
            e.Handled = true;
            return;
        }

        if (Mode == InkCanvasNextMode.Select && selectionGesture != SelectionGesture.None)
        {
            UpdateMouseSelection(e.GetPosition(InnerCanvas));
            e.Handled = true;
            return;
        }

        if (!eraser.Active)
        {
            return;
        }

        var screenPosition = e.GetPosition(this);
        var canvasPosition = e.GetPosition(InnerCanvas);
        eraser.Show(screenPosition);
        eraser.Move(canvasPosition);
        e.Handled = true;
    }

    private void CanvasPreviewMouseUp(object o, MouseButtonEventArgs e)
    {
        if (e.StylusDevice != null)
        {
            return;
        }

        if (shapeActive)
        {
            UpdateShape(e.GetPosition(InnerCanvas));
            CommitShape( );
            e.Handled = true;
            return;
        }

        if (Mode == InkCanvasNextMode.Select && selectionGesture != SelectionGesture.None)
        {
            EndMouseSelection( );
            e.Handled = true;
            return;
        }

        if (!eraser.Active)
        {
            return;
        }

        EndEraserCycle( );
        e.Handled = true;
    }

    private void CanvasPreviewMouseWheel(object o, MouseWheelEventArgs e)
    {
        if (MouseWheelAction == MouseWheelAction.None)
        {
            return;
        }

        e.Handled = true;

        if (MouseWheelAction == MouseWheelAction.Zoom || Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            ZoomAtCursor(e);
            return;
        }

        var step = ScrollStep * CurrentScale;
        CurrentView = CurrentView
            .Pan(new Vector(0, e.Delta > 0 ? -step : step))
            .Clamp(CanvasScroll.ScrollableWidth, CanvasScroll.ScrollableHeight);
    }

    /// <summary>在指定位置落章（克隆/粘贴），返回是否处于盖章模式（调用方据此 Handled）。
    /// 鼠标在按下时落章，触摸由未升级为手势的单指序列在抬手时落章（CanvasPreviewTouchUp）。</summary>
    private bool TryStamp(Point point)
    {
        if (StampAction == StampAction.None)
        {
            return false;
        }

        if (StampAction == StampAction.Clone)
        {
            StampCloneAt(point);
        }
        else if (StampAction == StampAction.Paste)
        {
            StampPasteAt(point);
        }

        return true;
    }
}
