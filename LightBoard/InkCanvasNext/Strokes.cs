using System;
using System.IO;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    /// <summary>
    /// 获取当前选中的笔画数量。
    /// </summary>
    public int SelectedCount => selection.SelectedStrokes.Count;

    /// <summary>
    /// 获取当前选中的笔画集合副本。
    /// </summary>
    public StrokeCollection SelectedStrokes => [with(selection.SelectedStrokes)];

    /// <summary>
    /// 获取是否存在选中笔画。
    /// </summary>
    public bool HasSelection => selection.HasSelection;

    /// <summary>
    /// 复制选中笔画到剪贴板。
    /// </summary>
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

    /// <summary>
    /// 剪切选中笔画到剪贴板并从画布删除。
    /// </summary>
    public void CutSelected( )
    {
        if (!HasSelection)
        {
            return;
        }

        CopySelected( );
        DeleteSelected( );
    }

    /// <summary>
    /// 删除选中的笔画。
    /// </summary>
    public void DeleteSelected( )
    {
        if (!HasSelection)
        {
            return;
        }

        InnerCanvas.Strokes.Remove(SelectedStrokes);
        selection.Clear( );
    }

    /// <summary>
    /// 在指定位置克隆一份选区笔画。
    /// </summary>
    public void StampCloneAt(Point point)
    {
        if (!HasSelection)
        {
            return;
        }

        var clone = SelectedStrokes.Clone( );
        CenterAt(clone, point);
        InnerCanvas.Strokes.Add(clone);
    }

    /// <summary>
    /// 在指定位置粘贴剪贴板中的墨迹笔画。
    /// </summary>
    public void StampPasteAt(Point point)
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
        CenterAt(strokes, point);
        InnerCanvas.Strokes.Add(strokes);
    }

    private static void CenterAt(StrokeCollection strokes, Point point)
    {
        var bounds = strokes.GetBounds( );
        if (bounds.IsEmpty)
        {
            return;
        }

        var offsetX = point.X - (bounds.Left + bounds.Width / 2);
        var offsetY = point.Y - (bounds.Top + bounds.Height / 2);
        var matrix = new Matrix(1, 0, 0, 1, offsetX, offsetY);
        foreach (var stroke in strokes)
        {
            stroke.Transform(matrix, false);
        }
    }
}

internal static class StrokeCollectionExtension
{
    private const int PreviewWidth = 170;
    private const int PreviewHeight = PreviewWidth / 16 * 9;
    private static readonly SolidColorBrush Background = new(Color.FromRgb(0x1E, 0x1E, 0x1E));

    static StrokeCollectionExtension( )
    {
        Background.Freeze( );
    }

    internal static RenderTargetBitmap Render(
        this StrokeCollection strokes,
        DpiScale dpi,
        int scale = 100,
        ImageSource? background = null)
    {
        var ratio = scale / 100.0;
        var bounds = strokes.GetBounds( );
        if (background is not null)
        {
            bounds.Union(BackgroundRect(background));
        }

        bounds.Inflate(64, 64);

        var matrix = new Matrix(ratio, 0, 0, ratio,
            -bounds.X * ratio,
            -bounds.Y * ratio);
        var visual = strokes.CreateVisual(
            Background,
            new Rect(0, 0, bounds.Width * ratio, bounds.Height * ratio),
            matrix,
            background);

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

    internal static RenderTargetBitmap PreviewEmpty( )
    {
        var visual = new DrawingVisual( );
        using (var context = visual.RenderOpen( ))
        {
            context.DrawRectangle(Background, null, new Rect(0, 0, PreviewWidth, PreviewHeight));
        }

        var bitmap = new RenderTargetBitmap(PreviewWidth, PreviewHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze( );
        return bitmap;
    }

    internal static RenderTargetBitmap Preview(
        this StrokeCollection strokes,
        ImageSource? background = null)
    {
        var bounds = strokes.GetBounds( );
        if (background is not null)
        {
            bounds.Union(BackgroundRect(background));
        }

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
            Background, new Rect(0, 0, PreviewWidth, PreviewHeight), matrix, background);

        var render = new RenderTargetBitmap(PreviewWidth, PreviewHeight, 96, 96, PixelFormats.Pbgra32);
        render.Render(visual);
        render.Freeze( );
        return render;
    }

    private static DrawingVisual CreateVisual(
        this StrokeCollection strokes,
        Brush background,
        Rect bounds,
        Matrix transform,
        ImageSource? image = null)
    {
        var visual = new DrawingVisual( );
        using (var context = visual.RenderOpen( ))
        {
            context.DrawRectangle(background, null, bounds);
            if (image is not null)
            {
                // 背景按世界坐标矩形绘制，与墨迹共用同一变换，保证叠加位置一致。
                context.PushTransform(new MatrixTransform(transform));
                try
                {
                    context.DrawImage(image, BackgroundRect(image));
                }
                finally
                {
                    context.Pop( );
                }
            }

            foreach (var stroke in strokes)
            {
                var copy = stroke.Clone( );
                copy.Transform(transform, true);
                copy.Draw(context);
            }
        }

        return visual;
    }

    // 文档背景在世界坐标中的矩形：DocumentHost 将背景页居中于 32768×16384 画布，
    // 显示尺寸按图像自身 DPI 折算为 DIP。
    private static Rect BackgroundRect(ImageSource image)
    {
        var bitmap = (BitmapSource) image;
        var width = bitmap.PixelWidth * 96.0 / bitmap.DpiX;
        var height = bitmap.PixelHeight * 96.0 / bitmap.DpiY;
        return new Rect(16384 - width / 2, 8192 - height / 2, width, height);
    }
}
