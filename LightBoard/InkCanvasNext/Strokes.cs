using System;
using System.IO;
using System.Linq;
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

        var copy = new StrokeCollection([.. selection.SelectedStrokes.Select(s => s.Clone( ))]);
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

        // 深拷贝必须保留：落章要对副本平移并加入画布，浅引用会拖动原选中笔画
        var clone = new StrokeCollection([.. selection.SelectedStrokes.Select(s => s.Clone( ))]);
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

    /// <summary>
    /// 把笔画落入当前视口：包围盒不完全在可见内容区域内时平移到视口中心，否则按原坐标保留。
    /// </summary>
    public void EnsureStrokesVisible(StrokeCollection strokes)
    {
        // 布局完成前视口尺寸为 0，无法计算可见区域，此时不落位
        if (CanvasScroll.ViewportWidth <= 0 || CanvasScroll.ViewportHeight <= 0)
        {
            return;
        }

        var bounds = strokes.GetBounds( );
        if (bounds.IsEmpty)
        {
            return;
        }

        var view = CurrentView;
        var visible = new Rect(
            view.OffsetX / view.Scale,
            view.OffsetY / view.Scale,
            CanvasScroll.ViewportWidth / view.Scale,
            CanvasScroll.ViewportHeight / view.Scale);
        if (visible.Contains(bounds))
        {
            return;
        }

        CenterAt(strokes, new Point(visible.X + visible.Width / 2, visible.Y + visible.Height / 2));
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

internal static partial class StrokeCollectionExtension
{
    internal static RenderTargetBitmap Render(
        this StrokeCollection strokes,
        DpiScale dpi,
        int scale = 100,
        Brush? canvas = null,
        ImageSource? background = null,
        Rect? box = null)
    {
        var ratio = scale / 100.0;
        var bounds = strokes.GetBounds( );
        if (background is not null)
        {
            bounds.Union(BackgroundRect(box!.Value, background));
        }

        if (bounds.IsEmpty)
        {
            bounds = new Rect(0, 0, FallbackCanvasWidth, FallbackCanvasHeight);
        }

        bounds.Inflate(RenderPadding, RenderPadding);

        var matrix = new Matrix(ratio, 0, 0, ratio,
            -bounds.X * ratio,
            -bounds.Y * ratio);
        var visual = strokes.CreateVisual(
            new Rect(0, 0, bounds.Width * ratio, bounds.Height * ratio),
            matrix,
            canvas ?? Background,
            background,
            box);

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
        ImageSource? background = null,
        Rect? box = null)
    {
        var bounds = strokes.GetBounds( );
        if (background is not null)
        {
            bounds.Union(BackgroundRect(box!.Value, background));
        }

        var matrix = Matrix.Identity;

        if (!bounds.IsEmpty)
        {
            var scaleX = PreviewWidth / bounds.Width;
            var scaleY = PreviewHeight / bounds.Height;
            var scale = Math.Min(scaleX, scaleY) * PreviewContentFill;

            var centerX = bounds.Left + bounds.Width / 2;
            var centerY = bounds.Top + bounds.Height / 2;
            matrix = new Matrix(scale, 0, 0, scale,
                PreviewWidth / 2 - centerX * scale,
                PreviewHeight / 2 - centerY * scale);
        }

        var visual = strokes.CreateVisual(
            new Rect(0, 0, PreviewWidth, PreviewHeight), matrix, Background, background, box);

        var render = new RenderTargetBitmap(PreviewWidth, PreviewHeight, 96, 96, PixelFormats.Pbgra32);
        render.Render(visual);
        render.Freeze( );
        return render;
    }

    private static DrawingVisual CreateVisual(
        this StrokeCollection strokes,
        Rect bounds,
        Matrix transform,
        Brush background,
        ImageSource? image = null,
        Rect? box = null)
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
                    context.DrawImage(image, BackgroundRect(box!.Value, image));
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

    // 文档背景在世界坐标中的矩形：显示尺寸为图像 DPI 折算的 DIP 尺寸等比装进盒子、位置在盒子内居中
    // （与 SetDocumentPage 的布局适配同一公式）。背景与盒子成对提供（App.Document 与 App.DocumentBox 同步赋值）。
    private static Rect BackgroundRect(Rect box, ImageSource image)
    {
        var bitmap = (BitmapSource) image;
        var width = bitmap.PixelWidth * 96.0 / bitmap.DpiX;
        var height = bitmap.PixelHeight * 96.0 / bitmap.DpiY;
        var k = Math.Min(box.Width / width, box.Height / height);
        width *= k;
        height *= k;

        return new Rect(box.X + (box.Width - width) / 2, box.Y + (box.Height - height) / 2, width, height);
    }
}
