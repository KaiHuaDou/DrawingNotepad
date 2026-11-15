using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasNext.Tests;

/// <summary>依赖属性的空值处理</summary>
public class PropertyTests
{
    [Fact]
    public void DrawingAttributes_NullAssignment_ResetsToDefault( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;
            var attributes = canvas.DrawingAttributes.DrawingAttributes;
            attributes.Color = Colors.Red;
            canvas.DrawingAttributes = canvas.DrawingAttributes with { DrawingAttributes = attributes };

            canvas.DrawingAttributes = null!;

            Assert.Equal(InkCanvasNextMode.Ink, canvas.DrawingAttributes.Mode);
            Assert.Equal(StampAction.None, canvas.DrawingAttributes.Stamp);
            Assert.Equal(InkCanvasNext.DefaultPenColor, canvas.DrawingAttributes.DrawingAttributes.Color);
        });
    }

    [Fact]
    public void Strokes_NullAssignment_ReplacesWithEmptyCollection( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Strokes.Add(MakeStroke( ));
            Assert.True(host.Canvas.CanUndo);
            var before = host.Canvas.Strokes;

            host.Canvas.Strokes = null!;

            Assert.NotNull(host.Canvas.Strokes);
            Assert.NotSame(before, host.Canvas.Strokes);
            Assert.Empty(host.Canvas.Strokes);
            Assert.False(host.Canvas.CanUndo);
        });
    }

    private static Stroke MakeStroke( )
    {
        return new Stroke(
            [new StylusPoint(100, 100), new StylusPoint(200, 100)],
            new DrawingAttributes { Color = System.Windows.Media.Colors.Black, Width = 3, Height = 3 });
    }
}
