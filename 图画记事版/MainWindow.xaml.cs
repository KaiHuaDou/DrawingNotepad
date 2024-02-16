using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace 图画记事版;

public partial class MainWindow : Window
{
    public MainWindow( )
    {
        InitializeComponent( );
        canvasScroll.ScrollToHorizontalOffset(2560);
        canvasScroll.ScrollToVerticalOffset(1920);
        //AllowsTransparency = true;
        //WindowStyle = WindowStyle.None;
    }

    private bool isTransparent = false;

    private void OpenImage(object o, RoutedEventArgs e)
    {
        OpenFileDialog ofd = new( );
        ofd.ShowDialog( );
        if (string.IsNullOrWhiteSpace(ofd.FileName))
            return;
        try
        {
            Image image = new( )
            {
                Source = new BitmapImage(new Uri(ofd.FileName)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (image.Width > 900)
                image.Width = 900;
            if (image.Height > 700)
                image.Height = 700;
            canvas.Children.Add(image);
        }
        catch (NotSupportedException) { }
    }

    private void WindowClose(object o, RoutedEventArgs e) => Close( );

    private void ColorSelectionChange(object o, SelectionChangedEventArgs e)
    {
        if (canvas is null)
            return;
        try
        {
            if (ColorComboBox.SelectedIndex != 7)
                ColorComboBox.Items.RemoveAt(7);
        }
        catch { }
        switch (ColorComboBox.SelectedIndex)
        {
            case 0: canvas.DefaultDrawingAttributes.Color = Colors.Red; break;
            case 1: canvas.DefaultDrawingAttributes.Color = Colors.Yellow; break;
            case 2: canvas.DefaultDrawingAttributes.Color = Colors.Blue; break;
            case 3: canvas.DefaultDrawingAttributes.Color = Colors.Green; break;
            case 4: canvas.DefaultDrawingAttributes.Color = Colors.Black; break;
            case 5: canvas.DefaultDrawingAttributes.Color = Colors.White; break;
            case 6:
            {
                Color color = (Color) ColorBox( );
                ColorComboBox.SelectedIndex = color == null ? 4 : 7;
                if (color == null) break;

                canvas.DefaultDrawingAttributes.Color = color;
                ColorComboBox.Items.Add(new ComboBoxItem
                {
                    Content = color.R + " " + color.G + " " + color.B,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Width = 137
                });
                break;
            }
        }
    }

    private Color? ColorBox( )
    {
        System.Windows.Forms.ColorDialog box = new( );
        return box.ShowDialog( ) != System.Windows.Forms.DialogResult.OK
            ? null
            : Color.FromRgb(box.Color.R, box.Color.G, box.Color.B);
    }

    private void InkShapeSelectionChanged(object o, SelectionChangedEventArgs e)
    {
        if (canvas is null)
            return;
        switch (InkShapeComboBox.SelectedIndex)
        {
            case 0: canvas.DefaultDrawingAttributes.StylusTip = StylusTip.Ellipse; break;
            case 1: canvas.DefaultDrawingAttributes.StylusTip = StylusTip.Rectangle; break;
        }
    }

    private void EditingModeSelectionChanged(object o, SelectionChangedEventArgs e)
    {
        if (canvas is null)
            return;
        switch (EditingComboBox.SelectedIndex)
        {
            case 0: canvas.EditingMode = InkCanvasEditingMode.Ink; break;
            case 1: canvas.EditingMode = InkCanvasEditingMode.EraseByPoint; break;
            case 2: canvas.EditingMode = InkCanvasEditingMode.EraseByStroke; break;
            case 3: canvas.EditingMode = InkCanvasEditingMode.Select; break;
            case 4: canvas.EditingMode = InkCanvasEditingMode.None; break;
        }
    }

    private void HighLighterBoxClicked(object o, RoutedEventArgs e)
        => canvas.DefaultDrawingAttributes.IsHighlighter = (bool) HighLighterBox.IsChecked;

    private void OpenFileClick(object o, RoutedEventArgs e)
    {
        OpenFileDialog ofd = new( )
        {
            Filter = "绘画文件|*.draw|墨迹文件|*.ink|Windows 墨迹文件|*.isf|所有文件|*.*"
        };
        ofd.ShowDialog( );
        try
        {
            FileStream fs = new(ofd.FileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            canvas.Strokes = new StrokeCollection(fs);
        }
        catch { }
    }

    private void SaveFile_Click(object o, RoutedEventArgs e)
    {
        SaveFileDialog sfd = new( )
        {
            Filter = "绘画文件|*.draw|墨迹文件|*.ink|Windows墨迹文件|*.isf|所有文件|*.*"
        };
        sfd.ShowDialog( );
        try
        {
            FileStream fs = new(sfd.FileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            canvas.Strokes.Save(fs, false);
        }
        catch { }
        //inkc.EraserShape = new EllipseStylusShape(20, 20);
        //inkc.EraserShape = new RectangleStylusShape(20, 20);
    }

    private void CopyDrawClick(object o, RoutedEventArgs e)
    {
        if (canvas.GetSelectedStrokes( ).Count > 0)
            canvas.CopySelection( );
    }

    private void CutDrawClick(object o, RoutedEventArgs e)
    {
        if (canvas.GetSelectedStrokes( ).Count > 0)
            canvas.CutSelection( );
    }

    private void PasteDrawClick(object o, RoutedEventArgs e)
    {
        if (canvas.CanPaste( ))
            canvas.Paste( );
    }

    private void EraseShapeComboBox_SelectionChanged(object o, SelectionChangedEventArgs e)
    {
        if (canvas is null)
            return;
        switch (InkShapeComboBox.SelectedIndex)
        {
            case 0: canvas.EraserShape = new EllipseStylusShape(canvas.EraserShape.Width, canvas.EraserShape.Height); break;
            case 1: canvas.EraserShape = new RectangleStylusShape(canvas.EraserShape.Width, canvas.EraserShape.Height); break;
        }
    }

    private void CacheModeComboBox_SelectionChanged(object o, SelectionChangedEventArgs e)
    {
        if (canvas is null)
            return;
        switch (CacheModeComboBox.SelectedIndex)
        {
            case 0: canvas.CacheMode = new BitmapCache( ); break;
            case 1: canvas.CacheMode = null; break;
        }
    }

    private void SelectAllDrawClick(object o, RoutedEventArgs e)
    {
        canvas.Select(canvas.Strokes);
        EditingComboBox.SelectedIndex = 3;
    }

    private void DeleteDrawClick(object o, RoutedEventArgs e)
        => canvas.Strokes.Remove(canvas.GetSelectedStrokes( ));

    private void TransparentClick(object o, RoutedEventArgs e)
    {
        if (isTransparent)
        {
            Background.Opacity = 100;
            UpdateLayout( );
        }
        else
        {
            Background = new SolidColorBrush(Color.FromArgb(204, 255, 255, 255));
            WindowStyle = WindowStyle.None;
        }
        isTransparent = !isTransparent;
    }
}
