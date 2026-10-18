using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace InkCanvasNext;

/// <summary>
/// 真实鼠标的合成触点设备：自定义工具（形状/选区/面积擦/盖章）下，把鼠标按下-移动-抬起上报为触摸序列，
/// 与触摸共用状态机、事件答复与资产结算。原生工具（墨迹/按笔擦）不激活，鼠标维持 InkCanvas 原生收笔。
/// 位置以控件根（InkCanvasNext）坐标系存储，GetTouchPoint 按布局变换与滚动平移换算到目标元素。
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
            return;
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

        active = false;
        Deactivate( );
    }
}

/// <summary>
/// 鼠标输入路径：自定义工具下经 <see cref="MouseTouchDevice"/> 进入触摸状态机，
/// 原生工具下放行给 InkCanvas，另有滚轮缩放/滚动。落章统一在抬手位置发生（与触摸一致）。
/// </summary>
public partial class InkCanvasNext
{
    internal MouseTouchDevice MousePointer { get; }

    private void CanvasPreviewMouseDown(object o, MouseButtonEventArgs e)
    {
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
        if (e.StylusDevice != null)
        {
            return;
        }

        // 原生工具：鼠标未走合成触点，抬手贴边滚动与触摸抬手同款（仅左键，排除右键菜单）
        if (!MousePointer.IsDown)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                AutoScrollFromEdge(e.GetPosition(CanvasScroll));
            }

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
    /// 触摸与合流后的鼠标均由未升级为手势的单指序列在抬手位置落章（CanvasPreviewTouchUp）。</summary>
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
