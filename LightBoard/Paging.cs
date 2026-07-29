using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;

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

    private ImageSource preview = StrokeCollectionExtension.PreviewEmpty( );
    public ImageSource Preview
    {
        get => preview;
        set
        {
            preview = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Preview)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class MainWindow
{
    private void OnPageChanged(object? sender, EventArgs e)
    {
        LoadPageStateToView( );
        UpdatePageUI( );
        PagePreviewsBox.SelectedIndex = App.PageIndex;
    }

    private void LoadPageStateToView( )
    {
        var page = App.CurrentPage;
        CanvasNext.ResetTouchState( );
        CanvasNext.Strokes = page.Strokes;
        CanvasNext.CurrentScale = page.Scale;
        CanvasNext.OffsetX = page.OffsetX;
        CanvasNext.OffsetY = page.OffsetY;
        CanvasNext.SwapHistory(out _, page.History);
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

    private void SwitchPage(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= App.Pages.Count || pageIndex == App.PageIndex)
        {
            return;
        }

        SaveCurrentViewToPage( );
        App.SwitchPage(pageIndex);
    }

    private void PrevPage(object o, RoutedEventArgs e)
    {
        if (App.PageIndex > 0)
        {
            SwitchPage(App.PageIndex - 1);
        }
    }

    private void NewNextPage(object o, RoutedEventArgs e)
    {
        if (App.PageIndex < App.Pages.Count - 1)
        {
            SwitchPage(App.PageIndex + 1);
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
            SwitchPage(PagePreviewsBox.SelectedIndex);
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
        PageChanged?.Invoke(Current.MainWindow, EventArgs.Empty);
        return true;
    }

    public static void NewPage( )
    {
        Pages.Add(new Page { Number = Pages.Count + 1, Scale = 1.0, OffsetX = 8192, OffsetY = 8192 });
        PageIndex = Pages.Count - 1;
        PageChanged?.Invoke(Current.MainWindow, EventArgs.Empty);
    }

    public static void SaveStrokes(string fileName)
    {
        using var stream = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        CurrentPage.Strokes.Save(stream, false);
    }

    public static void OpenStrokes(string fileName)
    {
        using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
        CurrentPage.Strokes = new StrokeCollection(stream);
        CurrentPage.Scale = 1.0;
        CurrentPage.OffsetX = 8192;
        CurrentPage.OffsetY = 8192;
        CurrentPage.History = null;
        CurrentPage.Preview = CurrentPage.Strokes.Count > 0
            ? CurrentPage.Strokes.Preview( )
            : StrokeCollectionExtension.PreviewEmpty( );
        PageChanged?.Invoke(Current.MainWindow, EventArgs.Empty);
    }
}
