using System;
using System.ComponentModel;
using System.IO;
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
        get => field && !BoardState.IsBoardEmpty( );
        set;
    }

    private async Task AttachBoardAsync(string fileName)
    {
        var first = BoardState.Pages.Count;
        var invalid = await RunWithLoading("正在附加", ( ) => BoardPersistence.AttachAsync(fileName));

        // 空白板（无页）不切换，避免跳到既有末页
        if (first < BoardState.Pages.Count)
        {
            BoardState.SwitchPage(first);
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
            await RunWithLoading("正在附加文档", ( ) => DocumentPages.AttachAsync(fileName, CanvasViewport.Capture(CanvasNext)));

            // 不走 OnPageChanged：ApplyStrokes 会清空撤销历史。新页的背景盒取附加时的可见内容区域，
            // 画布视图无需移动；页视图已在 AttachDocument 中与画布视图同步
            var page = BoardState.CurrentPage;
            CanvasNext.SetDocumentPage(DocumentRegistry.Image(page.Background), page.Background?.Box);
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

        DocumentRegistry.CloseAll( );
    }

    private void NewFileClick(object o, RoutedEventArgs e)
    {
        if (WhetherCloseFile( ))
        {
            return;
        }

        CloseDocumentViewer( );
        CanvasNext.ClearMultiTouchVisuals( );
        BoardState.Pages.Clear( );
        // InitializePages 触发 PageChanged，完成墨迹、视图、历史、选区与触摸状态复位
        BoardState.InitializePages( );
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
                    BoardState.SnapshotPages( ),
                    BoardState.Underlay);
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
