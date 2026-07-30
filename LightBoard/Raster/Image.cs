using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LightBoard.Raster;

public static class Image
{
    public static byte[] ToArray(Stream stream)
    {
        stream.Position = 0;
        using var copy = new MemoryStream( );
        stream.CopyTo(copy);
        return copy.ToArray( );
    }

    public static BitmapImage FromStream(Stream stream)
    {
        stream.Position = 0;
        var bitmap = new BitmapImage( );
        bitmap.BeginInit( );
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit( );
        bitmap.Freeze( );
        return bitmap;
    }

    public static RenderTargetBitmap Error(string message)
    {
        const int Width = 1920;
        const int Height = 1080;
        const double FontSize = 19;

        var typeface = new Typeface(
            new FontFamily("Segoe UI"), FontStyles.Normal,
            FontWeights.Normal, FontStretches.Normal
        );

        var visual = new DrawingVisual( );
        using var context = visual.RenderOpen( );

        context.DrawRectangle(Brushes.White, null, new Rect(0, 0, Width, Height));

        var text = new FormattedText(
            message,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            FontSize,
            Brushes.Red,
            pixelsPerDip: 1.0)
        {
            MaxTextWidth = Width - 40,
            MaxTextHeight = Height - 40,
            TextAlignment = TextAlignment.Center
        };
        var x = (Width - text.Width) / 2.0;
        var y = (Height - text.Height) / 2.0;
        context.DrawText(text, new Point(x, y));

        var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze( );
        return bitmap;
    }
}
