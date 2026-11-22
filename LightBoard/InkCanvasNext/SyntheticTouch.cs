using System;
using System.Windows;
using System.Windows.Input;

namespace InkCanvasNext;

/// <summary>
/// 鼠标或手写笔的合成触点设备：自定义工具下，把指针操作上报为触摸序列
/// 位置以控件坐标系存储，GetTouchPoint 按布局变换与滚动平移换算到目标元素
/// </summary>
internal sealed class SyntheticTouchDevice(InkCanvasNext owner, int id) : TouchDevice(id)
{
    internal const int MouseId = int.MaxValue - 1;
    internal const int PenId = int.MaxValue - 2;

    private bool active;

    internal Point Position { get; private set; }

    internal bool IsDown { get; private set; }

    public override TouchPoint GetTouchPoint(IInputElement? relativeTo)
    {
        var position = relativeTo is UIElement element ? owner.TranslatePoint(Position, element) : Position;

        // 1×1 尺寸落在手掌通道的有效范围之外：合成触点始终按手指处理
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
        Capture(owner.InnerCanvas);
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
