using System.Windows;

namespace InkCanvasNext.Tests;

// 多指笔画的几何重建挂在帧节拍上：触摸事件只追加点，帧回调统一重绘。
// 笔画集合清空时必须退订，否则 WPF 会为静止内容持续逐帧渲染。
public class MultiTouchFrameTests
{
    private static readonly Point P1 = new(400, 300);

    [Fact]
    public void MoveFarFingers_AwaitsFrame_UntilUp( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var finger1 = host.Device( );
            var finger2 = host.Device( );

            finger1.Down(host.Target, P1);
            finger2.Down(host.Target, TestPoints.FarFrom(host, P1));
            Assert.Equal(TouchState.MultiDraw, host.Canvas.State);
            Assert.False(host.Canvas.AwaitsMultiTouchFrame);

            finger1.Move(host.Target, new Point(410, 300));
            Assert.True(host.Canvas.AwaitsMultiTouchFrame);

            finger1.Up(host.Target, new Point(410, 300));
            finger2.Up(host.Target, TestPoints.FarFrom(host, P1));
            Assert.False(host.Canvas.AwaitsMultiTouchFrame);
        });
    }

    [Fact]
    public void ClearMultiTouchVisuals_StopsFrame( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var finger1 = host.Device( );
            var finger2 = host.Device( );

            finger1.Down(host.Target, P1);
            finger2.Down(host.Target, TestPoints.FarFrom(host, P1));
            finger1.Move(host.Target, new Point(410, 300));
            Assert.True(host.Canvas.AwaitsMultiTouchFrame);

            host.Canvas.ClearMultiTouchVisuals( );

            Assert.False(host.Canvas.AwaitsMultiTouchFrame);
        });
    }
}
