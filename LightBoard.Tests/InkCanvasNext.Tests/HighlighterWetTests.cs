using System.Windows;
using System.Windows.Input;

namespace InkCanvasNext.Tests;

/// <summary>
/// 在绘笔画只由一个渲染器显示：忽略压力的笔（荧光笔）留给原生渲染器，湿墨不接管
/// </summary>
public class HighlighterWetTests
{
    [Fact]
    public void Pen_TakesOverLiveInkWithWetLayer( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var inner = host.Canvas.InnerCanvas;
            inner.DefaultDrawingAttributes.IgnorePressure = false;

            host.Target.RaiseEvent(Press( ));

            Assert.True(inner.Wet!.Active);
            Assert.False(inner.UsesNativeRenderer);
        });
    }

    [Fact]
    public void Highlighter_LeavesLiveInkToDynamicRenderer( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var inner = host.Canvas.InnerCanvas;
            inner.DefaultDrawingAttributes.IsHighlighter = true;
            inner.DefaultDrawingAttributes.IgnorePressure = true;

            host.Target.RaiseEvent(Press( ));

            Assert.False(inner.Wet!.Active);
            Assert.True(inner.UsesNativeRenderer);
        });
    }

    // 空 StylusDevice：鼠标事件按无笔服务的机器进入湿墨分支，与运行环境无关
    private static MouseButtonEventArgs Press( )
    {
        return new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left, null)
        {
            RoutedEvent = Mouse.PreviewMouseDownEvent
        };
    }
}
