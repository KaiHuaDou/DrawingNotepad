using System;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace InkCanvasNext;

/// <summary>
/// 墨迹渲染：导出位图与页面缩略图。
/// </summary>
public static class StrokeCollectionExtension
{
    // TODO: 缩略图尺寸与默认底色仍写死，后续按页面与设置开放
    internal const int PreviewWidth = 300;
    internal const int PreviewHeight = PreviewWidth / 16 * 9;

    internal const double FallbackCanvasWidth = 1920;
    internal const double FallbackCanvasHeight = 1080;

    internal const double RenderPadding = 64;

    private static readonly SolidColorBrush Background = CreateBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));

    /// <summary>
    /// 按缩放比例导出位图；canvas 为画布底色。
    /// </summary>
    public static RenderTargetBitmap Render(
        this StrokeCollection strokes,
        DpiScale dpi,
        int scale = 100,
        Color? canvas = null,
        ImageSource? background = null,
        Rect? box = null)
    {
        var ratio = scale / 100.0;
        var bounds = strokes.GetBounds( );
        if (background is not null && box is { } fitBox)
        {
            bounds.Union(BackgroundRect(fitBox, background));
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
            CanvasBrush(canvas),
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

    /// <summary>
    /// 生成空缩略图；canvas 为底色。
    /// </summary>
    public static RenderTargetBitmap PreviewEmpty(Color? canvas = null)
    {
        var visual = new DrawingVisual( );
        using (var context = visual.RenderOpen( ))
        {
            context.DrawRectangle(CanvasBrush(canvas), null, new Rect(0, 0, PreviewWidth, PreviewHeight));
        }

        var bitmap = new RenderTargetBitmap(PreviewWidth, PreviewHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze( );
        return bitmap;
    }

    /// <summary>
    /// 生成页面缩略图；canvas 为底色。可在后台线程调用，颜色是值类型，不涉及 Freezable 的线程亲和。
    /// </summary>
    public static RenderTargetBitmap Preview(
        this StrokeCollection strokes,
        ImageSource? background = null,
        Rect? box = null,
        Color? canvas = null)
    {
        var bounds = strokes.GetBounds( );
        if (background is not null && box is { } fitBox)
        {
            bounds.Union(BackgroundRect(fitBox, background));
        }

        var matrix = Matrix.Identity;

        if (!bounds.IsEmpty)
        {
            var scaleX = PreviewWidth / bounds.Width;
            var scaleY = PreviewHeight / bounds.Height;
            var scale = Math.Min(scaleX, scaleY);

            var centerX = bounds.Left + bounds.Width / 2;
            var centerY = bounds.Top + bounds.Height / 2;
            matrix = new Matrix(scale, 0, 0, scale,
                PreviewWidth / 2 - centerX * scale,
                PreviewHeight / 2 - centerY * scale);
        }

        var visual = strokes.CreateVisual(
            new Rect(0, 0, PreviewWidth, PreviewHeight), matrix, CanvasBrush(canvas), background, box);

        var render = new RenderTargetBitmap(PreviewWidth, PreviewHeight, 96, 96, PixelFormats.Pbgra32);
        render.Render(visual);
        render.Freeze( );
        return render;
    }

    private static SolidColorBrush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze( );
        return brush;
    }

    private static SolidColorBrush CanvasBrush(Color? color)
    {
        return color is { } value ? CreateBrush(value) : Background;
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
            if (image is not null && box is { } fitBox)
            {
                // 背景按世界坐标矩形绘制，与墨迹共用同一变换，保证叠加位置一致。
                context.PushTransform(new MatrixTransform(transform));
                try
                {
                    context.DrawImage(image, BackgroundRect(fitBox, image));
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

    // 文档背景在世界坐标中的矩形：显示尺寸为图像 DIP 尺寸等比装进盒子、位置在盒子内居中
    // （与 SetDocumentPage 的布局适配同一公式）。
    private static Rect BackgroundRect(Rect box, ImageSource image)
    {
        var width = image.Width;
        var height = image.Height;
        if (!(width > 0) || !(height > 0))
        {
            return box;
        }

        var k = Math.Min(box.Width / width, box.Height / height);
        width *= k;
        height *= k;

        return new Rect(box.X + (box.Width - width) / 2, box.Y + (box.Height - height) / 2, width, height);
    }
}
