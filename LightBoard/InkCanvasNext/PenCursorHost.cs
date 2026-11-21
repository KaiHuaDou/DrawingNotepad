using System;
using System.Windows;

namespace InkCanvasNext;

// 笔尖悬停圆环：画在视口覆盖层上，窗口失活时隐藏并放弃在途笔接触
public partial class InkCanvasNext
{
    private Window? penCursorWindow;
    private PenCursor penCursor = null!;

    /// <summary>
    /// 笔在范围内悬停时更新圆环；触摸、鼠标或按下期间不显示
    /// </summary>
    private void UpdatePenCursor(Point position)
    {
        if (!options.Pen.HoverCursor
            || !penInRange
            || arbiter.PenDown
            || arbiter.MouseDown
            || touches.Count > 0
            || position.X < 0
            || position.Y < 0
            || position.X > ActualWidth
            || position.Y > ActualHeight)
        {
            HidePenCursor( );
            return;
        }

        penCursor.Show(position, InnerCanvas.DefaultDrawingAttributes.Width * CurrentScale);
    }

    private void HidePenCursor( )
    {
        penCursor.Hide( );
    }

    /// <summary>
    /// 订阅窗口失活：圆环在窗口失活后不能留在屏幕上
    /// </summary>
    private void HookPenCursorWindow( )
    {
        var window = Window.GetWindow(this);

        if (ReferenceEquals(window, penCursorWindow))
        {
            return;
        }

        UnhookPenCursorWindow( );
        penCursorWindow = window;

        penCursorWindow?.Deactivated += PenCursorWindowDeactivated;
    }

    private void UnhookPenCursorWindow( )
    {
        if (penCursorWindow is null)
        {
            return;
        }

        penCursorWindow.Deactivated -= PenCursorWindowDeactivated;
        penCursorWindow = null;
    }

    private void PenCursorWindowDeactivated(object? sender, EventArgs e)
    {
        HidePenCursor( );

        if (arbiter.PenDown || PenPointer.IsDown)
        {
            ResetTouchState( );
        }
    }
}
