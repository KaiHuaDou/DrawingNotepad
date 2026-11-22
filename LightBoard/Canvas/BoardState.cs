using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace LightBoard;

/// <summary>
/// 板级状态：页集合、当前页、底色与修订号；App 只做组合根，不再承载画布数据
/// </summary>
public static class BoardState
{
    private static int pageIndex = -1;

    public static event EventHandler? PageChanged;

    public static Page CurrentPage => Pages[pageIndex];

    public static int PageIndex
    {
        get => pageIndex;
        private set
        {
            pageIndex = value;
            PageChanged?.Invoke(Application.Current?.MainWindow, EventArgs.Empty);
        }
    }

    public static ObservableCollection<Page> Pages { get; } = [];

    /// <summary>
    /// 当前板子的底色；写进 .lbf，随文件一起保存
    /// </summary>
    internal static Underlay Underlay { get; set; }

    /// <summary>
    /// 板子内容（墨迹或页集合）变化的版本号；自动备份据此跳过内容未变化的分钟
    /// </summary>
    internal static int Revision { get; private set; }

    internal static void MarkChanged( )
    {
        Revision++;
    }

    public static void InitializePages( )
    {
        Pages.Add(new Page { Number = 1, Scale = 1.0, OffsetX = CanvasViewport.InitialOffset.X, OffsetY = CanvasViewport.InitialOffset.Y });
        PageIndex = 0;
    }

    public static void SwitchPage(int index)
    {
        // 同索引不重绑：SwapHistory 会用页上的旧快照覆盖当前撤销历史
        if (index < 0 || index >= Pages.Count || index == PageIndex)
        {
            return;
        }

        (Application.Current?.MainWindow as MainWindow)?.SaveCurrentViewToPage( );
        PageIndex = index;
    }

    public static void NewPage( )
    {
        Pages.Add(new Page { Number = Pages.Count + 1, Scale = 1.0, OffsetX = CanvasViewport.InitialOffset.X, OffsetY = CanvasViewport.InitialOffset.Y });
        PageIndex = Pages.Count - 1;
        MarkChanged( );
    }

    public static void DeletePage(int index)
    {
        if (Pages.Count <= 1 || index < 0 || index >= Pages.Count)
        {
            return;
        }

        var current = pageIndex;
        var deletingCurrent = index == current;

        if (deletingCurrent)
        {
            // 移除同步改写页面列表的选中项，下标先落到移除后仍有效的页
            pageIndex = Math.Min(index, Pages.Count - 2);
        }
        else if (index < current)
        {
            // 当前页对象未变，页序前移；重绑会用页上的旧快照盖掉撤销历史
            pageIndex--;
        }

        Pages.RemoveAt(index);
        RenumberPages( );
        MarkChanged( );

        if (deletingCurrent)
        {
            // 页对象已移除，赋值只为发出重绑
            PageIndex = pageIndex;
        }
    }

    public static bool IsBoardEmpty( )
    {
        return !Pages.Any(p => p.Strokes.Count != 0 || p.Background is not null);
    }

    /// <summary>
    /// 整板载入：替换页集合与底色
    /// </summary>
    internal static void Load(IReadOnlyList<BoardPage> pages, Underlay underlay)
    {
        Pages.Clear( );
        foreach (var page in pages)
        {
            AddBoardPage(page);
        }

        Underlay = underlay;
        PageIndex = 0;
    }

    /// <summary>
    /// 追加板文件里的页
    /// </summary>
    internal static void Append(IReadOnlyList<BoardPage> pages)
    {
        foreach (var page in pages)
        {
            AddBoardPage(page);
        }
    }

    /// <summary>
    /// 整批替换页对象：打开文档时每页已带背景，不再从 BoardPage 转换
    /// </summary>
    internal static void LoadPages(IEnumerable<Page> pages)
    {
        Pages.Clear( );
        foreach (var page in pages)
        {
            Pages.Add(page);
        }

        PageIndex = 0;
    }

    internal static BoardPage[] SnapshotPages( )
    {
        return
        [
            .. Pages.Select(page => new BoardPage(
                page.Strokes,
                page.Scale,
                page.OffsetX,
                page.OffsetY,
                page.Background)),
        ];
    }

    private static void RenumberPages( )
    {
        for (var i = 0; i < Pages.Count; i++)
        {
            Pages[i].Number = i + 1;
        }
    }

    private static void AddBoardPage(BoardPage p)
    {
        var page = new Page
        {
            Number = Pages.Count + 1,
            Strokes = p.Strokes,
            Scale = p.Scale,
            OffsetX = p.OffsetX,
            OffsetY = p.OffsetY,
            Background = p.Background,
        };

        if (p.Background is not null)
        {
            page.InvalidatePreview( );
        }

        Pages.Add(page);
        MarkChanged( );
    }
}
