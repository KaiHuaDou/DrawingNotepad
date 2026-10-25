using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace LightBoard;

// PageNumber 为 1 基页码，与文件里的写法一致。
internal sealed record DocumentBackground(string Path, int PageNumber, Rect Box)
{
    public static bool TryParse(string spec, Rect box, out DocumentBackground? background)
    {
        background = null;

        var split = spec.LastIndexOf("::", StringComparison.Ordinal);
        if (split <= 0
            || !int.TryParse(spec.AsSpan(split + 2), NumberStyles.None, CultureInfo.InvariantCulture, out var pageNumber)
            || pageNumber < 1)
        {
            return false;
        }

        background = new DocumentBackground(spec[..split], pageNumber, box);
        return true;
    }

    public override string ToString( )
    {
        return $"{Path}::{PageNumber}";
    }
}

public partial class App
{
    private static readonly Dictionary<string, DocumentService> OpenedDocuments = new(StringComparer.OrdinalIgnoreCase);

    internal static bool HasBackground => Pages.Any(p => p.Background is not null);

    internal static async Task<DocumentService> OpenDocumentSourceAsync(string path)
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

    internal static async Task RegisterBackgroundsAsync(IEnumerable<BoardPage> pages)
    {
        var paths = pages
            .Select(p => p.Background?.Path)
            .OfType<string>( )
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            try
            {
                await OpenDocumentSourceAsync(path);
            }
            catch (Exception e)
            {
                // 源文件缺失或无法转换只影响该来源的背景，板子其余内容照常打开
                LogException(e);
            }
        }
    }

    // XPS 页树有线程亲和性，PDFium 不保证同文档并发取页；取图只在 UI 线程执行。
    internal static ImageSource? BackgroundImage(DocumentBackground? background)
    {
        if (background is null || !OpenedDocuments.TryGetValue(background.Path, out var document))
        {
            return null;
        }

        return document.GetPage(background.PageNumber - 1);
    }

    internal static void CloseDocuments( )
    {
        foreach (var document in OpenedDocuments.Values)
        {
            document.Dispose( );
        }

        OpenedDocuments.Clear( );

        foreach (var page in Pages)
        {
            if (page.Background is not null)
            {
                page.Background = null;
                page.InvalidatePreview( );
            }
        }
    }
}
