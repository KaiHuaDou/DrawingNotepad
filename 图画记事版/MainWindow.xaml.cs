using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace 图画记事版;

/// <summary>
/// MainWindow.xaml 的交互逻辑
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow( )
    {
        InitializeComponent( );
        inkcScroll.ScrollToHorizontalOffset(2560);
        inkcScroll.ScrollToVerticalOffset(1920);
        //AllowsTransparency = true;
        //WindowStyle = WindowStyle.None;
    }

    private bool isTransparent = false;
    private void button_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog ofd = new( );
        ofd.ShowDialog( );
        Image image = new( );
        if (ofd.FileName != "")
        {
            try
            {
                image.Source = new BitmapImage(new Uri(ofd.FileName));
                if (image.Width > 900)
                    image.Width = 900;
                if (image.Height > 700)
                    image.Height = 700;
                image.HorizontalAlignment = HorizontalAlignment.Center;
                image.VerticalAlignment = VerticalAlignment.Center;
                inkc.Children.Add(image);
            }
            catch (NotSupportedException) { }
        }
    }

    private void button1_Click(object sender, RoutedEventArgs e)
    {
        Close( );
    }

    private void comboBox1_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (ColorComboBox.SelectedIndex != 7)
                ColorComboBox.Items.RemoveAt(7);
        }
        catch (Exception) { }
        switch (ColorComboBox.SelectedIndex)
        {
            case 0: inkc.DefaultDrawingAttributes.Color = Color.FromRgb(255, 0, 0); break;
            case 2: inkc.DefaultDrawingAttributes.Color = Color.FromRgb(0, 0, 255); break;
            case 3: inkc.DefaultDrawingAttributes.Color = Color.FromRgb(0, 255, 0); break;
            case 4: inkc.DefaultDrawingAttributes.Color = Color.FromRgb(0, 0, 0); break;
            case 5: inkc.DefaultDrawingAttributes.Color = Color.FromRgb(255, 255, 255); break;
            case 1: inkc.DefaultDrawingAttributes.Color = Color.FromRgb(255, 255, 0); break;
            case 6:
                try
                {
                    Color color = ColorBox( );
                    inkc.DefaultDrawingAttributes.Color = color;
                    ComboBoxItem cbi = new( )
                    {
                        Content = color.R + " " + color.G + " " + color.B,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Width = 137
                    };
                    ColorComboBox.Items.Add(cbi);
                    ColorComboBox.SelectedIndex = 7;
                }
                catch (Exception)
                {
                    ColorComboBox.SelectedIndex = 4;
                }
                break;
        }
    }

    private Color ColorBox( )
    {
        System.Windows.Forms.ColorDialog cd = new( );
        if (cd.ShowDialog( ) == System.Windows.Forms.DialogResult.OK)
        {
            Color color = new( )
            {
                R = cd.Color.R,
                G = cd.Color.G,
                B = cd.Color.B,
                A = 255
            };
            return color;
        }
        throw new FormatException( );
    }

    private void comboBox2_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        switch (InkShapeComboBox.SelectedIndex)
        {
            case 0: inkc.DefaultDrawingAttributes.StylusTip = StylusTip.Ellipse; break;
            case 1: inkc.DefaultDrawingAttributes.StylusTip = StylusTip.Rectangle; break;
        }
    }

    private void comboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        switch (EditingComboBox.SelectedIndex)
        {
            case 0: inkc.EditingMode = InkCanvasEditingMode.Ink; break;
            case 1: inkc.EditingMode = InkCanvasEditingMode.EraseByPoint; break;
            case 2: inkc.EditingMode = InkCanvasEditingMode.EraseByStroke; break;
            case 3: inkc.EditingMode = InkCanvasEditingMode.Select; break;
            case 4: inkc.EditingMode = InkCanvasEditingMode.None; break;
        }
    }

    private void HighLighter_Checked(object sender, RoutedEventArgs e)
    {
        inkc.DefaultDrawingAttributes.IsHighlighter = (bool) HighLighter.IsChecked;
        HighLighter.IsEnabled = false;
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog ofd = new( )
        {
            Filter = "绘画文件|*.draw|墨迹文件|*.ink|Windows墨迹文件|*.isf|所有文件|*.*"
        };
        ofd.ShowDialog( );
        try
        {
            FileStream fs = new(ofd.FileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            inkc.Strokes = new StrokeCollection(fs);
        }
        catch (Exception) { }
    }

    private void SaveFile_Click(object sender, RoutedEventArgs e)
    {
        SaveFileDialog sfd = new( )
        {
            Filter = "绘画文件|*.draw|墨迹文件|*.ink|Windows墨迹文件|*.isf|所有文件|*.*"
        };
        sfd.ShowDialog( );
        try
        {
            FileStream fs = new(sfd.FileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            inkc.Strokes.Save(fs, false);
        }
        catch (Exception) { }
        inkc.EraserShape = new EllipseStylusShape(20, 20);
        inkc.EraserShape = new RectangleStylusShape(20, 20);
    }

    private void CopyDraw_Click(object sender, RoutedEventArgs e)
    {
        if (inkc.GetSelectedStrokes( ).Count > 0)
            inkc.CopySelection( );
    }

    private void CutDraw_Click(object sender, RoutedEventArgs e)
    {
        if (inkc.GetSelectedStrokes( ).Count > 0)
            inkc.CutSelection( );
    }

    private void PasteDraw_Click(object sender, RoutedEventArgs e)
    {
        if (inkc.CanPaste( ))
        {
            inkc.Paste( );
        }
    }

    private void EraseShapeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        switch (InkShapeComboBox.SelectedIndex)
        {
            case 0: inkc.EraserShape = new EllipseStylusShape(inkc.EraserShape.Width, inkc.EraserShape.Height); break;
            case 1: inkc.EraserShape = new RectangleStylusShape(inkc.EraserShape.Width, inkc.EraserShape.Height); break;
        }
    }

    private void CacheModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        switch (CacheModeComboBox.SelectedIndex)
        {
            case 0: inkc.CacheMode = new BitmapCache( ); break;
            case 1: inkc.CacheMode = null; break;
        }
    }

    private void SelectAllDraw_Click(object sender, RoutedEventArgs e)
    {
        inkc.Select(inkc.Strokes);
        EditingComboBox.SelectedIndex = 3;
    }

    private void DeleteDraw_Click(object sender, RoutedEventArgs e)
    {
        inkc.Strokes.Remove(inkc.GetSelectedStrokes( ));
    }

    private void Transparent_Click(object sender, RoutedEventArgs e)
    {
        if (isTransparent)
        {
            Background.Opacity = 100;
            UpdateLayout( );
            isTransparent = false;
        }
        else
        {
            Color color = new( )
            {
                A = 204,
                R = 255,
                G = 255,
                B = 255
            };
            Background = new SolidColorBrush(color);
            WindowStyle = WindowStyle.None;
            isTransparent = true;
        }
    }
}
