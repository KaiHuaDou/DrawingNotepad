using System.Windows;
using System.Windows.Input;

namespace InkCanvasNext.Tests;

/// <summary>
/// 自定义 TouchDevice 模拟类：按 WPF 触摸输入协议（SetActiveSource → Activate → ReportDown/ReportMove/ReportUp → Deactivate）
/// 向目标元素上报触摸，从而驱动 InkCanvasNext 的触摸状态机。
/// <para>
/// GetTouchPoint 采用恒等映射：状态机只做相对量比较（距离/位移/两两距离），不受坐标系影响；
/// 上报坐标位于宿主窗口根可视（InkCanvasNext）坐标系内，保证命中测试命中 InnerCanvas。
/// </para>
/// </summary>
internal sealed class MockTouchDevice : TouchDevice
{
    private static int NextId;

    private bool active;
    private bool down;

    public MockTouchDevice( ) : base(Interlocked.Increment(ref NextId))
    {
    }

    public Point Position { get; private set; }

    public TouchAction Action { get; private set; }

    public override TouchPoint GetTouchPoint(IInputElement? relativeTo)
    {
        return new TouchPoint(this, Position, new Rect(Position, new Size(1, 1)), Action);
    }

    public override TouchPointCollection GetIntermediateTouchPoints(IInputElement? relativeTo)
    {
        return [];
    }

    public void Down(UIElement target, Point position)
    {
        ActivateFor(target);
        Position = position;
        Action = TouchAction.Down;
        down = true;
        ReportDown( );
    }

    public void Move(UIElement target, Point position)
    {
        ActivateFor(target);
        Position = position;
        Action = TouchAction.Move;
        ReportMove( );
    }

    public void Up(UIElement target, Point position)
    {
        ActivateFor(target);
        Position = position;
        Action = TouchAction.Up;
        down = false;
        ReportUp( );
        DeactivateIfActive( );
    }

    /// <summary>未抬起时强制结束设备生命周期（配对 Activate），用于测试清理。</summary>
    public void ForceEnd( )
    {
        if (down)
        {
            down = false;
            ReportUp( );
        }

        DeactivateIfActive( );
    }

    private void ActivateFor(UIElement target)
    {
        if (!active)
        {
            SetActiveSource(PresentationSource.FromVisual(target));
            Activate( );
            active = true;
        }

        // 预先捕获到目标元素：CaptureMode.Element 下 DirectlyOver 恒为目标，跳过命中测试，
        // 使上报坐标无需落在视口内（测试可任意放大间距以覆盖 d > l 的 MultiDraw 判定）。
        Capture(target);
    }

    private void DeactivateIfActive( )
    {
        if (!active)
        {
            return;
        }

        active = false;
        Deactivate( );
    }
}
