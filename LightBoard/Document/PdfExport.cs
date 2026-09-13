using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

using InkCanvasNext;

using PDFiumCore;

namespace LightBoard;

public partial class App
{
    public static void ExportAllPdf(string fileName, DpiScale dpi, int scale)
    {
        PdfWriter.Export(fileName, Pages, Document, dpi, scale);
    }
}

internal static class PdfWriter
{
    static PdfWriter( )
    {
        fpdfview.FPDF_InitLibrary( );
    }

    public static void Export(string path, IList<Page> pages, DocumentService? document, DpiScale dpi, int scale)
    {
        var pdf = fpdf_edit.FPDF_CreateNewDocument( ) ?? throw new InvalidDataException("无法创建 PDF 文档");
        try
        {
            for (var i = 0; i < pages.Count; i++)
            {
                AddPage(pdf, i, pages[i], FetchBackground(document, i), dpi, scale);
            }

            Save(pdf, path);
        }
        finally
        {
            fpdfview.FPDF_CloseDocument(pdf);
        }
    }

    private static void AddPage(FpdfDocumentT pdf, int index, Page page, ImageSource? background, DpiScale dpi, int scale)
    {
        var content = page.Strokes.Render(dpi, scale, background, Brushes.White);
        var ptW = content.PixelWidth * 72d / dpi.PixelsPerInchX;
        var ptH = content.PixelHeight * 72d / dpi.PixelsPerInchY;

        var pdfPage = fpdf_edit.FPDFPageNew(pdf, index, ptW, ptH) ?? throw new InvalidDataException("无法创建 PDF 页面");
        try
        {
            var image = fpdf_edit.FPDFPageObjNewImageObj(pdf);

            // 画布底色不透明，预乘色与直通色一致，Pbgra32 像素可直接整块拷入 BGRA 位图。
            var bitmap = fpdfview.FPDFBitmapCreateEx(content.PixelWidth, content.PixelHeight, (int) FPDFBitmapFormat.BGRA, IntPtr.Zero, 0)
                ?? throw new InvalidDataException("创建页位图失败");
            try
            {
                var pixels = new byte[content.PixelWidth * content.PixelHeight * 4];
                content.CopyPixels(pixels, content.PixelWidth * 4, 0);
                Marshal.Copy(pixels, 0, fpdfview.FPDFBitmapGetBuffer(bitmap), pixels.Length);

                if (fpdf_edit.FPDFImageObjSetBitmap(pdfPage, 1, image, bitmap) == 0)
                {
                    throw new InvalidDataException("内嵌页面图像失败");
                }
            }
            finally
            {
                fpdfview.FPDFBitmapDestroy(bitmap);
            }

            // 位图按 72/PPI 折算为 PDF 页面点尺寸，保证导出物理尺寸与显示一致（所见即所得）。
            using var matrix = new FS_MATRIX_ { A = (float) ptW, B = 0, C = 0, D = (float) ptH, E = 0, F = 0 };
            fpdf_edit.FPDFPageObjSetMatrix(image, matrix);
            if (fpdf_edit.FPDFPageInsertObject(pdfPage, image) == 0)
            {
                throw new InvalidDataException("页面图像插入失败");
            }

            if (fpdf_edit.FPDFPageGenerateContent(pdfPage) == 0)
            {
                throw new InvalidDataException("生成页面内容失败");
            }
        }
        finally
        {
            // 页与页内对象由文档持有，ClosePage 仅释放句柄，失败路径的清理由 CloseDocument 兜底。
            fpdfview.FPDF_ClosePage(pdfPage);
        }
    }

    private static void Save(FpdfDocumentT pdf, string path)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        byte[] buffer = [];

        using var writer = new FPDF_FILEWRITE_
        {
            Version = 1,
            WriteBlock = (_, data, size) =>
            {
                var count = checked((int) size);
                if (count > buffer.Length)
                {
                    buffer = new byte[count];
                }

                Marshal.Copy(data, buffer, 0, count);
                stream.Write(buffer, 0, count);
                return 0;
            },
        };
        if (fpdf_save.FPDF_SaveAsCopy(pdf, writer, 0) == 0)
        {
            throw new InvalidDataException("PDF 保存失败");
        }
    }

    private static ImageSource? FetchBackground(DocumentService? document, int index)
    {
        if (document is null)
        {
            return null;
        }

        return Application.Current.Dispatcher.Invoke(( ) =>
        {
            var page = document.GetPage(index);
            if (page?.CanFreeze == true)
            {
                page.Freeze( );
            }

            return page;
        });
    }
}