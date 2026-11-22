using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LightBoard;

/// <summary>
/// 已打开文档源的注册表：画布、缩略图与导出都从这里取页图
/// </summary>
internal static class DocumentRegistry
{
    private static readonly Dictionary<string, DocumentService> OpenedDocuments = [with(StringComparer.OrdinalIgnoreCase)];

    public static async Task<DocumentService> OpenSourceAsync(string path)
    {
        var fullPath = Path.GetFullPath(path);

        if (OpenedDocuments.TryGetValue(fullPath, out var opened))
        {
            return opened;
        }

        var document = await DocumentService.OpenAsync(fullPath);
        if (document.PageCount == 0)
        {
            document.Dispose( );
            throw new InvalidDataException("文档没有任何页面");
        }

        OpenedDocuments[fullPath] = document;
        return document;
    }

    // 拍照位图已在内存：登记后画布、缩略图与导出直接取内存位图；路径仍指向磁盘 PNG，下次打开走常规解码
    public static void RegisterPhoto(string path, BitmapSource page)
    {
        var fullPath = Path.GetFullPath(path);
        if (!OpenedDocuments.ContainsKey(fullPath))
        {
            OpenedDocuments[fullPath] = DocumentService.FromBitmap(page);
        }
    }

    public static async Task RegisterBackgroundsAsync(IEnumerable<BoardPage> pages)
    {
        var paths = pages
            .Select(p => p.Background?.Path)
            .OfType<string>( )
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            try
            {
                await OpenSourceAsync(path);
            }
            catch (Exception e)
            {
                // 源文件缺失或无法转换只影响该来源的背景，板子其余内容照常打开
                App.LogException(e);
            }
        }
    }

    // XPS 页树有线程亲和性，PDFium 不保证同文档并发取页；取图只在 UI 线程执行。
    public static ImageSource? Image(DocumentBackground? background)
    {
        if (background is null || !OpenedDocuments.TryGetValue(background.Path, out var document))
        {
            return null;
        }

        return document.GetPage(background.PageNumber - 1);
    }

    public static void CloseAll( )
    {
        foreach (var document in OpenedDocuments.Values)
        {
            document.Dispose( );
        }

        OpenedDocuments.Clear( );

        foreach (var page in BoardState.Pages)
        {
            if (page.Background is not null)
            {
                page.Background = null;
                page.InvalidatePreview( );
            }
        }
    }
}
