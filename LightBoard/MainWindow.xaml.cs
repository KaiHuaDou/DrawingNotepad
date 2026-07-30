using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

using InkCanvasNext;

using Microsoft.Win32;

using Ookii.Dialogs.Wpf;

namespace LightBoard;

public partial class MainWindow : Window
{
    private const string FileFilter =
        "可打开的文件|*.isf;*.pptx;*.ppt;*.docx;*.doc|Windows 墨迹文件|*.isf|演示文稿|*.pptx;*.ppt|Word 文档|*.docx;*.doc|所有文件|*.*";

    private bool dirty;

    private readonly DispatcherTimer timeTimer;

    public MainWindow( )
    {
        InitializeComponent( );
        App.InitializePages( );
        App.PageChanged += OnPageChanged;

        if (App.PendingOpen is not null)
        {
            OpenFile(App.PendingOpen);
        }

        OnPageChanged(this, EventArgs.Empty);

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

    private bool CloseWindow( )
    {
        if (!dirty)
        {
            return false;
        }

        using TaskDialog dialog = new( )
        {
            WindowTitle = "轻白板",
            MainInstruction = "有未保存的墨迹，是否保存？",
            MainIcon = TaskDialogIcon.Warning,
            ButtonStyle = TaskDialogButtonStyle.CommandLinks
        };

        var saveButton = new TaskDialogButton("保存");
        var discardButton = new TaskDialogButton("放弃");
        var cancelButton = new TaskDialogButton(ButtonType.Cancel);
        dialog.Buttons.Add(saveButton);
        dialog.Buttons.Add(discardButton);
        dialog.Buttons.Add(cancelButton);
        var result = dialog.ShowDialog( );

        if (result == saveButton)
        {
            SaveFile( );
            return false;
        }
        else if (result == discardButton)
        {
            return false;
        }

        return true;
    }

    private void WindowClosing(object o, CancelEventArgs e)
    {
        e.Cancel = CloseWindow( );
    }

    private void MinimizeWindowClick(object o, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void AllPageToogleClick(object o, RoutedEventArgs e)
    {
        if (o is not ToggleButton { IsChecked: bool isChecked })
        {
            return;
        }

        var heightAnimation = new DoubleAnimation
        {
            From = RightBorder.ActualHeight,
            To = isChecked ? ActualHeight - 20 : 66,
            Duration = TimeSpan.FromSeconds(0.1),
            EasingFunction = new CubicEase( ) { EasingMode = EasingMode.EaseInOut }
        };

        RightBorder.BeginAnimation(Border.HeightProperty, heightAnimation);
        TimeText.Visibility = isChecked ? Visibility.Collapsed : Visibility.Visible;
    }

    private void AboutClick(object o, RoutedEventArgs e)
    {
        using TaskDialog dialog = new( )
        {
            WindowTitle = "轻白板",
            MainInstruction = "轻白板 / LightBoard 26H4 Beta",
            MainIcon = TaskDialogIcon.Information,
            Content =
            """
            源代码: <a href="https://github.com/KaiHuaDou/DrawingNotepad/">https://github.com/KaiHuaDou/DrawingNotepad/</a>        
            发布版本: <a href="https://github.com/KaiHuaDou/DrawingNotepad/releases/">https://github.com/KaiHuaDou/DrawingNotepad/releases/</a>
            """,
            EnableHyperlinks = true,
        };
        dialog.HyperlinkClicked += (o, e) => Process.Start(new ProcessStartInfo(e.Href) { UseShellExecute = true });
        dialog.Buttons.Add(new TaskDialogButton(ButtonType.Ok));
        dialog.ShowDialog( );
    }

    private void CollapseExpandClick(object o, RoutedEventArgs e)
    {
        var flag = CollapseExpandButton.IsChecked == true;
        CollapseExpandIcon.Text = flag ? "\uE70E" : "\uE70D";

        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var animationLeft = new DoubleAnimation
        {
            From = flag ? 0 : -LeftBorder.ActualWidth,
            To = flag ? -LeftBorder.ActualWidth : 0,
            Duration = TimeSpan.FromSeconds(0.1),
            EasingFunction = ease
        };
        var animationCenter = new DoubleAnimation
        {
            From = flag ? 0 : CenterBorder.ActualHeight,
            To = flag ? CenterBorder.ActualHeight : 0,
            Duration = TimeSpan.FromSeconds(0.1),
            EasingFunction = ease
        };
        var animationRight = new DoubleAnimation
        {
            From = flag ? 0 : RightBorder.ActualWidth - 60,
            To = flag ? RightBorder.ActualWidth - 60 : 0,
            Duration = TimeSpan.FromSeconds(0.1),
            EasingFunction = ease
        };

        LeftTransform.BeginAnimation(TranslateTransform.XProperty, animationLeft);
        CenterTransform.BeginAnimation(TranslateTransform.YProperty, animationCenter);
        RightTransform.BeginAnimation(TranslateTransform.XProperty, animationRight);
    }

    #region IO

    private void OpenFileClick(object o, RoutedEventArgs e)
    {
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
            if (IsDocumentFile(fileName))
            {
                LoadingBorder.Visibility = Visibility.Visible;
                CanvasNext.IsEnabled = false;
                await App.OpenDocument(fileName);

                if (App.Raster?.Session is null)
                {
                    LoadingBorder.Visibility = Visibility.Hidden;
                    CanvasNext.IsEnabled = true;
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

    private void SaveFileClick(object o, RoutedEventArgs e)
    {
        SaveFile( );
    }

    private void SaveFile( )
    {
        var dialog = new SaveFileDialog( )
        {
            Filter = FileFilter,
            FileName = $"{DateTime.Now:yyyyMMdd-HHmmss}"
        };

        if (dialog.ShowDialog( ) != true)
        {
            return;
        }

        try
        {
            App.CurrentPage.SaveStrokes(dialog.FileName);
            dirty = false;
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            App.ShowException(ex, "错误日志已记录");
            return;
        }

        App.ShowInfo("墨迹已保存");
    }

    private void ExportImageClick(object o, RoutedEventArgs e)
    {
        if (App.CurrentPage.Strokes.Count == 0)
        {
            App.ShowInfo("没有可以导出的墨迹");
            return;
        }

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
                foreach (var page in App.Pages)
                {
                    var pad = (int) (Math.Log10(App.Pages.Count) + 1);
                    var fileName = Path.Join(directory, $"{page.Number.ToString( ).PadLeft(pad, '0')}.png");
                    page.ExportStokes(fileName, dpi, scale);
                }
            }
            catch (Exception ex)
            {
                App.LogException(ex);
                App.ShowException(ex, "错误日志已记录");
                return;
            }
            finally
            {
                Dispatcher.Invoke(( ) => ExportImageMenu.IsEnabled = true);
            }

            App.ShowInfo("导出图片成功");
        });
    }

    #endregion IO

    #region Editing

    private void HighLighterBoxClicked(object o, RoutedEventArgs e)
    {
        CanvasNext.DefaultDrawingAttributes.IsHighlighter = HighLighterToggle.IsChecked ?? false;
    }

    private void ColorRadioChecked(object o, RoutedEventArgs e)
    {
        if (o is not RadioButton { Background: SolidColorBrush brush })
        {
            return;
        }

        CanvasNext.Mode = InkCanvasNextMode.Ink;
        CanvasNext.DefaultDrawingAttributes.Color = brush.Color;
    }

    private void ThicknessRadioClick(object o, RoutedEventArgs e)
    {
        if (o is not RadioButton { MinWidth: double thickness })
        {
            return;
        }

        CanvasNext.DefaultDrawingAttributes.Width = CanvasNext.DefaultDrawingAttributes.Height = thickness;
    }

    private void ToolRadioChecked(object o, RoutedEventArgs e)
    {
        if (o is not RadioButton { Tag: string tag })
        {
            return;
        }

        CanvasNext.Mode = tag switch
        {
            "\uED60" => InkCanvasNextMode.EraseArea,
            "\uED61" => InkCanvasNextMode.EraseStroke,
            "\uEF20" => InkCanvasNextMode.Select,
            _ => CanvasNext.Mode,
        };
    }

    private void EraseAll(object o, RoutedEventArgs e)
    {
        CanvasNext.ClearMultiTouchVisuals( );
        CanvasNext.Strokes.Clear( );
    }

    private void UndoButtonClick(object o, RoutedEventArgs e)
    {
        CanvasNext.Undo( );
    }

    private void RedoButtonClick(object o, RoutedEventArgs e)
    {
        CanvasNext.Redo( );
    }

    private void CanvasNextStrokesChanged(object o, EventArgs e)
    {
        dirty = true;
    }

#pragma warning disable IDE0060

    private void CanvasNextCanUndoChanged(object o, DependencyPropertyChangedEventArgs e)
    {
        UndoButton.IsEnabled = CanvasNext.CanUndo;
    }

    private void CanvasNextCanRedoChanged(object o, DependencyPropertyChangedEventArgs e)
    {
        RedoButton.IsEnabled = CanvasNext.CanRedo;
    }

#pragma warning restore IDE0060

    private void CopyClick(object o, RoutedEventArgs e)
    {
        CanvasNext.CopySelected( );
    }

    private void PasteClick(object o, RoutedEventArgs e)
    {
        CanvasNext.Paste( );
    }

    private void CutClick(object o, RoutedEventArgs e)
    {
        CanvasNext.CutSelected( );
    }

    private void DeleteClick(object o, RoutedEventArgs e)
    {
        CanvasNext.DeleteSelected( );
    }

    private void CloneClick(object o, RoutedEventArgs e)
    {
        CanvasNext.CloneSelected( );
    }

    #endregion Editing

    private void TransparentModeClick(object o, RoutedEventArgs e)
    {
        var mode = TransparentModeButton.IsChecked == true;
        var blackBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
        var borderBrush = new SolidColorBrush(Color.FromArgb(128, 0x2E, 0x2E, 0x2E));

        CanvasNext.Background = mode ? Brushes.Transparent : blackBrush;
        TimeText.Visibility = mode || AllPageToogle.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
        LeftBorder.Background = mode ? blackBrush : borderBrush;
        CenterBorder.Background = mode ? blackBrush : borderBrush;
        RightBorder.Background = mode ? blackBrush : borderBrush;
        TransparentModeText.Text = mode ? "\uE7C3" : "\uE729";
    }
}
