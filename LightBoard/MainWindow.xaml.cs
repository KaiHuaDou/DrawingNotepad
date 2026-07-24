using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
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

        if (!string.IsNullOrWhiteSpace(App.PendingOpen))
        {
            OpenStrokes(App.PendingOpen);
        }
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
        var stream = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
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

        StrokeCollection strokes = CanvasNext.Strokes.Clone( );
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
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

        Rect bounds = strokes.GetBounds( );
        bounds.Inflate(64, 64);

        var background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
        var visual = new DrawingVisual( );
        using (DrawingContext context = visual.RenderOpen( ))
        {
            context.DrawRectangle(background, null, new Rect(0, 0, bounds.Width, bounds.Height));
            foreach (Stroke stroke in strokes)
            {
                Stroke copy = stroke.Clone( );
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

    private void CanvasNextCanUndoChanged(object o, DependencyPropertyChangedEventArgs e)
    {
        UndoButton.IsEnabled = CanvasNext.CanUndo;
    }

    private void CanvasNextCanRedoChanged(object o, DependencyPropertyChangedEventArgs e)
    {
        RedoButton.IsEnabled = CanvasNext.CanRedo;
    }

    #endregion Editing
}
