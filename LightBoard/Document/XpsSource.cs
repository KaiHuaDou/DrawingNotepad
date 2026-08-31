using System;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Xps.Packaging;

namespace LightBoard;

internal sealed class XpsSource : PageSource
{
    private readonly XpsDocument xps;
    private readonly FixedDocumentSequence sequence;

    public XpsSource(string path)
    {
        xps = new XpsDocument(path, FileAccess.Read);
        try
        {
            sequence = xps.GetFixedDocumentSequence( );
            PageCount = CountPages(sequence);
        }
        catch
        {
            xps.Close( );
            throw;
        }
    }

    public override int PageCount { get; }

    public override ImageSource? GetPage(int index)
    {
        var i = 0;
        foreach (var docRef in sequence.References)
        {
            var doc = docRef.GetDocument(false);
            if (index < i + doc.Pages.Count)
            {
                var fixedPage = doc.Pages[index - i].GetPageRoot(false);
                return RenderFixedPage(fixedPage);
            }

            i += doc.Pages.Count;
        }

        return null;
    }

    public override void Dispose( )
    {
        // 缓存文件留给下次打开复用，这里只释放 XPS 句柄。
        xps.Close( );
    }

    private static int CountPages(FixedDocumentSequence sequence)
    {
        var count = 0;
        foreach (var docRef in sequence.References)
        {
            count += docRef.GetDocument(false).Pages.Count;
        }

        return count;
    }

    private static RenderTargetBitmap RenderFixedPage(FixedPage fixedPage)
    {
        const double Dpi = 192; // 2x 渲染，放大查看时更清晰
        fixedPage.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = fixedPage.DesiredSize;
        if (size.Width <= 0 || double.IsNaN(size.Width) || size.Height <= 0 || double.IsNaN(size.Height))
        {
            size = new Size(fixedPage.Width, fixedPage.Height);
        }

        fixedPage.Arrange(new Rect(size));
        var pxW = (int) Math.Max(1, Math.Round(size.Width * Dpi / 96d));
        var pxH = (int) Math.Max(1, Math.Round(size.Height * Dpi / 96d));
        var bitmap = new RenderTargetBitmap(pxW, pxH, Dpi, Dpi, PixelFormats.Pbgra32);
        bitmap.Render(fixedPage);
        return bitmap;
    }
}
