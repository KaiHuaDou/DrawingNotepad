#pragma warning disable CA1508

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
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
            var bitmaps = new List<FpdfBitmapT>( );
            try
            {
                for (var i = 0; i < pages.Count; i++)
                {
                    var bitmap = AddPage(pdf, i, pages[i], FetchBackground(document, i), dpi, scale);
                    bitmaps.Add(bitmap);
                }

                Save(pdf, path);
            }
            finally
            {
                // 页面位图必须存活到保存结束：PDFium 在写文档时才读取图像像素，过早销毁会写出损坏的图像流。
                foreach (var bitmap in bitmaps)
                {
                    fpdfview.FPDFBitmapDestroy(bitmap);
                }
            }
        }
        finally
        {
            fpdfview.FPDF_CloseDocument(pdf);
        }
    }

    private static FpdfBitmapT AddPage(FpdfDocumentT pdf, int index, Page page, ImageSource? background, DpiScale dpi, int scale)
    {
        var content = page.Strokes.Render(dpi, scale, App.CanvasSize, Brushes.White, background);
        var ptW = content.PixelWidth * 72d / dpi.PixelsPerInchX;
        var ptH = content.PixelHeight * 72d / dpi.PixelsPerInchY;

        var pdfPage = fpdf_edit.FPDFPageNew(pdf, index, ptW, ptH) ?? throw new InvalidDataException("无法创建 PDF 页面");
        try
        {
            var image = fpdf_edit.FPDFPageObjNewImageObj(pdf);

            // 画布底色不透明，预乘色与直通色一致，Pbgra32 像素可直接整块拷入 BGRA 位图。
            var bitmap = fpdfview.FPDFBitmapCreateEx(content.PixelWidth, content.PixelHeight, (int) FPDFBitmapFormat.BGRA, IntPtr.Zero, 0)
                ?? throw new InvalidDataException($"创建页位图失败：{fpdfview.FPDF_GetLastError( ):X}");

            var pixels = new byte[content.PixelWidth * content.PixelHeight * 4];
            content.CopyPixels(pixels, content.PixelWidth * 4, 0);
            Marshal.Copy(pixels, 0, fpdfview.FPDFBitmapGetBuffer(bitmap), pixels.Length);

            if (fpdf_edit.FPDFImageObjSetBitmap(pdfPage, 1, image, bitmap) == 0)
            {
                var errorCode = fpdfview.FPDF_GetLastError( );
                throw new InvalidDataException($"内嵌页面图像失败：{errorCode:X}");
            }

            // 位图按 72/PPI 折算为 PDF 页面点尺寸，保证导出物理尺寸与显示一致（所见即所得）。
            using var matrix = new FS_MATRIX_ { A = (float) ptW, B = 0, C = 0, D = (float) ptH, E = 0, F = 0 };
            fpdf_edit.FPDFPageObjSetMatrix(image, matrix);
            if (fpdf_edit.FPDFPageInsertObject(pdfPage, image) == 0)
            {
                var errorCode = fpdfview.FPDF_GetLastError( );
                throw new InvalidDataException($"页面图像插入失败：{errorCode:X}");
            }

            if (fpdf_edit.FPDFPageGenerateContent(pdfPage) == 0)
            {
                var errorCode = fpdfview.FPDF_GetLastError( );
                throw new InvalidDataException($"生成页面内容失败：{errorCode:X}");
            }

            return bitmap;
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

        Exception? error = null;

        using var writer = new FPDF_FILEWRITE_
        {
            Version = 1,
            WriteBlock = (_, data, size) =>
            {
                try
                {
                    var count = checked((int) size);
                    if (count > buffer.Length)
                    {
                        buffer = new byte[count];
                    }

                    Marshal.Copy(data, buffer, 0, count);
                    stream.Write(buffer, 0, count);

                    // WriteBlock 约定：非 0 表示成功、0 表示错误（见 fpdf_save.h）。
                    return 1;
                }
                catch (Exception ex)
                {
                    error = ex;
                    return 0;
                }
            },
        };

        var saved = fpdf_save.FPDF_SaveAsCopy(pdf, writer, 0) == 0;
        if (error is not null)
        {
            ExceptionDispatchInfo.Capture(error).Throw( );
        }

        if (saved)
        {
            throw new InvalidDataException($"PDF 保存失败，FPDF 错误码：{fpdfview.FPDF_GetLastError( )}");
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
