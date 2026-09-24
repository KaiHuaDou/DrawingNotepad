using System;
using System.ComponentModel;
using System.IO;
using System.Windows;

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

    private void AttachBoard(string fileName)
    {
        // 空白板（无页）不切换，避免跳到既有末页
        var first = App.Pages.Count;
        App.AttachBoard(fileName);
        if (first < App.Pages.Count)
        {
            SaveCurrentViewToPage( );
            App.SwitchPage(first);
            Dirty = true;
        }
    }

    private void AttachClick(object o, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new( ) { Filter = FileFilter };
        if (dialog.ShowDialog( ) != true)
        {
            return;
        }

        try
        {
            var fileName = dialog.FileName;

            if (IsBoardFile(fileName))
            {
                AttachBoard(fileName);
            }
            else if (IsInkFile(fileName))
            {
                AttachInk(fileName);
            }
            else
            {
                AttachDocument(fileName);
            }
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            App.ShowDetailedInfo("无法附加文件", ex.Message, $"{ex.Message}\n{ex.StackTrace}");
        }
    }

    private async void AttachDocument(string fileName)
    {
        if (App.Document is not null)
        {
            // 已有文档时附加等价于替换式打开，先走保存确认
            if (WhetherCloseFile( ))
            {
                return;
            }

            OpenFile(fileName);
            return;
        }

        CanvasNext.IsEnabled = false;
        ShowLoading("正在附加文档");

        try
        {
            var document = await App.AttachDocument(fileName);

            // 不走 OnPageChanged：ApplyStrokes 会清空撤销历史。背景立即应用；视图仅在对中文档（无墨迹）时
            // 套用 Page 中的居中参数，有墨迹时不动画布，避免把上次换页/保存的快照回写到画布
            CanvasNext.SetDocumentPage(document.GetPage(App.PageIndex));
            if (App.CurrentPage.Strokes.Count == 0)
            {
                CanvasNext.CurrentScale = App.CurrentPage.Scale;
                CanvasNext.OffsetX = App.CurrentPage.OffsetX;
                CanvasNext.OffsetY = App.CurrentPage.OffsetY;
            }

            // 附加在页与文档就位时已成立，缩略图刷新失败仅记录（切页时会重新生成）
            try
            {
                await App.RefreshDocumentPreviewsAsync( );
            }
            catch (Exception ex)
            {
                App.LogException(ex);
            }
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            App.ShowDetailedInfo("无法附加文档", ex.Message, $"{ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            CanvasNext.IsEnabled = true;
            HideLoading( );
        }
    }

    private void AttachInk(string fileName)
    {
        using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
        CanvasNext.Strokes.Add([with(stream)]);
        // 直接向活动集合追加不经过 ApplyStrokes，画布尺寸保障需单独调用
        CanvasNext.EnsureStrokesFit( );
    }

    private void CloseDocumentViewer( )
    {
        // 先清空页面视图再释放 XPS，否则已渲染的页面会失效。
        CanvasNext.SetDocumentPage(null);

        App.Document?.Dispose( );
        App.Document = null;
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
                BoardFile.Write(Path.Join(App.AppPath, "fastsave", $"{DateTime.Now:yyyyMMdd-HHmmss}.lbf"), App.Pages);
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
