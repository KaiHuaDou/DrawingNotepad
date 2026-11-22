using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

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

/// <summary>
/// 单页背景的写入与清除；文档源由 DocumentRegistry 提供
/// </summary>
internal static class PageBackgrounds
{
    /// <summary>
    /// 把当前页的背景换成指定图片；盒子取当前视口，与打开文档时一致
    /// </summary>
    public static async Task SetAsync(Page page, string path, Rect box)
    {
        var fullPath = Path.GetFullPath(path);
        await DocumentRegistry.OpenSourceAsync(fullPath);          // 打不开时抛异常，由调用方提示

        page.Background = new DocumentBackground(fullPath, 1, box);
        page.InvalidatePreview( );
        BoardState.MarkChanged( );
    }

    /// <summary>
    /// 清掉一页的背景
    /// </summary>
    public static void Clear(Page page)
    {
        page.Background = null;
        page.InvalidatePreview( );
        BoardState.MarkChanged( );
    }
}
