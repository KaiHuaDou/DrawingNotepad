using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasNext.Tests;

/// <summary>
/// 选区覆盖层的视觉分层：halo、四条边与手柄各自一个 visual，
/// 拖动时 halo 空、四边仍按选区尺寸绘制，脏区不会扩散到整块选区
/// </summary>
public class SelectionVisualLayoutTests
{
    private const int HaloVisual = 0;
    private const int TopVisual = 1;
    private const int BottomVisual = 2;
    private const int LeftVisual = 3;
    private const int RightVisual = 4;
    private const int RotateVisual = 5;
    private const int LassoVisual = 6;

    [Fact]
    public void Selection_DrawsHaloAndFourEdgeVisuals( )
    {
        StaTest.Run(( ) =>
        {
            using var host = BuildSelectedHost(out var bounds);
            var hostVisual = HostVisual(host);

            Assert.Equal(7, VisualTreeHelper.GetChildrenCount(hostVisual));

            var halo = BoundsOf(hostVisual, HaloVisual);
            var top = BoundsOf(hostVisual, TopVisual);
            var lasso = BoundsOf(hostVisual, LassoVisual);

            Assert.False(halo.IsEmpty, "选中后应绘制 halo");
            Assert.True(lasso.IsEmpty, "套索结束应清空套索轨迹");
            Assert.False(top.IsEmpty, "边框应绘制");
            Assert.True(top.Height < bounds.Height / 2, "上边框的脏区应是窄带，不覆盖选区高度");

            foreach (var index in new[] { BottomVisual, LeftVisual, RightVisual, RotateVisual })
            {
                Assert.False(BoundsOf(hostVisual, index).IsEmpty, "四条边与旋转手柄都应绘制");
            }
        });
    }

    [Fact]
    public void Dragging_HidesHaloAndKeepsEdgeBandsThin( )
    {
        StaTest.Run(( ) =>
        {
            using var host = BuildSelectedHost(out var bounds);
            var hostVisual = HostVisual(host);
            var leftBefore = BoundsOf(hostVisual, LeftVisual);

            var finger = host.Device( );
            finger.Down(host.Target, new Point(250, 250));
            finger.Move(host.Target, new Point(290, 250));
            Assert.Equal(TouchState.Selection, host.Canvas.State);

            var halo = BoundsOf(hostVisual, HaloVisual);
            var top = BoundsOf(hostVisual, TopVisual);
            var left = BoundsOf(hostVisual, LeftVisual);

            Assert.True(halo.IsEmpty, "拖动中不绘制 halo");
            Assert.False(top.IsEmpty, "拖动中边框跟随选区");
            Assert.True(top.Height < bounds.Height / 2, "上边框的脏区应是窄带");
            Assert.True(left.Width < bounds.Width / 2, "左边框的脏区应是窄带");
            Assert.Equal(40, left.Left - leftBefore.Left, 6);
        });
    }

    /// <summary>套索被多指手势接管后图层清空，不把半途路径留在画面上</summary>
    [Fact]
    public void AbandonedLasso_LeavesNoTrail( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.DrawingAttributes = host.Canvas.DrawingAttributes with { Mode = InkCanvasNextMode.Select };
            host.Canvas.Strokes.Add(new Stroke(
                [new StylusPoint(200, 100, 0.5f), new StylusPoint(300, 400, 0.5f)],
                new DrawingAttributes { Color = Colors.Black, Width = 3, Height = 3 }));

            var hostVisual = HostVisual(host);
            var lasso = host.Device( );
            lasso.Down(host.Target, new Point(60, 60));
            lasso.Move(host.Target, new Point(140, 60));
            lasso.Move(host.Target, new Point(140, 140));
            Assert.False(BoundsOf(hostVisual, LassoVisual).IsEmpty, "拖动中应绘制套索");

            // 第二指并入把套索交给双指缩放：本次路径放弃，图层清空
            var second = host.Device( );
            second.Down(host.Target, TestPoints.CloseTo(host, new Point(60, 60)));
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);
            Assert.True(BoundsOf(hostVisual, LassoVisual).IsEmpty, "放弃的套索不应留在画面上");
        });
    }

    private static VisualCanvas HostVisual(TouchHost host)
    {
        var layer = host.Canvas.SelectionLayer;
        return Assert.IsType<VisualCanvas>(Assert.Single(layer.Children));
    }

    private static Rect BoundsOf(Visual hostVisual, int index)
    {
        return VisualTreeHelper.GetDescendantBounds((Visual) VisualTreeHelper.GetChild(hostVisual, index));
    }

    /// <summary>建好宿主并套索选中一条竖长笔画，返回该笔画的包围盒</summary>
    private static TouchHost BuildSelectedHost(out Rect bounds)
    {
        var host = new TouchHost( );
        host.Canvas.DrawingAttributes = host.Canvas.DrawingAttributes with { Mode = InkCanvasNextMode.Select };

        var stroke = new Stroke(
            [new StylusPoint(200, 100, 0.5f), new StylusPoint(300, 400, 0.5f)],
            new DrawingAttributes { Color = Colors.Black, Width = 3, Height = 3 });

        host.Canvas.Strokes.Add(stroke);
        bounds = stroke.GetBounds( );

        var lasso = host.Device( );
        lasso.Down(host.Target, new Point(150, 50));
        lasso.Move(host.Target, new Point(360, 50));
        lasso.Move(host.Target, new Point(360, 450));
        lasso.Move(host.Target, new Point(150, 450));
        lasso.Move(host.Target, new Point(150, 50));
        lasso.Up(host.Target, new Point(150, 50));

        Assert.Single(host.Canvas.SelectedStrokes);
        return host;
    }
}
