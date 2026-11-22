using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace LightBoard;

/// <summary>
/// 自动备份：内容变化的分钟把板子写入 recover/，进程异常退出时同步补写一次
/// </summary>
internal static class BoardRecovery
{
    private static readonly DispatcherTimer Timer = new( );

    private static int savedRevision;

    public static void Start( )
    {
        Timer.Interval = TimeSpan.FromMinutes(1);
        Timer.Tick += (_, _) => SaveInBackground( );
        Timer.Start( );
    }

    /// <summary>
    /// 进程即将退出（未处理异常）时同步写完，不能等后台任务
    /// </summary>
    public static void SaveNow( )
    {
        if (BoardState.IsBoardEmpty( ))
        {
            return;
        }

        Write(
            Path.Join(App.AppPath, "recover", $"{DateTime.Now:yyyyMMdd-HHmmss}.lbf"),
            BoardState.SnapshotPages( ),
            BoardState.Underlay);
    }

    /// <summary>
    /// 定时备份：内容未变化时跳过；交互期间跳过
    /// </summary>
    private static void SaveInBackground( )
    {
        if (Application.Current?.MainWindow is not MainWindow window || window.CanvasNext.IsGestureActive)
        {
            return;
        }

        if (BoardState.IsBoardEmpty( ) || savedRevision == BoardState.Revision)
        {
            return;
        }

        savedRevision = BoardState.Revision;
        var path = Path.Join(App.AppPath, "recover", $"{DateTime.Now:yyyyMMdd-HHmmss}.lbf");
        var pages = BoardState.SnapshotPages( );

        Task.Run(( ) => Write(path, ClonePages(pages), BoardState.Underlay));
    }

    // 笔迹克隆不要求 UI 线程：Stroke 不是 DependencyObject
    private static BoardPage[] ClonePages(BoardPage[] pages)
    {
        return
        [
            .. pages.Select(page => page with
            {
                Strokes = [.. page.Strokes.Select(s => s.Clone( ))],
            }),
        ];
    }

    private static void Write(string path, IReadOnlyList<BoardPage> pages, Underlay underlay)
    {
        try
        {
            Board.Write(path, pages, underlay);
        }
        catch (Exception e)
        {
            App.LogException(e);
        }
    }
}
