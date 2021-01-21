using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace 图画记事版
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow( )
        {
            InitializeComponent( );
            inkc.DefaultDrawingAttributes.Height = 20;
            inkc.DefaultDrawingAttributes.Width = 20;
        }
        private void button_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog sfd = new System.Windows.Forms.OpenFileDialog( );
            sfd.ShowDialog( );
            if (sfd.FileName != "")
            {
                try
                {
                    image.Source = new BitmapImage(new Uri(sfd.FileName));
                    if (image.Width > 1000)
                        image.Width = 1000;
                    if (image.Height > 1000)
                        image.Height = 1000;
                }
                catch (NotSupportedException) { }
            }
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            this.Close( );
        }

        private void comboBox1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (ColorComboBox.SelectedIndex)
            {
                case 0: inkc.DefaultDrawingAttributes.Color = Color.FromRgb(255, 0, 0); break;
                case 2: inkc.DefaultDrawingAttributes.Color = Color.FromRgb(0, 0, 255); break;
                case 3: inkc.DefaultDrawingAttributes.Color = Color.FromRgb(0, 255, 0); break;
                case 4: inkc.DefaultDrawingAttributes.Color = Color.FromRgb(0, 0, 0); break;
                case 5: inkc.DefaultDrawingAttributes.Color = Color.FromRgb(255, 255, 255); break;
                case 1: inkc.DefaultDrawingAttributes.Color = Color.FromRgb(255, 255, 0); break;
            }
        }

        private void comboBox2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (InkShapeComboBox.SelectedIndex)
            {
                case 0: inkc.DefaultDrawingAttributes.StylusTip = System.Windows.Ink.StylusTip.Ellipse;break;
                case 1: inkc.DefaultDrawingAttributes.StylusTip = System.Windows.Ink.StylusTip.Rectangle;break;
            }
        }

        private void comboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (EditingComboBox.SelectedIndex)
            {
                case 0:
                inkc.EditingMode = InkCanvasEditingMode.Ink;
                break;
                case 1:
                inkc.EditingMode = InkCanvasEditingMode.EraseByPoint;
                break;
                case 2:
                inkc.EditingMode = InkCanvasEditingMode.EraseByStroke;
                break;
                case 3:
                inkc.EditingMode = InkCanvasEditingMode.Select;
                break;
                case 4:
                inkc.EditingMode = InkCanvasEditingMode.None;
                break;
            }
        }

        private void HighLighter_Checked(object sender, RoutedEventArgs e)
        {
            inkc.DefaultDrawingAttributes.IsHighlighter = (bool) HighLighter.IsChecked;
            HighLighter.IsEnabled = false;
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog( );
            ofd.Filter = "绘画文件|*.draw|墨迹文件|*.ink|所有文件|*.*";
            ofd.ShowDialog( );
            try
            {
                FileStream fs = new FileStream(ofd.FileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                inkc.Strokes = new StrokeCollection(fs);
            }
            catch (Exception) { } 
        }

        private void SaveFile_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog( );
            sfd.Filter = "绘画文件|*.draw|墨迹文件|*.ink|所有文件|*.*";
            sfd.ShowDialog( );
            try
            {
                FileStream fs = new FileStream(sfd.FileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                inkc.Strokes.Save(fs, false);
            }
            catch (Exception) { }
        }
    }
}
