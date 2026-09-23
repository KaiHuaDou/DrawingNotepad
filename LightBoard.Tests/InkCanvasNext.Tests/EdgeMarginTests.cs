using System.Windows;

using LightBoard;

namespace InkCanvasNext.Tests;

/// <summary>
/// 平移/缩放手势结束时按视口与画布边缘的距离扩展画布：右/下余量不足一屏则各扩展一屏。
/// 宿主 1280x800、缩放 1，一屏即 1280x800。
/// </summary>
public class EdgeMarginTests
{
    private static readonly Point P1 = new(100, 100);
    private static readonly Point Close2 = new(180, 140);
    private static readonly Point Close3 = new(300, 100);

    [Fact]
    public void Pan_ReleaseAtRightBottomEdge_ExtendsCanvasByOneViewport( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            // 滚动到右下角，视口贴住画布右/下边缘
            host.Canvas.OffsetX = double.MaxValue;
            host.Canvas.OffsetY = double.MaxValue;
            host.Canvas.UpdateLayout( );

            var widthBefore = host.Canvas.InnerCanvas.Width;
            var heightBefore = host.Canvas.InnerCanvas.Height;

            var a = host.Device( );
            var b = host.Device( );
            var c = host.Device( );
            a.Down(host.Target, P1);
            b.Down(host.Target, Close2);
            c.Down(host.Target, Close3);
            Assert.Equal(TouchState.Pan, host.Canvas.State);

            // 手指左移推高滚动偏移但被夹在最大值，视口保持贴住右缘
            a.Move(host.Target, new Point(50, 100));
            a.Up(host.Target, new Point(50, 100));
            b.Up(host.Target, Close2);
            c.Up(host.Target, Close3);
            Assert.Equal(TouchState.Idle, host.Canvas.State);

            Assert.Equal(1280, host.Canvas.InnerCanvas.Width - widthBefore, 5);
            Assert.Equal(800, host.Canvas.InnerCanvas.Height - heightBefore, 5);
        });
    }

    [Fact]
    public void Pan_ReleaseAwayFromEdges_KeepsCanvasSize( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            // 滚动到左上角，右/下余量远超一屏
            host.Canvas.OffsetX = 0;
            host.Canvas.OffsetY = 0;
            host.Canvas.UpdateLayout( );

            var a = host.Device( );
            var b = host.Device( );
            var c = host.Device( );
            a.Down(host.Target, P1);
            b.Down(host.Target, Close2);
            c.Down(host.Target, Close3);
            Assert.Equal(TouchState.Pan, host.Canvas.State);

            // 手指右移压低滚动偏移但被夹在 0，视口保持贴住左缘
            a.Move(host.Target, new Point(150, 100));
            a.Up(host.Target, new Point(150, 100));
            b.Up(host.Target, Close2);
            c.Up(host.Target, Close3);
            Assert.Equal(TouchState.Idle, host.Canvas.State);

            Assert.Equal(App.CanvasSize.Width, host.Canvas.InnerCanvas.Width, 5);
            Assert.Equal(App.CanvasSize.Height, host.Canvas.InnerCanvas.Height, 5);
        });
    }
}
