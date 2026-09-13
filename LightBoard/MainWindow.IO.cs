using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

using Microsoft.Win32;

using Ookii.Dialogs.Wpf;

namespace LightBoard;

public partial class MainWindow
{
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

    private async void OpenFile(string fileName)
    {
        try
        {
            CloseDocumentViewer( );

            if (IsBoardFile(fileName))
            {
                App.LoadBoard(fileName);
            }
            else if (IsInkFile(fileName))
            {
                App.CurrentPage.OpenStrokes(fileName);
                OnPageChanged(this, EventArgs.Empty);
            }
            else
            {
                CanvasNext.IsEnabled = false;
                LoadingBorder.Visibility = Visibility.Visible;

                await App.OpenDocument(fileName)
                    .ContinueWith(_ => Dispatcher.Invoke(( ) =>
                    {
                        CanvasNext.IsEnabled = true;
                        LoadingBorder.Visibility = Visibility.Hidden;
                    }));

                await App.RefreshDocumentPreviewsAsync( );
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
            LoadingBorder.Visibility = Visibility.Hidden;
        }
    }

    private static bool IsBoardFile(string fileName)
    {
        return Path.GetExtension(fileName).Equals(BoardFile.Extension, StringComparison.OrdinalIgnoreCase);
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
            BoardFile.Write(dialog.FileName, App.Pages);
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

        var directory = Path.Join(fileDialog.SelectedPath, DateTime.Now.Ticks.ToString( ));
        var dpi = VisualTreeHelper.GetDpi(CanvasNext);

        ExportImageMenu.IsEnabled = false;
        CanvasNext.IsEnabled = false;

        Task.Run(( ) =>
        {
            try
            {
                App.ExportAllImage(percent, directory, dpi);
            }
            catch (Exception ex)
            {
                App.LogException(ex);
                App.ShowException(ex, "导出失败。错误日志已记录。");
                return;
            }
            finally
            {
                Dispatcher.Invoke(( ) =>
                {
                    ExportImageMenu.IsEnabled = true;
                    CanvasNext.IsEnabled = true;
                });
            }

            App.ShowInfo("导出图片成功");
        });
    }

    private void ExportPdfClick(object o, RoutedEventArgs e)
    {
        if (App.IsBoardEmpty( ) && App.Document is null)
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
        var loadingTip = LoadingText.Text;

        ExportPdfButton.IsEnabled = false;

        Task.Run(( ) =>
        {
            try
            {
                App.ExportAllPdf(dialog.FileName, dpi, percent);
            }
            catch (Exception ex)
            {
                App.LogException(ex);
                App.ShowException(ex, "导出失败。错误日志已记录。");
                return;
            }
            finally
            {
                Dispatcher.Invoke(( ) => ExportPdfButton.IsEnabled = true);
            }

            App.ShowInfo("导出 PDF 成功");
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
