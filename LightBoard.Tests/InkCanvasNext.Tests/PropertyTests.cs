using System.Windows.Ink;
using System.Windows.Input;

namespace InkCanvasNext.Tests;

/// <summary>依赖属性的空值处理。</summary>
public class PropertyTests
{
    [Fact]
    public void DefaultDrawingAttributes_NullAssignment_KeepsPreviousValue( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var attributes = host.Canvas.DefaultDrawingAttributes;

            host.Canvas.DefaultDrawingAttributes = null!;

            Assert.Same(attributes, host.Canvas.DefaultDrawingAttributes);
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
