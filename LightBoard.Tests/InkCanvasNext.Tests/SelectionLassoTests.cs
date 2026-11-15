using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasNext.Tests;

/// <summary>
/// 套索选择：每次从空白处起手都按本次路径重新累积，上一次的选中不参与下一次
/// </summary>
public class SelectionLassoTests
{
    [Fact]
    public void SecondLasso_SelectsOnlyStrokesInsideCurrentPath( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.DrawingAttributes = host.Canvas.DrawingAttributes with { Mode = InkCanvasNextMode.Select };

            var first = MakeStroke(new Point(200, 150), new Point(250, 350));
            var second = MakeStroke(new Point(600, 150), new Point(650, 350));
            host.Canvas.Strokes.Add(first);
            host.Canvas.Strokes.Add(second);

            Lasso(host, new Point(150, 100), new Point(300, 400));
            Assert.Same(first, Assert.Single(host.Canvas.SelectedStrokes));

            Lasso(host, new Point(550, 100), new Point(700, 400));
            Assert.Same(second, Assert.Single(host.Canvas.SelectedStrokes));
        });
    }

    private static void Lasso(TouchHost host, Point topLeft, Point bottomRight)
    {
        var finger = host.Device( );
        finger.Down(host.Target, topLeft);
        finger.Move(host.Target, new Point(bottomRight.X, topLeft.Y));
        finger.Move(host.Target, new Point(bottomRight.X, bottomRight.Y));
        finger.Move(host.Target, new Point(topLeft.X, bottomRight.Y));
        finger.Move(host.Target, topLeft);
        finger.Up(host.Target, topLeft);
    }

    private static Stroke MakeStroke(params Point[] points)
    {
        return new Stroke(
            [.. points.Select(p => new StylusPoint(p.X, p.Y, 0.5f))],
            new DrawingAttributes { Color = Colors.Black, Width = 3, Height = 3 });
    }
}
