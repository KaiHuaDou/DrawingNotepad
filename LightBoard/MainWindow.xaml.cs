using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

using InkCanvasNext;

using Microsoft.Win32;

namespace LightBoard;

public partial class MainWindow : Window
{
    private const string FileFilter = "Windows 墨迹文件|*.isf|所有文件|*.*";
    private const string ImageFilter = "PNG 图像|*.png|所有文件|*.*";

    private bool dirty;

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
    }

    private void WindowDeactivated(object o, EventArgs e)
    {
        CanvasNext.ResetTouchState( );
    }

    private void CloseWindow(object o, RoutedEventArgs e)
    {
        if (!dirty)
        {
            Close( );
            return;
        }

        switch (MessageBox.Show(
            "已修改。是否保存？",
            "轻白板",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Asterisk
        ))
        {
            case MessageBoxResult.Yes: SaveFileClick(o, e); break;
            case MessageBoxResult.No: Close( ); break;
            default: return;
        }
    }

    private void MinimizeWindow(object o, RoutedEventArgs e)
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
    }

    private void AboutClick(object o, RoutedEventArgs e)
    {
        MessageBox.Show(
            "轻白板 / LightBoard 26H3\n源代码: https://github.com/KaiHuaDou/DrawingNotepad/\n发布版本: https://github.com/KaiHuaDou/DrawingNotepad/releases/",
            "轻白板",
            MessageBoxButton.OK,
            MessageBoxImage.Information
        );
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
            using var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);

            CanvasNext.Strokes = new StrokeCollection(fs);
            dirty = false;
        }
        catch { }
    }

    private void SaveFileClick(object o, RoutedEventArgs e)
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
        catch { }
    }

    public void SaveStrokes(string fileName)
    {
        using var stream = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        CanvasNext.Strokes.Save(stream, false);
    }

    private void ExportImageClick(object o, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog( )
        {
            Filter = ImageFilter,
            FileName = $"{DateTime.Now:yyyyMMdd-HHmmss}"
        };
        if (dialog.ShowDialog( ) != true)
        {
            return;
        }

        var strokes = CanvasNext.Strokes.Clone( );
        var dpi = VisualTreeHelper.GetDpi(this);
        var fileName = dialog.FileName;

        ExportImageButton.IsEnabled = false;
        Task.Run([STAThread] ( ) =>
        {
            try
            {
                ExportImage(strokes, fileName, dpi);
            }
            finally
            {
                Dispatcher.Invoke(( ) => ExportImageButton.IsEnabled = true);
            }
        });
    }

    private static void ExportImage(StrokeCollection strokes, string fileName, DpiScale dpi)
    {
        if (strokes.Count == 0)
        {
            return;
        }

        var bounds = strokes.GetBounds( );
        bounds.Inflate(64, 64);

        var background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
        var visual = new DrawingVisual( );
        using (var context = visual.RenderOpen( ))
        {
            context.DrawRectangle(background, null, new Rect(0, 0, bounds.Width, bounds.Height));
            foreach (var stroke in strokes)
            {
                var copy = stroke.Clone( );
                var matrix = new Matrix(1, 0, 0, 1, -bounds.X, -bounds.Y);
                copy.Transform(matrix, false);
                copy.Draw(context);
            }
        }

        var pixelWidth = Math.Max(1, (int) Math.Ceiling(bounds.Width * dpi.DpiScaleX));
        var pixelHeight = Math.Max(1, (int) Math.Ceiling(bounds.Height * dpi.DpiScaleY));

        var render = new RenderTargetBitmap(
            pixelWidth, pixelHeight,
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32
        );
        render.Render(visual);

        var encoder = new PngBitmapEncoder( );
        encoder.Frames.Add(BitmapFrame.Create(render));

        using var stream = new FileStream(fileName, FileMode.Create);
        encoder.Save(stream);
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
