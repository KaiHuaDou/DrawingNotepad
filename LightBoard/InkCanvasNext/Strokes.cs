using System;
using System.IO;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    public StrokeCollection SelectedStrokes => Canvas.GetSelectedStrokes( );

    public bool HasSelection => Canvas.GetSelectedStrokes( ).Count > 0;

    public void CopySelected( )
    {
        if (!HasSelection)
        {
            return;
        }

        var copy = SelectedStrokes.Clone( );
        using var stream = new MemoryStream( );
        copy.Save(stream);
        var data = new DataObject(StrokeCollection.InkSerializedFormat, stream);
        Clipboard.SetDataObject(data, true);
    }

    public void CutSelected( )
    {
        if (!HasSelection)
        {
            return;
        }

        CopySelected( );
        DeleteSelected( );
    }

    public void DeleteSelected( )
    {
        if (!HasSelection)
        {
            return;
        }

        Canvas.Strokes.Remove(SelectedStrokes);
    }

    public void Paste( )
    {
        if (!Clipboard.ContainsData(StrokeCollection.InkSerializedFormat))
        {
            return;
        }

        var data = Clipboard.GetData(StrokeCollection.InkSerializedFormat);
        if (data is not MemoryStream ms)
        {
            return;
        }

        ms.Position = 0;
        var strokes = new StrokeCollection(ms);

        var centerX = CanvasScroll.HorizontalOffset + CanvasScroll.ViewportWidth / 2;
        var centerY = CanvasScroll.VerticalOffset + CanvasScroll.ViewportHeight / 2;

        var bounds = strokes.GetBounds( );
        if (!bounds.IsEmpty)
        {
            var offsetX = centerX - (bounds.Left + bounds.Width / 2);
            var offsetY = centerY - (bounds.Top + bounds.Height / 2);
            var matrix = new Matrix(1, 0, 0, 1, offsetX, offsetY);
            foreach (var stroke in strokes)
            {
                stroke.Transform(matrix, false);
            }
        }

        Canvas.Strokes.Add(strokes);
    }

    public void CloneSelected( )
    {
        if (!HasSelection)
        {
            return;
        }

        var clone = SelectedStrokes.Clone( );

        var matrix = new Matrix(1, 0, 0, 1, 100, 100);

        foreach (var stroke in clone)
        {
            stroke.Transform(matrix, false);
        }

        Canvas.Strokes.Add(clone);
    }
}

internal static class StrokeCollectionExtension
{
    private const int PreviewWidth = 170;
    private const int PreviewHeight = PreviewWidth / 16 * 9;
    private static readonly SolidColorBrush background = new(Color.FromRgb(0x1E, 0x1E, 0x1E));

    public static RenderTargetBitmap Image(
        this StrokeCollection strokes,
        DpiScale dpi,
        int scale = 100)
    {
        var ratio = scale / 100.0;
        var bounds = strokes.GetBounds( );
        bounds.Inflate(64, 64);

        var matrix = new Matrix(ratio, 0, 0, ratio,
            -bounds.X * ratio,
            -bounds.Y * ratio);
        var visual = strokes.CreateVisual(
            background,
            new Rect(0, 0, bounds.Width * ratio, bounds.Height * ratio),
            matrix);

        var pixelWidth = Math.Max(1, (int) Math.Ceiling(bounds.Width * ratio * dpi.DpiScaleX));
        var pixelHeight = Math.Max(1, (int) Math.Ceiling(bounds.Height * ratio * dpi.DpiScaleY));

        var image = new RenderTargetBitmap(
            pixelWidth, pixelHeight,
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32
        );
        image.Render(visual);
        image.Freeze( );

        return image;
    }

    public static RenderTargetBitmap PreviewEmpty( )
    {
        var visual = new DrawingVisual( );
        using (var context = visual.RenderOpen( ))
        {
            context.DrawRectangle(background, null, new Rect(0, 0, PreviewWidth, PreviewHeight));
        }

        var bitmap = new RenderTargetBitmap(PreviewWidth, PreviewHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze( );
        return bitmap;
    }

    public static RenderTargetBitmap Preview(this StrokeCollection strokes)
    {
        var bounds = strokes.GetBounds( );
        var matrix = Matrix.Identity;

        if (!bounds.IsEmpty)
        {
            var scaleX = PreviewWidth / bounds.Width;
            var scaleY = PreviewHeight / bounds.Height;
            var scale = Math.Min(scaleX, scaleY) * 0.8;

            var centerX = bounds.Left + bounds.Width / 2;
            var centerY = bounds.Top + bounds.Height / 2;
            matrix = new Matrix(scale, 0, 0, scale,
                PreviewWidth / 2 - centerX * scale,
                PreviewHeight / 2 - centerY * scale);
        }

        var visual = strokes.CreateVisual(
            background, new Rect(0, 0, PreviewWidth, PreviewHeight), matrix);

        var render = new RenderTargetBitmap(PreviewWidth, PreviewHeight, 96, 96, PixelFormats.Pbgra32);
        render.Render(visual);
        render.Freeze( );
        return render;
    }

    public static DrawingVisual CreateVisual(
        this StrokeCollection strokes,
        Brush background,
        Rect bounds,
        Matrix transform)
    {
        var visual = new DrawingVisual( );
        using (var context = visual.RenderOpen( ))
        {
            context.DrawRectangle(background, null, bounds);
            foreach (var stroke in strokes)
            {
                var copy = stroke.Clone( );
                copy.Transform(transform, true);
                copy.Draw(context);
            }
        }

        return visual;
    }
}
