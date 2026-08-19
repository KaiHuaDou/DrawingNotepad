using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using InkCanvasNext;

namespace LightBoard;

public class Page : INotifyPropertyChanged
{
    public int Number { get; set; }
    public StrokeCollection Strokes { get; set; } = [];
    public double Scale { get; set; } = 1.0;
    public double OffsetX { get; set; } = 8192;
    public double OffsetY { get; set; } = 8192;
    public HistorySnapshot? History { get; set; }

    public ImageSource Preview
    {
        get; set
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Preview)));
        }
    } = StrokeCollectionExtension.PreviewEmpty( );

    public event PropertyChangedEventHandler? PropertyChanged;

    public void OpenStrokes(string fileName)
    {
        using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
        Strokes = new StrokeCollection(stream);
        Scale = 1.0;
        OffsetX = 8192;
        OffsetY = 8192;
        History = null;
        Preview = Strokes.Count > 0 ? Strokes.Preview( ) : StrokeCollectionExtension.PreviewEmpty( );
    }

    public void ExportStokes(string fileName, DpiScale dpi, int scale)
    {
        var image = Strokes.Render(dpi, scale);
        var encoder = new PngBitmapEncoder( );
        encoder.Frames.Add(BitmapFrame.Create(image));

        using var stream = new FileStream(fileName, FileMode.Create);
        encoder.Save(stream);
    }
}

public partial class MainWindow
{
    private async void OnPageChanged(object? sender, EventArgs e)
    {
        var target = App.PageIndex;

        UpdatePageUI( );
        PagePreviewsBox.SelectedIndex = target;

        CanvasNext.ResetTouchState( );
        CanvasNext.Strokes = App.CurrentPage.Strokes;
        CanvasNext.CurrentScale = App.CurrentPage.Scale;
        CanvasNext.OffsetX = App.CurrentPage.OffsetX;
        CanvasNext.OffsetY = App.CurrentPage.OffsetY;
        CanvasNext.SwapHistory(out _, App.CurrentPage.History);

        if (App.Raster?.HasDocument != true)
        {
            CanvasNext.SetImage(null);
            return;
        }

        LoadingBar.IsIndeterminate = true;
        LoadingText.Text = "正在解析文档…";
        LoadingBorder.Visibility = Visibility.Visible;
        CanvasNext.IsEnabled = false;
        await LoadImageAsync(target);
    }

    private void SaveCurrentViewToPage( )
    {
        var page = App.CurrentPage;
        CanvasNext.SwapHistory(out var snapshot, null);
        page.History = snapshot;
        page.Scale = CanvasNext.CurrentScale;
        page.OffsetX = CanvasNext.OffsetX;
        page.OffsetY = CanvasNext.OffsetY;
        page.Preview = page.Strokes.Preview( );
    }

    private void PrevPage(object o, RoutedEventArgs e)
    {
        if (App.PageIndex > 0)
        {
            SaveCurrentViewToPage( );
            App.SwitchPage(App.PageIndex - 1);
        }
    }

    private void NewNextPage(object o, RoutedEventArgs e)
    {
        if (App.PageIndex < App.Pages.Count - 1)
        {
            SaveCurrentViewToPage( );
            App.SwitchPage(App.PageIndex + 1);
        }
        else
        {
            SaveCurrentViewToPage( );
            App.NewPage( );
        }
    }

    private void PagePreviewsBoxSelectionChanged(object o, SelectionChangedEventArgs e)
    {
        if (PagePreviewsBox.SelectedIndex >= 0 && PagePreviewsBox.SelectedIndex != App.PageIndex)
        {
            SaveCurrentViewToPage( );
            App.SwitchPage(PagePreviewsBox.SelectedIndex);
        }
    }

    private void UpdatePageUI( )
    {
        (AllPageToogle.Content as TextBlock)?.Text = $"{App.PageIndex + 1}/{App.Pages.Count}";
        NewNextPageButton.Tag = App.PageIndex < App.Pages.Count - 1 ? "\uE72A" : "\uE710";
        PrevPageButton.IsEnabled = App.PageIndex > 0;
    }
}

public partial class App
{
    public static void InitializePages( )
    {
        Pages.Add(new Page { Number = 1, Scale = 1.0, OffsetX = 8192, OffsetY = 8192 });
        PageIndex = 0;
    }

    public static bool SwitchPage(int newIndex)
    {
        if (newIndex < 0 || newIndex >= Pages.Count || newIndex == PageIndex)
        {
            return false;
        }

        PageIndex = newIndex;
        return true;
    }

    public static void NewPage( )
    {
        Pages.Add(new Page { Number = Pages.Count + 1, Scale = 1.0, OffsetX = 8192, OffsetY = 8192 });
        PageIndex = Pages.Count - 1;
    }

    public static void LoadBoard(string path)
    {
        var content = BoardFile.Read(path);

        Raster?.Dispose( );
        Raster = null;

        Pages.Clear( );
        for (var i = 0; i < content.Pages.Count; i++)
        {
            var p = content.Pages[i];
            Pages.Add(new Page
            {
                Number = i + 1,
                Strokes = p.Strokes,
                Scale = p.Scale,
                OffsetX = p.OffsetX,
                OffsetY = p.OffsetY,
                Preview = p.Strokes.Count > 0 ? p.Strokes.Preview( ) : StrokeCollectionExtension.PreviewEmpty( ),
            });
        }

        PageIndex = 0;
    }

    private static void SaveRecover( )
    {
        if (!Pages.Any(p => p.Strokes.Count > 0))
        {
            return;
        }

        try
        {
            BoardFile.Write(Path.Join(AppPath, "recover", $"{DateTime.Now:yyyyMMdd-HHmmss}.lbf"), Pages);
        }
        catch { }
    }

    public static void ExportAllImage(int scale, string directory, DpiScale dpi)
    {
        var exported = 0;
        foreach (var page in Pages)
        {
            if (page.Strokes.Count == 0)
            {
                continue;
            }

            var pad = (int) (Math.Log10(Pages.Count) + 1);
            var fileName = Path.Join(directory, $"{page.Number.ToString( ).PadLeft(pad, '0')}.png");
            page.ExportStokes(fileName, dpi, scale);
            exported++;
        }

        if (exported == 0)
        {
            ShowInfo("没有可以导出的墨迹");
        }
    }
}
