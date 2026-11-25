using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using static InkCanvasNext.StrokeCollectionExtension;

namespace LightBoard;

/// <summary>
/// 一页内容写成 SVG 文档
/// </summary>
internal static class SvgWriter
{
    private const double HighlighterOpacity = 0.5;

    public static void Write(string fileName, PageSnapshot snapshot, int scale)
    {
        using var stream = new FileStream(fileName, FileMode.Create, FileAccess.Write);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\n" };
        Write(writer, snapshot, scale);
    }

    public static void Write(TextWriter writer, PageSnapshot snapshot, int scale)
    {
        var bounds = snapshot.Strokes.ContentBounds(snapshot.Background, snapshot.Box);
        WriteRoot(writer, bounds, scale);
        WriteBackground(writer, bounds, snapshot);
        WriteInk(writer, snapshot.Strokes);
        writer.Write("</svg>\n");
    }

    private static void WriteRoot(TextWriter writer, Rect bounds, int scale)
    {
        var width = bounds.Width * scale / 100;
        var height = bounds.Height * scale / 100;

        writer.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
        writer.Write("<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" version=\"1.1\"");
        writer.Write($" width=\"{SvgFormat.Number(width)}\" height=\"{SvgFormat.Number(height)}\"");
        writer.Write($" viewBox=\"{SvgFormat.Number(bounds.X)} {SvgFormat.Number(bounds.Y)} {SvgFormat.Number(bounds.Width)} {SvgFormat.Number(bounds.Height)}\"");
        writer.Write(" fill-rule=\"nonzero\">\n");
    }

    private static void WriteBackground(TextWriter writer, Rect bounds, PageSnapshot snapshot)
    {
        writer.Write($"<rect x=\"{SvgFormat.Number(bounds.X)}\" y=\"{SvgFormat.Number(bounds.Y)}\"");
        writer.Write($" width=\"{SvgFormat.Number(bounds.Width)}\" height=\"{SvgFormat.Number(bounds.Height)}\"");
        writer.Write($" fill=\"{SvgFormat.Color(snapshot.Canvas ?? DefaultCanvasColor)}\"/>\n");

        if (snapshot.Background is BitmapSource bitmap && snapshot.Box is { } box)
        {
            var image = ImageBounds(box, bitmap);
            writer.Write($"<image x=\"{SvgFormat.Number(image.X)}\" y=\"{SvgFormat.Number(image.Y)}\"");
            writer.Write($" width=\"{SvgFormat.Number(image.Width)}\" height=\"{SvgFormat.Number(image.Height)}\"");
            writer.Write($" preserveAspectRatio=\"none\" xlink:href=\"data:image/png;base64,{Encode(bitmap)}\"/>\n");
        }
    }

    private static void WriteInk(TextWriter writer, StrokeCollection strokes)
    {
        foreach (var group in strokes.Where(s => s.DrawingAttributes.IsHighlighter)
            .GroupBy(s => HighlighterColor(s.DrawingAttributes.Color)))
        {
            writer.Write($"<g opacity=\"{SvgFormat.Number(HighlighterOpacity)}\" fill=\"{SvgFormat.Color(group.Key)}\">\n");
            foreach (var stroke in group)
            {
                WritePath(writer, stroke, inheritFill: true);
            }

            writer.Write("</g>\n");
        }

        foreach (var stroke in strokes.Where(s => !s.DrawingAttributes.IsHighlighter))
        {
            WritePath(writer, stroke, inheritFill: false);
        }
    }

    private static void WritePath(TextWriter writer, Stroke stroke, bool inheritFill)
    {
        var path = SvgPath.From(stroke.GetGeometry( ));
        writer.Write("<path d=\"");
        writer.Write(path.Data);
        writer.Write('"');
        if (path.Transform is { } transform)
        {
            writer.Write($" transform=\"{transform}\"");
        }

        if (!inheritFill)
        {
            var color = stroke.DrawingAttributes.Color;
            writer.Write($" fill=\"{SvgFormat.Color(color)}\"");
            if (color.A != byte.MaxValue)
            {
                writer.Write($" fill-opacity=\"{SvgFormat.Number(color.A / 255.0)}\"");
            }
        }

        writer.Write("/>\n");
    }

    // WPF 绘制高亮笔时忽略颜色 alpha，改为整组施加 0.5 不透明度
    private static Color HighlighterColor(Color color)
    {
        return Color.FromArgb(byte.MaxValue, color.R, color.G, color.B);
    }

    private static string Encode(BitmapSource image)
    {
        var encoder = new PngBitmapEncoder( );
        encoder.Frames.Add(BitmapFrame.Create(image));

        using var stream = new MemoryStream( );
        encoder.Save(stream);

        return Convert.ToBase64String(stream.GetBuffer( ), 0, (int) stream.Length);
    }
}
