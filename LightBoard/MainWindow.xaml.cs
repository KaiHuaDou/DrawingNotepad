using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Microsoft.Win32;

namespace LightBoard;

public partial class MainWindow : Window
{
    private const string FileFilter = "Windows 墨迹文件|*.isf|所有文件|*.*";
    private const string ImageFilter = "PNG 图像|*.png|所有文件|*.*";

    private void CloseWindow(object o, RoutedEventArgs e)
    {
        Close( );
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

        try
        {
            using FileStream fs = new(dialog.FileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            MainCanvas.Strokes.StrokesChanged -= OnStrokesChanged;
            MainCanvas.Strokes = new StrokeCollection(fs);
            MainCanvas.Strokes.StrokesChanged += OnStrokesChanged;
            undoStack.Clear( );
            redoStack.Clear( );
            UpdateUndoRedoButtons( );
        }
        catch { }
    }

    private void SaveFileClick(object o, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog( ) { Filter = FileFilter };
        if (dialog.ShowDialog( ) != true)
        {
            return;
        }

        try
        {
            using var stream = new FileStream(dialog.FileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            MainCanvas.Strokes.Save(stream, false);
        }
        catch { }
    }

    private void ExportImageClick(object o, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog( ) { Filter = ImageFilter };
        if (dialog.ShowDialog( ) != true)
        {
            return;
        }

        StrokeCollection strokes = MainCanvas.Strokes.Clone( );
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        var fileName = dialog.FileName;

        ExportImageButton.IsEnabled = false;
        Task.Run(( ) =>
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
        MainCanvas.DefaultDrawingAttributes.IsHighlighter = (bool) HighLighterToggle.IsChecked;
    }

    private void ColorRadioChecked(object o, RoutedEventArgs e)
    {
        if (o is not RadioButton { Background: SolidColorBrush brush })
        {
            return;
        }

        MainCanvas.EditingMode = InkCanvasEditingMode.Ink;
        MainCanvas.DefaultDrawingAttributes.Color = brush.Color;
    }

    private void ThicknessRadioClick(object o, RoutedEventArgs e)
    {
        if (o is not RadioButton { MinWidth: double thickness })
        {
            return;
        }

        MainCanvas.DefaultDrawingAttributes.Width = MainCanvas.DefaultDrawingAttributes.Height = thickness;
    }

    private void ToolRadioChecked(object o, RoutedEventArgs e)
    {
        if (o is not RadioButton { Tag: string tag })
        {
            return;
        }

        switch (tag)
        {
            case "\uED60": MainCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint; break;
            case "\uED61": MainCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke; break;
            case "\uEF20": MainCanvas.EditingMode = InkCanvasEditingMode.Select; break;
        }
    }

    private void EraseAll(object o, RoutedEventArgs e)
    {
        MainCanvas.Strokes.Clear( );
    }

    #endregion Editing

    #region UndoRedo

    private sealed class StrokeChange(StrokeCollection added, StrokeCollection removed)
    {
        public StrokeCollection Added { get; } = added;
        public StrokeCollection Removed { get; } = removed;
    }

    private readonly Stack<StrokeChange> undoStack = new( );
    private readonly Stack<StrokeChange> redoStack = new( );
    private bool isApplyingUndoRedo;

    private void OnStrokesChanged(object sender, StrokeCollectionChangedEventArgs e)
    {
        if (isApplyingUndoRedo)
        {
            return;
        }

        if (e.Added.Count == 0 && e.Removed.Count == 0)
        {
            return;
        }

        undoStack.Push(new StrokeChange(e.Added, e.Removed));
        redoStack.Clear( );
        UpdateUndoRedoButtons( );
    }

    private void UndoButtonClick(object o, RoutedEventArgs e)
    {
        if (undoStack.Count == 0)
        {
            return;
        }

        isApplyingUndoRedo = true;
        StrokeChange change = undoStack.Pop( );
        MainCanvas.Strokes.Remove(change.Added);
        MainCanvas.Strokes.Add(change.Removed);
        isApplyingUndoRedo = false;

        redoStack.Push(change);
        UpdateUndoRedoButtons( );
    }

    private void RedoButtonClick(object o, RoutedEventArgs e)
    {
        if (redoStack.Count == 0)
        {
            return;
        }

        isApplyingUndoRedo = true;
        StrokeChange change = redoStack.Pop( );
        MainCanvas.Strokes.Remove(change.Removed);
        MainCanvas.Strokes.Add(change.Added);
        isApplyingUndoRedo = false;

        undoStack.Push(change);
        UpdateUndoRedoButtons( );
    }

    private void UpdateUndoRedoButtons( )
    {
        UndoButton.IsEnabled = undoStack.Count > 0;
        RedoButton.IsEnabled = redoStack.Count > 0;
    }

    #endregion UndoRedo
}
