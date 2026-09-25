using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using PDFiumCore;

namespace LightBoard;

internal sealed class PdfSource : PageSource
{
    // PDFium 是进程级库，首次使用前初始化一次；FPDF_DestroyLibrary 由进程退出回收。
    static PdfSource( )
    {
        fpdfview.FPDF_InitLibrary( );
    }

    private const ulong FpdfErrPassword = 4; // FPDF_ERR_PASSWORD：文档已加密

    private const double Dpi = 144;

    private readonly FpdfDocumentT document;

    private PdfSource(FpdfDocumentT document)
    {
        this.document = document;
        PageCount = fpdfview.FPDF_GetPageCount(document);
    }

    public override int PageCount { get; }

    public static PdfSource Open(string path)
    {
        var document = fpdfview.FPDF_LoadDocument(path, "");
        if (document is null)
        {
            var errorCode = fpdfview.FPDF_GetLastError( );
            if (errorCode == FpdfErrPassword)
            {
                throw new InvalidDataException("该 PDF 已加密，暂不支持打开");
            }

            throw new InvalidDataException($"无法打开 PDF 文件：{errorCode:X}");
        }

        return new PdfSource(document);
    }

    public override ImageSource? GetPage(int index)
    {
        if ((uint) index >= (uint) PageCount)
        {
            return null;
        }

        var page = fpdfview.FPDF_LoadPage(document, index);
        if (page is null)
        {
            return null;
        }

        try
        {
            using var size = new FS_SIZEF_( );
            fpdfview.FPDF_GetPageSizeByIndexF(document, index, size);
            var pxW = Math.Max(1, (int) Math.Round(size.Width * Dpi / 72));
            var pxH = Math.Max(1, (int) Math.Round(size.Height * Dpi / 72));

            var bitmap = fpdfview.FPDFBitmapCreateEx(pxW, pxH, (int) FPDFBitmapFormat.BGRA, IntPtr.Zero, 0);
            if (bitmap is null)
            {
                return null;
            }

            try
            {
                fpdfview.FPDFBitmapFillRect(bitmap, 0, 0, pxW, pxH, 0xFFFFFFFF);
                const float Scale = (float) (Dpi / 72);
                using var matrix = new FS_MATRIX_ { A = Scale, B = 0, C = 0, D = Scale, E = 0, F = 0 };
                using var clip = new FS_RECTF_ { Left = 0, Right = pxW, Bottom = 0, Top = pxH };
                fpdfview.FPDF_RenderPageBitmapWithMatrix(bitmap, page, matrix, clip, (int) RenderFlags.RenderAnnotations);

                var bytes = new byte[fpdfview.FPDFBitmapGetStride(bitmap) * pxH];
                Marshal.Copy(fpdfview.FPDFBitmapGetBuffer(bitmap), bytes, 0, bytes.Length);

                var image = BitmapSource.Create(pxW, pxH, Dpi, Dpi, PixelFormats.Pbgra32, null, bytes, pxW * 4);
                image.Freeze( );
                return image;
            }
            finally
            {
                fpdfview.FPDFBitmapDestroy(bitmap);
            }
        }
        finally
        {
            fpdfview.FPDF_ClosePage(page);
        }
    }

    public override void Dispose( )
    {
        fpdfview.FPDF_CloseDocument(document);
    }
}
