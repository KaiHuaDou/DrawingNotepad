using System.IO;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasNext.Tests;

/// <summary>
/// 干墨改写：收集完成的笔画末端压力、忽略压力与 ISF 往返
/// </summary>
public class TaperStrokeTests
{
    private static Stroke LineStroke(int count)
    {
        var points = new StylusPointCollection(count);
        for (var i = 0; i < count; i++)
        {
            points.Add(new StylusPoint(i * 5, 100));
        }

        return new Stroke(points, new DrawingAttributes
        {
            Width = 6,
            Height = 6,
            FitToCurve = false,
            Color = Colors.White
        });
    }

    [Fact]
    public void Apply_TapersTail_KeepsHead( )
    {
        StaTest.Run(( ) =>
        {
            var stroke = LineStroke(40);
            Taper.Apply(stroke, new TaperParameters { Points = 8 });

            var points = stroke.StylusPoints;
            Assert.Equal(40, points.Count);
            Assert.Equal(0.5, (double) points[0].PressureFactor, 5);
            Assert.Equal(0.5, (double) points[31].PressureFactor, 5);
            Assert.Equal(0, (double) points[39].PressureFactor, 5);

            for (var i = 32; i < 39; i++)
            {
                Assert.True(points[i].PressureFactor > points[i + 1].PressureFactor);
            }
        });
    }

    [Fact]
    public void Apply_IgnorePressure_KeepsUntouched( )
    {
        StaTest.Run(( ) =>
        {
            var stroke = LineStroke(40);
            stroke.DrawingAttributes.IgnorePressure = true;

            Taper.Apply(stroke, Taper.Effective(new TaperParameters( ), stroke.DrawingAttributes));

            Assert.Equal(0.5, (double) stroke.StylusPoints[39].PressureFactor, 5);
        });
    }

    [Fact]
    public void Apply_ShortStroke_KeepsUntouched( )
    {
        StaTest.Run(( ) =>
        {
            var stroke = LineStroke(2);

            Taper.Apply(stroke, new TaperParameters( ));

            Assert.Equal(0.5, (double) stroke.StylusPoints[1].PressureFactor, 5);
        });
    }

    [Fact]
    public void Apply_NullTaper_KeepsUntouched( )
    {
        StaTest.Run(( ) =>
        {
            var stroke = LineStroke(40);

            Taper.Apply(stroke, null);

            Assert.Equal(0.5, (double) stroke.StylusPoints[39].PressureFactor, 5);
        });
    }

    [Fact]
    public void Apply_IsfRoundTrip_KeepsTailPressure( )
    {
        StaTest.Run(( ) =>
        {
            var stroke = LineStroke(40);
            Taper.Apply(stroke, new TaperParameters { Points = 8 });

            var strokes = new StrokeCollection([stroke]);
            using var stream = new MemoryStream( );
            strokes.Save(stream);
            stream.Position = 0;

            var restored = new StrokeCollection(stream);
            var points = restored[0].StylusPoints;
            Assert.Equal(40, points.Count);
            Assert.Equal(0, (double) points[39].PressureFactor, 4);
            Assert.True(points[39].PressureFactor < points[0].PressureFactor);
        });
    }
}
