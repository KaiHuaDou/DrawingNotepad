using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

using Microsoft.Win32;

using Ookii.Dialogs.Wpf;

namespace LightBoard;

public partial class MainWindow
{
    private bool opening;

    private const string FileFilter =
        "可打开的文件|*.lbf;*.isf;*.pptx;*.ppt;*.docx;*.doc;*.xps;*.pdf;*.bmp;*.gif;*.ico;*.jpg;*.jpeg;*.png;*.tiff|轻白板文件|*.lbf|Windows 墨迹文件|*.isf|演示文稿|*.pptx;*.ppt|Word 文档|*.docx;*.doc|XPS 文档|*.xps|PDF 文档|*.pdf|图片|*.bmp;*.gif;*.ico;*.jpg;*.jpeg;*.png;*.tiff|所有文件|*.*";

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
                App.CurrentPage.OpenStrokes(fileName);
                // 整页替换后页视图与画布当前视图同步（不跳视图），墨迹落入视口保证打开即可见
                CanvasNext.EnsureStrokesVisible(App.CurrentPage.Strokes);
                App.CurrentPage.Scale = CanvasNext.CurrentScale;
                App.CurrentPage.OffsetX = CanvasNext.OffsetX;
                App.CurrentPage.OffsetY = CanvasNext.OffsetY;
                OnPageChanged(this, EventArgs.Empty);
            }
            else
            {
                await RunWithLoading("正在打开文档", ( ) => App.OpenDocument(fileName));
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
        var invalid = await RunWithLoading("正在打开", ( ) => App.LoadBoard(fileName));

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
        var current = App.CurrentPage;
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
            Board.Write(dialog.FileName, App.SnapshotPages(cloneStrokes: false));
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

    private void ExportImageClick(object o, RoutedEventArgs e)
    {
        if (App.IsBoardEmpty( ))
        {
            App.ShowInfo("没有可以导出的墨迹");
            return;
        }

        var scale = PickScale( );
        if (scale is not int percent)
        {
            return;
        }

        var fileDialog = new VistaFolderBrowserDialog( )
        {
            RootFolder = Environment.SpecialFolder.MyComputer,
            Multiselect = false,
            ShowNewFolderButton = true
        };
        if (fileDialog.ShowDialog( ) != true)
        {
            return;
        }

        var directory = Path.Join(fileDialog.SelectedPath, $"{DateTime.Now:yyyyMMddHHmmss}");
        var dpi = VisualTreeHelper.GetDpi(CanvasNext);
        var progress = ShowProgress( );

        ExportImageMenu.IsEnabled = false;
        CanvasNext.IsEnabled = false;

        Task.Run(( ) =>
        {
            try
            {
                App.ExportAllImage(percent, directory, dpi, progress);
            }
            catch (Exception ex)
            {
                App.LogException(ex);
                Dispatcher.Invoke(( ) => App.ShowException(ex, "导出失败。错误日志已记录。"));
                return;
            }
            finally
            {
                Dispatcher.Invoke(( ) =>
                {
                    ExportImageMenu.IsEnabled = true;
                    CanvasNext.IsEnabled = true;
                    HideLoading( );
                });
            }

            Dispatcher.Invoke(( ) => App.ShowInfo("导出图片成功"));
        });
    }

    private void ExportPdfClick(object o, RoutedEventArgs e)
    {
        if (App.IsBoardEmpty( ) && !App.HasBackground)
        {
            App.ShowInfo("没有可以导出的内容");
            return;
        }

        var scale = PickScale( );
        if (scale is not int percent)
        {
            return;
        }

        var dialog = new VistaSaveFileDialog( )
        {
            Filter = "PDF 文档 (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = $"{DateTime.Now:yyyyMMdd-HHmmss}",
        };
        if (dialog.ShowDialog( ) != true)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(CanvasNext);
        var progress = ShowProgress( );

        ExportPdfButton.IsEnabled = false;
        CanvasNext.IsEnabled = false;

        Task.Run(( ) =>
        {
            try
            {
                App.ExportAllPdf(dialog.FileName, dpi, percent, progress);
            }
            catch (Exception ex)
            {
                App.LogException(ex);
                Dispatcher.Invoke(( ) => App.ShowException(ex, "导出失败。错误日志已记录。"));
                return;
            }
            finally
            {
                Dispatcher.Invoke(( ) =>
                {
                    ExportPdfButton.IsEnabled = true;
                    CanvasNext.IsEnabled = true;
                    HideLoading( );
                });
            }

            Dispatcher.Invoke(( ) => App.ShowInfo("导出 PDF 成功"));
        });
    }

    private static int? PickScale( )
    {
        using TaskDialog scaleDialog = new( )
        {
            WindowTitle = "轻白板",
            MainInstruction = "请选择缩放比例",
            MainIcon = TaskDialogIcon.Information,
            ButtonStyle = TaskDialogButtonStyle.CommandLinks
        };

        var zoom25 = new TaskDialogButton("25%");
        var zoom50 = new TaskDialogButton("50%");
        var zoom100 = new TaskDialogButton("100%");
        var cancelButton = new TaskDialogButton(ButtonType.Cancel);
        scaleDialog.Buttons.Add(zoom25);
        scaleDialog.Buttons.Add(zoom50);
        scaleDialog.Buttons.Add(zoom100);
        scaleDialog.Buttons.Add(cancelButton);
        var result = scaleDialog.ShowDialog( );

        if (result == cancelButton)
        {
            return null;
        }
        else if (result == zoom25)
        {
            return 25;
        }
        else if (result == zoom50)
        {
            return 50;
        }
        else if (result == zoom100)
        {
            return 100;
        }

        return null;
    }
}
