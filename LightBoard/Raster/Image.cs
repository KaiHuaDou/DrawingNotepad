using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LightBoard.Raster;

public static class Image
{
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
            new FontFamily("微软雅黑"), FontStyles.Normal,
            FontWeights.Normal, FontStretches.Normal);

        var visual = new DrawingVisual( );
        using var context = visual.RenderOpen( );

        var formattedText = new FormattedText(
            message,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            FontSize,
            Brushes.Red,
            pixelsPerDip: 1.0);

        var x = (Width - formattedText.Width) / 2.0;
        var y = (Height - formattedText.Height) / 2.0;
        context.DrawText(formattedText, new Point(x, y));

        var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }
}
