using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using InkCanvasNext;

using Microsoft.Win32;

using Ookii.Dialogs.Wpf;

namespace LightBoard;

public partial class MainWindow : Window
{
    private const string FileFilter = "Windows 墨迹文件|*.isf|所有文件|*.*";
    private const string ImageFilter = "PNG 图像|*.png|所有文件|*.*";

    private bool dirty;

    private readonly DispatcherTimer timeTimer;
    private readonly DispatcherTimer recoverTimer;

    public MainWindow( )
    {
        InitializeComponent( );
        InitializePages( );
        LoadPageState(CurrentPage);

        if (!string.IsNullOrWhiteSpace(App.PendingOpen))
        {
            OpenStrokes(App.PendingOpen);
        }

        UpdatePageUI( );

        timeTimer = new(
            TimeSpan.FromSeconds(1),
            DispatcherPriority.Normal,
            (o, e) => TimeText.Text = $"{DateTime.Now:HH:mm}",
            Dispatcher.CurrentDispatcher
        );
        timeTimer.Start( );

        recoverTimer = new(
            TimeSpan.FromMinutes(1),
            DispatcherPriority.Normal,
            (o, e) => SaveStrokes(Path.Join(App.AppPath, "recover", $"{DateTime.Now.Ticks}.isf")),
            Dispatcher.CurrentDispatcher
        );
        recoverTimer.Start( );
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
            MainInstruction = "轻白板 / LightBoard 26H3",
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

        var animationLeft = new DoubleAnimation
        {
            From = flag ? 0 : -LeftBorder.ActualWidth - 10,
            To = flag ? -LeftBorder.ActualWidth - 10 : 0,
            Duration = TimeSpan.FromSeconds(0.1),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        var animationCenter = new DoubleAnimation
        {
            From = flag ? 0 : CenterBorder.ActualHeight + 10,
            To = flag ? CenterBorder.ActualHeight + 10 : 0,
            Duration = TimeSpan.FromSeconds(0.1),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        var animationRight = new DoubleAnimation
        {
            From = flag ? 0 : RightBorder.ActualWidth + 10 - 70,
            To = flag ? RightBorder.ActualWidth + 10 - 70 : 0,
            Duration = TimeSpan.FromSeconds(0.1),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
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

        OpenStrokes(dialog.FileName);
    }

    private void OpenStrokes(string fileName)
    {
        try
        {
            using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read);

            CanvasNext.Strokes = new StrokeCollection(stream);
            CurrentPage.Strokes = CanvasNext.Strokes.Clone( );

            dirty = false;
            CanvasNext.CurrentScale = 1.0;
            CanvasNext.OffsetX = 8192;
            CanvasNext.OffsetY = 8192;
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
            SaveStrokes(dialog.FileName);
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

    public void SaveStrokes(string fileName)
    {
        using var stream = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        CanvasNext.Strokes.Save(stream, false);
    }

    private void ExportImageClick(object o, RoutedEventArgs e)
    {
        if (CanvasNext.Strokes.Count == 0)
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

        var fileDialog = new SaveFileDialog( )
        {
            Filter = ImageFilter,
            FileName = $"{DateTime.Now:yyyyMMdd-HHmmss}"
        };
        if (fileDialog.ShowDialog( ) != true)
        {
            return;
        }

        var fileName = fileDialog.FileName;
        var strokes = CanvasNext.Strokes;
        var dpi = VisualTreeHelper.GetDpi(CanvasNext);

        ExportImageMenu.IsEnabled = false;
        Task.Run([STAThread] ( ) =>
        {
            try
            {
                var image = strokes.Image(dpi, scale);
                var encoder = new PngBitmapEncoder( );
                encoder.Frames.Add(BitmapFrame.Create(image));

                using var stream = new FileStream(fileName, FileMode.Create);
                encoder.Save(stream);
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
}
