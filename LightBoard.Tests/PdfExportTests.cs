using System.IO;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LightBoard.Tests;

public class PdfExportTests
{
    // 直接调用 PdfWriter，避免与 AppPagesTests 并行修改共享的 App.Pages 造成竞态。
    [Fact]
    public void ExportAllPdf_ReopenContainsAllPagesIncludingInk()
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
            PdfWriter.Export(path, pages, null, new DpiScale(1, 1), 100);

            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 0);

            using var source = PdfSource.Open(path);
            Assert.Equal(2, source.PageCount);

            Assert.True(HasRedPixel(source.GetPage(0)!), "有墨迹的页应包含红色笔画像素");
            Assert.False(HasRedPixel(source.GetPage(1)!), "空页应为纯白背景");
        }
        finally
        {
            File.Delete(path);
        }
    }

    // Bgra32 字节序：B、G、R、A；红笔画判定取 R 高且 G/B 低，避开白色背景（全 255）。
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