using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Ink;

using Microsoft.Win32;

using Ookii.Dialogs.Wpf;

namespace LightBoard;

public partial class MainWindow
{
    private bool Dirty
    {
        get => field && !App.IsBoardEmpty( );
        set;
    }

    private async Task AttachBoardAsync(string fileName)
    {
        var first = App.Pages.Count;
        var invalid = await RunWithLoading("正在附加", ( ) => App.AttachBoardAsync(fileName));

        // 空白板（无页）不切换，避免跳到既有末页
        if (first < App.Pages.Count)
        {
            App.SwitchPage(first);
            Dirty = true;
        }

        ShowInvalidBackgrounds(invalid);
    }

    private async void AttachClick(object o, RoutedEventArgs e)
    {
        if (opening)
        {
            return;
        }

        OpenFileDialog dialog = new( ) { Filter = FileFilter };
        if (dialog.ShowDialog( ) != true)
        {
            return;
        }

        var fileName = dialog.FileName;

        // 有背景时文档附加按替换式打开处理，OpenFile 自带同一把闸
        if (!IsBoardFile(fileName) && !IsInkFile(fileName) && App.HasBackground)
        {
            if (WhetherCloseFile( ))
            {
                return;
            }

            OpenFile(fileName);
            return;
        }

        opening = true;
        try
        {
            if (IsBoardFile(fileName))
            {
                await AttachBoardAsync(fileName);
            }
            else if (IsInkFile(fileName))
            {
                AttachInk(fileName);
            }
            else
            {
                await AttachDocumentAsync(fileName);
            }
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            App.ShowDetailedInfo("无法附加文件", ex.Message, $"{ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            opening = false;
        }
    }

    private async Task AttachDocumentAsync(string fileName)
    {
        try
        {
            await RunWithLoading("正在附加文档", ( ) => App.AttachDocumentAsync(fileName));

            // 不走 OnPageChanged：ApplyStrokes 会清空撤销历史。新页的背景盒取附加时的可见内容区域，
            // 画布视图无需移动；页视图已在 AttachDocument 中与画布视图同步
            var page = App.CurrentPage;
            CanvasNext.SetDocumentPage(App.BackgroundImage(page.Background), page.Background?.Box);
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            App.ShowDetailedInfo("无法附加文档", ex.Message, $"{ex.Message}\n{ex.StackTrace}");
        }
    }

    private void AttachInk(string fileName)
    {
        using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
        StrokeCollection strokes = [with(stream)];
        // 墨迹落入视口：不完全在可见内容区域内时平移到视口中心（与粘贴落点同一语义），保证附加后可见
        CanvasNext.EnsureStrokesVisible(strokes);
        CanvasNext.Strokes.Add(strokes);
    }

    private void CloseDocumentViewer( )
    {
        // 先清空页面视图再释放 XPS，否则已渲染的页面会失效。
        CanvasNext.SetDocumentPage(null, null);

        App.CloseDocuments( );
    }

    private void NewFileClick(object o, RoutedEventArgs e)
    {
        if (WhetherCloseFile( ))
        {
            return;
        }

        CloseDocumentViewer( );
        CanvasNext.ClearMultiTouchVisuals( );
        App.Pages.Clear( );
        // InitializePages 触发 PageChanged，完成墨迹、视图、历史、选区与触摸状态复位
        App.InitializePages( );
        Dirty = false;
    }

    private bool WhetherCloseFile( )
    {
        if (!Dirty)
        {
            return false;
        }

        using TaskDialog dialog = new( )
        {
            WindowTitle = "轻白板",
            MainInstruction = "有未保存的墨迹，是否保存？",
            MainIcon = TaskDialogIcon.Information,
            ButtonStyle = TaskDialogButtonStyle.CommandLinks
        };

        var fastSaveButton = new TaskDialogButton("快速保存");
        var saveButton = new TaskDialogButton("手动保存");
        var discardButton = new TaskDialogButton("放弃");
        var cancelButton = new TaskDialogButton("取消");
        dialog.Buttons.Add(fastSaveButton);
        dialog.Buttons.Add(saveButton);
        dialog.Buttons.Add(discardButton);
        dialog.Buttons.Add(cancelButton);
        var result = dialog.ShowDialog( );

        if (result == fastSaveButton)
        {
            try
            {
                Board.Write(
                    Path.Join(App.AppPath, "fastsave", $"{DateTime.Now:yyyyMMdd-HHmmss}.lbf"),
                    App.SnapshotPages( ));
                Dirty = false;
                return false;
            }
            catch (Exception ex)
            {
                App.ShowException(ex, "无法保存文件，请尝试手动保存");
                return true;
            }
        }
        else if (result == saveButton)
        {
            return !SaveFile( );
        }
        else if (result == discardButton)
        {
            return false;
        }

        return true;
    }

    private void WindowClosing(object o, CancelEventArgs e)
    {
        e.Cancel = WhetherCloseFile( );
    }
}

public partial class App
{
    /// <summary>
    /// 板子内容（墨迹或页集合）变化的版本号；自动备份据此跳过内容未变化的分钟
    /// </summary>
    internal static int BoardRevision { get; set; }

    /// <summary>
    /// 已写入备份的内容版本；等于 <see cref="BoardRevision"/> 时本次备份跳过
    /// </summary>
    private static int RecoverRevision { get; set; }

    public static async Task<IReadOnlyList<string>> AttachBoardAsync(string path)
    {
        var content = Board.Read(path);
        await RegisterBackgroundsAsync(content.Pages);

        foreach (var p in content.Pages)
        {
            AddBoardPage(p);
        }

        return content.InvalidBackgrounds;
    }

    // 背景服务先注册完再动 Pages：OpenFile 在 await 处提前返回时，Pages 不能停在半空状态。
    public static async Task<IReadOnlyList<string>> LoadBoardAsync(string path)
    {
        var content = Board.Read(path);
        await RegisterBackgroundsAsync(content.Pages);

        Pages.Clear( );
        foreach (var p in content.Pages)
        {
            AddBoardPage(p);
        }

        PageIndex = 0;

        return content.InvalidBackgrounds;
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
        BoardRevision++;
    }

    /// <summary>
    /// 进程即将退出（未处理异常）时同步写完，不能等后台任务
    /// </summary>
    private static void SaveRecover( )
    {
        if (IsBoardEmpty( ))
        {
            return;
        }

        WriteRecover(
            Path.Join(AppPath, "recover", $"{DateTime.Now:yyyyMMdd-HHmmss}.lbf"),
            SnapshotPages( ));
    }

    /// <summary>
    /// 定时备份：内容未变化时跳过；交互期间跳过
    /// </summary>
    private static void SaveRecoverInBackground( )
    {
        if (Current.MainWindow is not MainWindow window || window.CanvasNext.IsGestureActive)
        {
            return;
        }

        if (IsBoardEmpty( ) || RecoverRevision == BoardRevision)
        {
            return;
        }

        RecoverRevision = BoardRevision;
        var path = Path.Join(AppPath, "recover", $"{DateTime.Now:yyyyMMdd-HHmmss}.lbf");
        var pages = SnapshotPages( );

        Task.Run(( ) => WriteRecover(path, ClonePages(pages)));
    }

    private static void WriteRecover(string path, IReadOnlyList<BoardPage> pages)
    {
        try
        {
            Board.Write(path, pages);
        }
        catch (Exception e)
        {
            LogException(e);
        }
    }
}
