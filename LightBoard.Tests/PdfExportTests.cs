using System.IO;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using InkCanvasNext;

using LightBoard.Tests.Toolbar;

namespace LightBoard.Tests;

public class PdfExportTests
{
    // 直接调用 PdfWriter，避免与 AppPagesTests 并行修改共享的 BoardState.Pages 造成竞态。
    [Fact]
    public void ExportAllPdf_ReopenContainsAllPagesIncludingInk( )
    {
        // 页快照经 App.Current 派发，用例必须在唯一 App 的线程上执行
        UiThread.Run(( ) =>
        {
            var pages = new List<Page>
            {
                new()
                {
                    Number = 1,
                    Strokes = [new Stroke(
                        [new StylusPoint(50, 50), new StylusPoint(200, 200)],
                        new DrawingAttributes { Color = Colors.Red, Width = 20, Height = 20 })],
                },
                new() { Number = 2 }, // 空页也应写入，保持页序一一对应
            };

            var path = Path.Join(Path.GetTempPath( ), $"lb-test-{Guid.NewGuid( ):N}.pdf");
            try
            {
                PdfWriter.Export(path, pages, new DpiScale(1, 1), 100);

                Assert.True(File.Exists(path));
                Assert.True(new FileInfo(path).Length > 0);

                using var source = PdfSource.Open(path);
                Assert.Equal(2, source.PageCount);

                Assert.True(HasRedPixel(source.GetPage(0)!), "有墨迹的页应包含红色笔画像素");
                Assert.False(HasRedPixel(source.GetPage(1)!), "空页不应有墨迹");
            }
            finally
            {
                File.Delete(path);
            }
        });
    }

    [Fact]
    public void ExportAllPdf_EmptyPage_BackgroundMatchesImageExport( )
    {
        UiThread.Run(( ) =>
        {
            var pages = new List<Page> { new( ) { Number = 1 } };
            var directory = Path.Join(Path.GetTempPath( ), $"lb-test-{Guid.NewGuid( ):N}");
            Directory.CreateDirectory(directory);
            try
            {
                var dpi = new DpiScale(1, 1);
                var pdfPath = Path.Join(directory, "page.pdf");
                var pngPath = Path.Join(directory, "page.png");
                PdfWriter.Export(pdfPath, pages, dpi, 100);

                var snapshot = PageSnapshot.Capture(pages[0]);
                snapshot.Strokes.ExportPng(pngPath, dpi, 100, snapshot.Canvas, snapshot.Background, snapshot.Box);

                using var source = PdfSource.Open(pdfPath);
                using var png = File.OpenRead(pngPath);
                var pngFrame = (BitmapSource) PngBitmapDecoder.Create(png, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
                var pdfFrame = (BitmapSource) source.GetPage(0)!;
                Assert.Equal(
                    PixelColor(pngFrame, pngFrame.PixelWidth / 2, pngFrame.PixelHeight / 2),
                    PixelColor(pdfFrame, pdfFrame.PixelWidth / 2, pdfFrame.PixelHeight / 2));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        });
    }

    private static Color PixelColor(ImageSource image, int x, int y)
    {
        var bitmap = (BitmapSource) image;
        var bytes = new byte[4];
        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), bytes, 4, 0);
        return Color.FromRgb(bytes[2], bytes[1], bytes[0]);
    }

    // Bgra32 字节序：B、G、R、A；红笔画判定取 R 高且 G/B 低，避开底色。
    private static bool HasRedPixel(ImageSource image)
    {
        var bitmap = (BitmapSource) image;
        var bytes = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(bytes, bitmap.PixelWidth * 4, 0);
        for (var i = 0; i < bytes.Length; i += 4)
        {
            if (bytes[i + 2] > 150 && bytes[i + 1] < 100 && bytes[i] < 100)
            {
                return true;
            }
        }

        return false;
    }
}