using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasNext.Tests;

/// <summary>
/// 矩形框选：按下点与当前点为对角线，矩形内笔画命中；按下即抬为点选
/// </summary>
public class SelectionRectangleTests
{
    private static readonly InkCanvasNextOptions RectangleOptions = new( ) { SelectionDragMode = SelectionDragMode.Rectangle };

    [Fact]
    public void Rectangle_SelectsStrokesInsideBounds( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost(options: RectangleOptions);
            host.Canvas.Mode = InkCanvasNextMode.Select;

            var first = MakeStroke(new Point(200, 150), new Point(250, 350));
            var second = MakeStroke(new Point(600, 150), new Point(650, 350));
            host.Canvas.Strokes.Add(first);
            host.Canvas.Strokes.Add(second);

            Drag(host, new Point(150, 100), new Point(300, 400));

            Assert.Same(first, Assert.Single(host.Canvas.SelectedStrokes));
        });
    }

    [Fact]
    public void Rectangle_OppositeDiagonal_SelectsSame( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost(options: RectangleOptions);
            host.Canvas.Mode = InkCanvasNextMode.Select;

            var first = MakeStroke(new Point(200, 150), new Point(250, 350));
            host.Canvas.Strokes.Add(first);

            Drag(host, new Point(300, 400), new Point(150, 100));

            Assert.Same(first, Assert.Single(host.Canvas.SelectedStrokes));
        });
    }

    [Fact]
    public void Rectangle_Tap_OnStroke_PointSelects( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost(options: RectangleOptions);
            host.Canvas.Mode = InkCanvasNextMode.Select;

            var first = MakeStroke(new Point(220, 150), new Point(220, 350));
            host.Canvas.Strokes.Add(first);

            var finger = host.Device( );
            finger.Down(host.Target, new Point(220, 250));
            finger.Up(host.Target, new Point(220, 250));

            Assert.Same(first, Assert.Single(host.Canvas.SelectedStrokes));
        });
    }

    [Fact]
    public void Rectangle_Tap_OnBlank_ClearsSelection( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost(options: RectangleOptions);
            host.Canvas.Mode = InkCanvasNextMode.Select;

            var first = MakeStroke(new Point(200, 150), new Point(250, 350));
            host.Canvas.Strokes.Add(first);

            Drag(host, new Point(150, 100), new Point(300, 400));
            Assert.NotEmpty(host.Canvas.SelectedStrokes);

            var finger = host.Device( );
            finger.Down(host.Target, new Point(900, 500));
            finger.Up(host.Target, new Point(900, 500));

            Assert.Empty(host.Canvas.SelectedStrokes);
        });
    }

    private static void Drag(TouchHost host, Point start, Point end)
    {
        var finger = host.Device( );
        finger.Down(host.Target, start);
        finger.Move(host.Target, end);
        finger.Up(host.Target, end);
    }

    private static Stroke MakeStroke(params Point[] points)
    {
        return new Stroke(
            [.. points.Select(p => new StylusPoint(p.X, p.Y, 0.5f))],
            new DrawingAttributes { Color = Colors.Black, Width = 3, Height = 3 });
    }
}
