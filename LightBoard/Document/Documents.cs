using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LightBoard;

/// <summary>
/// 把文档铺成页：打开替换整板，附加从当前页起覆盖并追加
/// </summary>
internal static class DocumentPages
{
    public static async Task OpenAsync(string path, CanvasView view)
    {
        var fullPath = Path.GetFullPath(path);
        var document = await DocumentRegistry.OpenSourceAsync(fullPath);

        var pages = new List<Page>(document.PageCount);
        for (var i = 0; i < document.PageCount; i++)
        {
            pages.Add(CreatePage(pages.Count + 1, view, new DocumentBackground(fullPath, i + 1, view.Box)));
        }

        BoardState.LoadPages(pages);
    }

    public static async Task AttachAsync(string path, CanvasView view)
    {
        var fullPath = Path.GetFullPath(path);
        var document = await DocumentRegistry.OpenSourceAsync(fullPath);

        // 从当前页起铺：已有页按页序取文档对应页，与追加的新页保持同一份背景来源
        var start = BoardState.PageIndex;
        foreach (var page in BoardState.Pages.Skip(start))
        {
            if (page.Number - start > document.PageCount)
            {
                break;
            }

            page.Background = new DocumentBackground(fullPath, page.Number - start, view.Box);
            page.InvalidatePreview( );
        }

        while (BoardState.Pages.Count - start < document.PageCount)
        {
            BoardState.Pages.Add(CreatePage(
                BoardState.Pages.Count + 1,
                view,
                new DocumentBackground(fullPath, BoardState.Pages.Count - start + 1, view.Box)));
        }

        // 当前页视图同步为捕获视图（即画布当前视图），换页往返后背景仍落在视口内
        var current = BoardState.CurrentPage;
        current.Scale = view.Scale;
        current.OffsetX = view.OffsetX;
        current.OffsetY = view.OffsetY;
    }

    private static Page CreatePage(int number, CanvasView view, DocumentBackground background)
    {
        var page = new Page
        {
            Number = number,
            Scale = view.Scale,
            OffsetX = view.OffsetX,
            OffsetY = view.OffsetY,
            Background = background,
        };

        // 文档页即使没有墨迹也有背景缩略图
        page.InvalidatePreview( );
        return page;
    }
}
