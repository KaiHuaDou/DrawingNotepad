using System;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using LightBoard.Tests.Toolbar;

namespace LightBoard.Tests;

public class SvgPathTests
{
    [Fact]
    public void From_AllSegmentTypes_WritesMatchingCommands( )
    {
        var figure = new PathFigure(new Point(1, 2),
        [
            new LineSegment(new Point(3, 4), true),
            new PolyLineSegment([new Point(5, 6), new Point(7, 8)], true),
            new BezierSegment(new Point(9, 10), new Point(11, 12), new Point(13, 14), true),
            new PolyBezierSegment([new Point(15, 16), new Point(17, 18), new Point(19, 20)], true),
            new QuadraticBezierSegment(new Point(21, 22), new Point(23, 24), true),
            new PolyQuadraticBezierSegment([new Point(25, 26), new Point(27, 28)], true),
            new ArcSegment(new Point(29, 30), new Size(2, 3), 45, true, SweepDirection.Counterclockwise, true),
        ], true);

        var data = SvgPath.From(new PathGeometry([figure]));

        Assert.Equal("M 1 2L 3 4L 5 6 7 8C 9 10 11 12 13 14C 15 16 17 18 19 20Q 21 22 23 24Q 25 26 27 28A2 3 45 1 0 29 30Z", data.Data);
        Assert.Null(data.Transform);
    }

    [Fact]
    public void From_UnfilledFigure_SkipsIt( )
    {
        var figure = new PathFigure(new Point(1, 2), [new LineSegment(new Point(3, 4), true)], false)
        {
            IsFilled = false,
        };

        Assert.Equal(string.Empty, SvgPath.From(new PathGeometry([figure])).Data);
    }

    [Fact]
    public void From_EmptyGeometry_ReturnsEmptyData( )
    {
        Assert.Equal(string.Empty, SvgPath.From(Geometry.Empty).Data);
    }

    [Fact]
    public void From_NonIdentityTransform_WritesMatrixAttribute( )
    {
        var figure = new PathFigure(new Point(0, 0), [new LineSegment(new Point(10, 0), true)], true);
        var geometry = new PathGeometry([figure]) { Transform = new TranslateTransform(5, 7) };

        var data = SvgPath.From(geometry);

        Assert.Equal("M 0 0L 10 0Z", data.Data);
        Assert.Equal("matrix(1 0 0 1 5 7)", data.Transform);
    }

    [Fact]
    public void From_FourDecimals_MatchesWpfStrokeRendering( )
    {
        UiThread.Run(( ) =>
        {
            var points = new StylusPointCollection( );
            for (var i = 0; i < 40; i++)
            {
                var t = i / 39.0;
                points.Add(new StylusPoint(100 + t * 600, 200 + Math.Sin(t * Math.PI * 2) * 80, (float) (0.2 + 0.75 * t)));
            }

            var stroke = new Stroke(points, new DrawingAttributes
            {
                Color = Colors.Red,
                Width = 8,
                Height = 8,
                FitToCurve = true,
            });

            var data = SvgPath.From(stroke.GetGeometry( ));
            var bounds = stroke.GetBounds( );
            bounds.Inflate(16, 16);

            var reference = Render(bounds, context => stroke.Draw(context));
            var candidate = Render(bounds, context => context.DrawGeometry(
                new SolidColorBrush(Colors.Red), null,
                PathGeometry.CreateFromGeometry(Geometry.Parse("F1" + data.Data))));

            Assert.True(Diff(reference, candidate, out var pixels) * 100.0 / pixels < 0.2);
        });
    }

    private static RenderTargetBitmap Render(Rect bounds, Action<DrawingContext> draw)
    {
        var visual = new DrawingVisual( );
        using (var context = visual.RenderOpen( ))
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, bounds.Width, bounds.Height));
            context.PushTransform(new TranslateTransform(-bounds.X, -bounds.Y));
            draw(context);
            context.Pop( );
        }

        var bitmap = new RenderTargetBitmap(
            Math.Max(1, (int) Math.Ceiling(bounds.Width)),
            Math.Max(1, (int) Math.Ceiling(bounds.Height)),
            96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze( );
        return bitmap;
    }

    private static long Diff(BitmapSource left, BitmapSource right, out int pixels)
    {
        var width = Math.Min(left.PixelWidth, right.PixelWidth);
        var height = Math.Min(left.PixelHeight, right.PixelHeight);
        var a = new byte[left.PixelWidth * left.PixelHeight * 4];
        var b = new byte[right.PixelWidth * right.PixelHeight * 4];
        left.CopyPixels(a, left.PixelWidth * 4, 0);
        right.CopyPixels(b, right.PixelWidth * 4, 0);

        pixels = width * height;
        long diff = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = (y * left.PixelWidth + x) * 4;
                var j = (y * right.PixelWidth + x) * 4;
                if (Math.Abs(a[i] - b[j]) > 2
                    || Math.Abs(a[i + 1] - b[j + 1]) > 2
                    || Math.Abs(a[i + 2] - b[j + 2]) > 2)
                {
                    diff++;
                }
            }
        }

        return diff;
    }
}
