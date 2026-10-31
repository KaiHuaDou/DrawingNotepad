using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasNext.Tests;

/// <summary>落章靠近画布边缘时的画布扩展。</summary>
public class StampEdgeTests
{
    [Fact]
    public void StampCloneNearRightEdge_ExtendsCanvas( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.Select;
            host.Canvas.Strokes.Add(new Stroke(
                [new StylusPoint(100, 100, 0.5f), new StylusPoint(200, 100, 0.5f)],
                new DrawingAttributes { Color = Colors.Black, Width = 3, Height = 3 }));
            LassoSelectAll(host);
            Assert.True(host.Canvas.HasSelection);

            var width = host.Canvas.InnerCanvasElement.Width;
            host.Canvas.StampCloneAt(new Point(width - 1, 100));

            Assert.True(host.Canvas.InnerCanvasElement.Width > width);
        });
    }

    private static void LassoSelectAll(TouchHost host)
    {
        var lasso = host.Device( );
        lasso.Down(host.Target, new Point(50, 50));
        lasso.Move(host.Target, new Point(400, 50));
        lasso.Move(host.Target, new Point(400, 300));
        lasso.Move(host.Target, new Point(50, 300));
        lasso.Move(host.Target, new Point(50, 50));
        lasso.Up(host.Target, new Point(50, 50));
    }
}
