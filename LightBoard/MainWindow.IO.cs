using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

using Microsoft.Win32;

using Ookii.Dialogs.Wpf;

namespace LightBoard;

public partial class MainWindow
{
    private bool opening;

    private const string FileFilter =
        "可打开的文件|*.lbf;*.isf;*.pptx;*.ppt;*.docx;*.doc;*.xps;*.pdf;*.bmp;*.gif;*.ico;*.jpg;*.jpeg;*.png;*.tiff|轻白板文件|*.lbf|Windows 墨迹文件|*.isf|演示文稿|*.pptx;*.ppt|Word 文档|*.docx;*.doc|XPS 文档|*.xps|PDF 文档|*.pdf|图片|*.bmp;*.gif;*.ico;*.jpg;*.jpeg;*.png;*.tiff|所有文件|*.*";

    private const string ImageFilter = "图片|*.bmp;*.gif;*.ico;*.jpg;*.jpeg;*.png;*.tiff";

    private void OpenFileClick(object o, RoutedEventArgs e)
    {
        if (WhetherCloseFile( ))
        {
            return;
        }

        OpenFileDialog dialog = new( ) { Filter = FileFilter };
        if (dialog.ShowDialog( ) != true)
        {
            return;
        }

        OpenFile(dialog.FileName);
    }

    private void OpenLastBackupClick(object o, RoutedEventArgs e)
    {
        var path = BoardRecovery.FindLatest( );
        if (path is null)
        {
            App.ShowInfo("没有可打开的最后备份");
            return;
        }

        if (WhetherCloseFile( ))
        {
            return;
        }

        OpenFile(path);
    }

    public void RequestOpenFile(string filename)
    {
        if (WhetherCloseFile( ))
        {
            return;
        }

        OpenFile(filename);
    }

    private async void OpenFile(string fileName)
    {
        if (opening)
        {
            return;
        }

        opening = true;
        try
        {
            CloseDocumentViewer( );

            if (IsBoardFile(fileName))
            {
                await LoadBoard(fileName);
            }
            else if (IsInkFile(fileName))
            {
                BoardState.CurrentPage.OpenStrokes(fileName);
                // 整页替换后页视图与画布当前视图同步（不跳视图），墨迹落入视口保证打开即可见
                CanvasNext.EnsureStrokesVisible(BoardState.CurrentPage.Strokes);
                BoardState.CurrentPage.Scale = CanvasNext.CurrentScale;
                BoardState.CurrentPage.OffsetX = CanvasNext.OffsetX;
                BoardState.CurrentPage.OffsetY = CanvasNext.OffsetY;
                OnPageChanged(this, EventArgs.Empty);
            }
            else
            {
                await RunWithLoading("正在打开文档", ( ) => DocumentPages.OpenAsync(fileName, CanvasViewport.Capture(CanvasNext)));
            }

            Dirty = false;
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            App.ShowDetailedInfo("无法打开文档", ex.Message, $"{ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            opening = false;
            HideLoading( );
        }
    }

    private async Task LoadBoard(string fileName)
    {
        var invalid = await RunWithLoading("正在打开", ( ) => BoardPersistence.LoadAsync(fileName));

        SyncUnderlay( );
        ShowInvalidBackgrounds(invalid);
    }

    private async Task RunWithLoading(string tip, Func<Task> action)
    {
        await RunWithLoading(tip, async ( ) =>
        {
            await action( );
            return true;
        });
    }

    // 背景加载可能触发 Office 转换，期间禁用画布
    private async Task<T> RunWithLoading<T>(string tip, Func<Task<T>> action)
    {
        CanvasNext.IsEnabled = false;
        ShowLoading(tip);

        try
        {
            return await action( );
        }
        finally
        {
            CanvasNext.IsEnabled = true;
            HideLoading( );
        }
    }

    private static void ShowInvalidBackgrounds(IReadOnlyList<string> invalid)
    {
        if (invalid.Count == 0)
        {
            return;
        }

        App.ShowInfo("部分页的背景引用无效，已忽略", string.Join("\n", invalid));
    }

    private static bool IsBoardFile(string fileName)
    {
        return Path.GetExtension(fileName).Equals(Board.Ext, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInkFile(string fileName)
    {
        return Path.GetExtension(fileName).Equals(".isf", StringComparison.OrdinalIgnoreCase);
    }

    private void SaveFileClick(object o, RoutedEventArgs e)
    {
        SaveFile( );
    }

    private bool SaveFile( )
    {
        var current = BoardState.CurrentPage;
        current.Scale = CanvasNext.CurrentScale;
        current.OffsetX = CanvasNext.OffsetX;
        current.OffsetY = CanvasNext.OffsetY;

        var dialog = new VistaSaveFileDialog( )
        {
            Filter = "轻白板文件 (*.lbf)|*.lbf",
            DefaultExt = ".lbf",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = $"{DateTime.Now:yyyyMMdd-HHmmss}",
        };
        if (dialog.ShowDialog( ) != true)
        {
            return false;
        }

        try
        {
            Board.Write(dialog.FileName, BoardState.SnapshotPages( ), BoardState.Underlay);
            Dirty = false;
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            App.ShowException(ex, "保存失败。错误日志已记录。");
            return false;
        }

        App.ShowInfo("墨迹已保存");
        return true;
    }
}
