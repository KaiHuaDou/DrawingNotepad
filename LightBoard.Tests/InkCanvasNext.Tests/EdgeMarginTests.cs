using System.Windows;

using LightBoard;

namespace InkCanvasNext.Tests;

/// <summary>
/// 画布余量：视图变化（缩放、拖动、恢复视图）后右/下方向不足一屏时按一屏补足
/// 宿主 1280x800、缩放 1，一屏即 1280x800
/// </summary>
public class EdgeMarginTests
{
    private static readonly Point P1 = new(100, 100);

    [Fact]
    public void ZoomToMinScale_KeepsOneViewportMargin( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var viewport = host.Canvas.ViewportSize;

            host.Canvas.CurrentView = new View(InkCanvasNext.MinScale, 0, 0);

            Assert.True(host.Canvas.CanvasSize.Width * InkCanvasNext.MinScale >= viewport.Width * 2);
            Assert.True(host.Canvas.CanvasSize.Height * InkCanvasNext.MinScale >= viewport.Height * 2);
        });
    }

    [Fact]
    public void ZoomToMinScale_ShiftsContentOnce( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var shifts = 0;
            host.Canvas.ContentShifted += (_, _) => shifts++;

            // 四边可能同时需要补足：单次计算只平移一次内容
            host.Canvas.CurrentView = new View(InkCanvasNext.MinScale, 0, 0);
            host.Canvas.UpdateLayout( );

            Assert.Equal(1, shifts);
        });
    }

    [Fact]
    public void SettingsViewNearCanvasEnd_KeepsOneViewportMargin( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var viewport = host.Canvas.ViewportSize;

            // 视图偏移超出当前画布：设置时先夹到边缘，再由扩展补足一屏余量
            host.Canvas.CurrentView = new View(1.0, double.MaxValue, double.MaxValue);
            host.Canvas.UpdateLayout( );

            Assert.True(host.Canvas.CanvasSize.Width >= host.Canvas.OffsetX + viewport.Width * 2);
            Assert.True(host.Canvas.CanvasSize.Height >= host.Canvas.OffsetY + viewport.Height * 2);
        });
    }

    [Fact]
    public void Pan_ToRightBottomEdge_KeepsOneViewportMargin( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var viewport = host.Canvas.ViewportSize;

            // 滚动到右下角：视口贴住边缘时自动补一屏余量
            host.Canvas.OffsetX = double.MaxValue;
            host.Canvas.OffsetY = double.MaxValue;
            host.Canvas.UpdateLayout( );

            var a = host.Device( );
            var b = host.Device( );
            var c = host.Device( );
            var cluster = TestPoints.CloseCluster(host, P1, 3);
            a.Down(host.Target, cluster[0]);
            b.Down(host.Target, cluster[1]);
            c.Down(host.Target, cluster[2]);
            Assert.Equal(TouchState.EvalPan, host.Canvas.State);

            // 手指左移推高滚动偏移但被夹在最大值，视口保持贴住右缘
            a.Move(host.Target, new Point(50, 100));
            a.Up(host.Target, new Point(50, 100));
            b.Up(host.Target, cluster[1]);
            c.Up(host.Target, cluster[2]);
            Assert.Equal(TouchState.Idle, host.Canvas.State);

            host.Canvas.UpdateLayout( );
            Assert.True(host.Canvas.CanvasSize.Width - host.Canvas.OffsetX >= viewport.Width * 2);
            Assert.True(host.Canvas.CanvasSize.Height - host.Canvas.OffsetY >= viewport.Height * 2);
        });
    }

    [Fact]
    public void Pan_ReleaseAwayFromEdges_KeepsCanvasSize( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            // 滚到左上角：空闲期自动补足一屏余量，记录补足后的画布尺寸
            host.Canvas.OffsetX = 0;
            host.Canvas.OffsetY = 0;
            host.Canvas.UpdateLayout( );

            var width = host.Canvas.InnerCanvas.Width;
            var height = host.Canvas.InnerCanvas.Height;

            var a = host.Device( );
            var b = host.Device( );
            var c = host.Device( );
            var cluster = TestPoints.CloseCluster(host, P1, 3);
            a.Down(host.Target, cluster[0]);
            b.Down(host.Target, cluster[1]);
            c.Down(host.Target, cluster[2]);
            Assert.Equal(TouchState.EvalPan, host.Canvas.State);

            // 手指左移推高滚动偏移，远离左/上边缘
            a.Move(host.Target, new Point(50, 100));
            a.Up(host.Target, new Point(50, 100));
            b.Up(host.Target, cluster[1]);
            c.Up(host.Target, cluster[2]);
            Assert.Equal(TouchState.Idle, host.Canvas.State);

            Assert.Equal(width, host.Canvas.InnerCanvas.Width, 5);
            Assert.Equal(height, host.Canvas.InnerCanvas.Height, 5);
        });
    }
}
