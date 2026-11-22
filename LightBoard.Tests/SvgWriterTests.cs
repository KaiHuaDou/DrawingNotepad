using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace LightBoard.Tests;

public class SvgWriterTests
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";
    private static readonly XNamespace Xlink = "http://www.w3.org/1999/xlink";

    [Fact]
    public void Write_EmptyPage_UsesFallbackBounds( )
    {
        var root = XDocument.Parse(Write(new PageSnapshot(1, new StrokeCollection( ), null, null, null))).Root!;

        Assert.Equal("-64 -64 2048 1208", (string?) root.Attribute("viewBox"));
        Assert.Equal("2048", (string?) root.Attribute("width"));
        Assert.Equal("1208", (string?) root.Attribute("height"));
        Assert.Equal("nonzero", (string?) root.Attribute("fill-rule"));
        Assert.Equal("#1E1E1E", (string?) root.Element(Svg + "rect")!.Attribute("fill"));
    }

    [Fact]
    public void Write_Highlighters_GroupedByColorBeforeSolidInk( )
    {
        Stroke[] strokes =
        [
            MakeStroke(new DrawingAttributes { Color = Colors.Red, Width = 10, Height = 10 }, (10, 10), (50, 50)),
            MakeStroke(Highlighter(Color.FromArgb(0x80, 0xFF, 0xFF, 0x00)), (20, 20), (60, 60)),
            MakeStroke(Highlighter(Color.FromArgb(0x30, 0xFF, 0xFF, 0x00)), (30, 30), (70, 70)),
        ];

        var root = XDocument.Parse(Write(new PageSnapshot(1, new StrokeCollection(strokes), null, null, null))).Root!;
        var children = root.Elements( ).ToArray( );
        var group = Assert.Single(root.Elements(Svg + "g"));
        var solid = Assert.Single(root.Elements(Svg + "path"));

        Assert.Equal("0.5", (string?) group.Attribute("opacity"));
        Assert.Equal("#FFFF00", (string?) group.Attribute("fill"));
        Assert.Equal(2, group.Elements(Svg + "path").Count( ));
        Assert.True(Array.IndexOf(children, group) < Array.IndexOf(children, solid));
    }

    [Fact]
    public void Write_SolidStrokeWithAlpha_WritesFillOpacity( )
    {
        var attributes = new DrawingAttributes
        {
            Color = Color.FromArgb(128, 0, 0, 255),
            Width = 10,
            Height = 10,
        };

        var root = XDocument.Parse(Write(new PageSnapshot(1, new StrokeCollection { MakeStroke(attributes, (10, 10), (50, 50)) }, null, null, null))).Root!;
        var path = Assert.Single(root.Elements(Svg + "path"));

        Assert.Equal("#0000FF", (string?) path.Attribute("fill"));
        Assert.Equal("0.502", (string?) path.Attribute("fill-opacity"));
    }

    [Fact]
    public void Write_SolidStrokeWithoutAlpha_OmitsFillOpacity( )
    {
        var attributes = new DrawingAttributes { Color = Colors.Lime, Width = 10, Height = 10 };

        var root = XDocument.Parse(Write(new PageSnapshot(1, new StrokeCollection { MakeStroke(attributes, (10, 10), (50, 50)) }, null, null, null))).Root!;
        var path = Assert.Single(root.Elements(Svg + "path"));

        Assert.Equal("#00FF00", (string?) path.Attribute("fill"));
        Assert.Null(path.Attribute("fill-opacity"));
    }

    [Fact]
    public void Write_BackgroundImage_EmbeddedAsPngDataUri( )
    {
        var bitmap = BitmapSource.Create(4, 2, 96, 96, PixelFormats.Bgra32, null, new byte[4 * 2 * 4], 4 * 4);
        bitmap.Freeze( );

        var snapshot = new PageSnapshot(1, new StrokeCollection( ), bitmap, new Rect(0, 0, 400, 200), null);
        var root = XDocument.Parse(Write(snapshot)).Root!;
        var image = Assert.Single(root.Elements(Svg + "image"));
        var href = (string?) image.Attribute(Xlink + "href");

        Assert.NotNull(href);
        Assert.StartsWith("data:image/png;base64,", href);
        Assert.Equal("0", (string?) image.Attribute("x"));
        Assert.Equal("0", (string?) image.Attribute("y"));
        Assert.Equal("400", (string?) image.Attribute("width"));
        Assert.Equal("200", (string?) image.Attribute("height"));

        var bytes = Convert.FromBase64String(href["data:image/png;base64,".Length..]);
        using var stream = new MemoryStream(bytes);
        var decoded = BitmapFrame.Create(stream);
        Assert.Equal(4, decoded.PixelWidth);
        Assert.Equal(2, decoded.PixelHeight);
    }

    private static string Write(PageSnapshot snapshot)
    {
        using var writer = new StringWriter( );
        SvgWriter.Write(writer, snapshot);
        return writer.ToString( );
    }

    private static Stroke MakeStroke(DrawingAttributes attributes, params (double X, double Y)[] points)
    {
        var collection = new StylusPointCollection(points.Length);
        foreach (var (x, y) in points)
        {
            collection.Add(new StylusPoint(x, y));
        }

        return new Stroke(collection, attributes);
    }

    private static DrawingAttributes Highlighter(Color color)
    {
        return new DrawingAttributes
        {
            Color = color,
            Width = 36,
            Height = 36,
            StylusTip = StylusTip.Ellipse,
            IgnorePressure = true,
            IsHighlighter = true,
        };
    }
}
