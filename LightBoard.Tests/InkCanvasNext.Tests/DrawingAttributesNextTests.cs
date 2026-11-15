using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasNext.Tests;

/// <summary>
/// 工具设置写入口：归一化、同值早退、克隆隔离与操作载荷
/// </summary>
public class DrawingAttributesNextTests
{
    private static readonly Point Start = new(160, 300);

    [Fact]
    public void Assignment_InvalidCombination_Normalizes( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;

            canvas.DrawingAttributes = canvas.DrawingAttributes with
            {
                Mode = InkCanvasNextMode.Line,
                Stamp = StampAction.Clone
            };

            Assert.Equal(InkCanvasNextMode.Line, canvas.DrawingAttributes.Mode);
            Assert.Equal(StampAction.None, canvas.DrawingAttributes.Stamp);
        });
    }

    [Fact]
    public void Assignment_HighlighterPassesThrough( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;
            var attributes = canvas.DrawingAttributes.DrawingAttributes;
            attributes.Color = Colors.Yellow;
            attributes.Width = attributes.Height = 36;
            attributes.IsHighlighter = true;
            attributes.IgnorePressure = true;

            canvas.DrawingAttributes = canvas.DrawingAttributes with { DrawingAttributes = attributes };

            Assert.Equal(InkCanvasNextMode.Ink, canvas.DrawingAttributes.Mode);
            Assert.True(canvas.DrawingAttributes.DrawingAttributes.IsHighlighter);
            Assert.Equal(Colors.Yellow, canvas.DrawingAttributes.DrawingAttributes.Color);
            Assert.Equal(InkCanvasEditingMode.Ink, canvas.InnerCanvas.EditingMode);
        });
    }

    [Fact]
    public void SameValue_DoesNotRaise( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;
            var raised = 0;
            canvas.DrawingAttributesChanged += (_, _) => raised++;

            canvas.DrawingAttributes = canvas.DrawingAttributes;
            canvas.DrawingAttributes = canvas.DrawingAttributes with
            {
                DrawingAttributes = canvas.DrawingAttributes.DrawingAttributes
            };

            Assert.Equal(0, raised);

            var attributes = canvas.DrawingAttributes.DrawingAttributes;
            attributes.Color = Colors.Red;
            canvas.DrawingAttributes = canvas.DrawingAttributes with { DrawingAttributes = attributes };

            Assert.Equal(1, raised);
        });
    }

    [Fact]
    public void Clones_IsolateSourceAndReader( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;
            var attributes = canvas.DrawingAttributes.DrawingAttributes;
            canvas.DrawingAttributes = canvas.DrawingAttributes with { DrawingAttributes = attributes };

            attributes.Color = Colors.Red;
            Assert.NotEqual(Colors.Red, canvas.DrawingAttributes.DrawingAttributes.Color);

            var read = canvas.DrawingAttributes.DrawingAttributes;
            read.Color = Colors.Blue;
            Assert.NotEqual(Colors.Blue, canvas.DrawingAttributes.DrawingAttributes.Color);
        });
    }

    [Fact]
    public void ApplyStrokes_ClearsStamp_RaisesEvent( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;
            canvas.DrawingAttributes = canvas.DrawingAttributes with { Stamp = StampAction.Clone };
            var raised = 0;
            canvas.DrawingAttributesChanged += (_, _) => raised++;

            canvas.Strokes = [];

            Assert.Equal(StampAction.None, canvas.DrawingAttributes.Stamp);
            Assert.Equal(1, raised);
        });
    }

    [Fact]
    public void OperationCompleted_CarriesDrawingAttributes( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;
            var entries = new List<DrawingAttributesNext>( );
            canvas.OperationCompleted += (_, e) => entries.Add(e.DrawingAttributes);

            var attributes = canvas.DrawingAttributes.DrawingAttributes;
            attributes.IsHighlighter = true;
            canvas.DrawingAttributes = canvas.DrawingAttributes with { DrawingAttributes = attributes };
            canvas.Strokes.Add(MakeStroke( ));

            var strokeEntry = Assert.Single(entries);
            Assert.True(strokeEntry.DrawingAttributes.IsHighlighter);

            entries.Clear( );
            canvas.DrawingAttributes = canvas.DrawingAttributes with
            {
                Mode = InkCanvasNextMode.EraseArea,
                Stamp = StampAction.None,
                DrawingAttributes = DrawingAttributesNext.CreateDefault( )
            };
            var finger = host.Device( );
            finger.Down(host.Target, Start);
            finger.Up(host.Target, Start);

            var eraseEntry = Assert.Single(entries);
            Assert.Equal(InkCanvasNextMode.EraseArea, eraseEntry.Mode);
        });
    }

    [Fact]
    public void Assignment_AppliesEditingMode( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;

            canvas.DrawingAttributes = canvas.DrawingAttributes with { Mode = InkCanvasNextMode.Select };

            Assert.Equal(InkCanvasNextMode.Select, canvas.DrawingAttributes.Mode);
            Assert.Equal(InkCanvasEditingMode.None, canvas.InnerCanvas.EditingMode);

            canvas.DrawingAttributes = canvas.DrawingAttributes with { Mode = InkCanvasNextMode.Ink };

            Assert.Equal(InkCanvasEditingMode.Ink, canvas.InnerCanvas.EditingMode);
        });
    }

    [Fact]
    public void Assignment_PreservesFitToCurve( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost(options: new( ) { FitToCurve = true });
            var canvas = host.Canvas;
            Assert.True(canvas.DrawingAttributes.DrawingAttributes.FitToCurve);

            var attributes = canvas.DrawingAttributes.DrawingAttributes;
            attributes.FitToCurve = false;
            canvas.DrawingAttributes = canvas.DrawingAttributes with { DrawingAttributes = attributes };

            Assert.False(canvas.InnerCanvas.DefaultDrawingAttributes.FitToCurve);
        });
    }

    private static Stroke MakeStroke( )
    {
        return new Stroke(
            [
                new StylusPoint(100, 100, 0.5f),
                new StylusPoint(140, 100, 0.5f)
            ],
            new DrawingAttributes { Color = Colors.Black, Width = 3, Height = 3 });
    }
}
