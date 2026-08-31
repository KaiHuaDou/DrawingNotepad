using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

using LightBoard.External;

using Microsoft.Win32;

using Ookii.Dialogs.Wpf;

namespace LightBoard;

public partial class MainWindow : Window
{
    private const string FileFilter =
        "可打开的文件|*.lbf;*.isf;*.pptx;*.ppt;*.docx;*.doc;*.xps;*.pdf|轻白板文件|*.lbf|Windows 墨迹文件|*.isf|演示文稿|*.pptx;*.ppt|Word 文档|*.docx;*.doc|XPS 文档|*.xps|PDF 文档|*.pdf|所有文件|*.*";

    private bool dirty;

    private readonly DispatcherTimer timeTimer;

    public MainWindow( )
    {
        InitializeComponent( );

        if (Resources["MajorGridBrush"] is DrawingBrush gridBrush)
        {
            gridBrush.Freeze( );
        }

        colorRadio ??= DefaultColorRadio;
        thicknessRadio ??= DefaultThicknessRadio;
        App.InitializePages( );
        App.PageChanged += OnPageChanged;

        if (App.PendingOpen is not null)
        {
            OpenFile(App.PendingOpen);
        }

        OnPageChanged(this, EventArgs.Empty);

        SyncToolState( );

        CanvasNext.ViewOrSelectionChanged += (_, _) => UpdateSelectionBorderPosition( );

        timeTimer = new(
            TimeSpan.FromSeconds(1),
            DispatcherPriority.Normal,
            (o, e) => TimeText.Text = $"{DateTime.Now:HH:mm}",
            Dispatcher.CurrentDispatcher
        );
        timeTimer.Start( );
    }

    private void WindowDeactivated(object o, EventArgs e)
    {
        CanvasNext.ResetTouchState( );
    }

    private void CloseWindowClick(object o, RoutedEventArgs e)
    {
        Close( );
    }

    private bool WhetherCloseFile( )
    {
        if (!dirty)
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
            BoardFile.Write(Path.Join(App.AppPath, "fastsave", $"{DateTime.Now:yyyyMMdd-HHmmss}.lbf"), App.Pages);
            dirty = false;
            return false;
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

    private void MinimizeWindowClick(object o, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

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
            else if (IsDocumentFile(fileName))
            {
                LoadingBar.IsIndeterminate = true;
                LoadingText.Text = "解析文档中...";
                LoadingBorder.Visibility = Visibility.Visible;

                try
                {
                    await App.OpenDocument(fileName);
                }
                catch (Exception ex)
                {
                    App.LogException(ex);
                    App.ShowDetailedInfo("无法打开文档", ex.Message, $"{ex.Message}\n{ex.StackTrace}");
                    return;
                }
                finally
                {
                    LoadingBorder.Visibility = Visibility.Hidden;
                }
            }
            else
            {
                App.CurrentPage.OpenStrokes(fileName);
                OnPageChanged(this, EventArgs.Empty);
            }

            dirty = false;
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            App.ShowException(ex, "错误日志已记录");
        }
    }

    private static bool IsBoardFile(string fileName)
    {
        return Path.GetExtension(fileName).Equals(BoardFile.Extension, StringComparison.OrdinalIgnoreCase);
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
            dirty = false;
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

        var scale = 100;
        if (result == cancelButton)
        {
            return;
        }
        else if (result == zoom25)
        {
            scale = 25;
        }
        else if (result == zoom50)
        {
            scale = 50;
        }
        else if (result == zoom100)
        {
            scale = 100;
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
        Directory.CreateDirectory(directory);

        var dpi = VisualTreeHelper.GetDpi(CanvasNext);

        ExportImageMenu.IsEnabled = false;
        Task.Run(( ) =>
        {
            try
            {
                App.ExportAllImage(scale, directory, dpi);
            }
            catch (Exception ex)
            {
                App.LogException(ex);
                App.ShowException(ex, "导出失败。错误日志已记录。");
                return;
            }
            finally
            {
                Dispatcher.Invoke(( ) => ExportImageMenu.IsEnabled = true);
            }

            App.ShowInfo("导出图片成功");
        });
    }

    private void SwitchOutClick(object o, RoutedEventArgs e)
    {
        NativeMethods.SwitchTo("msedge");
    }
}
