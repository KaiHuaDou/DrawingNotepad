using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    public Stack<StrokeChanges>? UndoStack { get; set; }
    public Stack<StrokeChanges>? RedoStack { get; set; }

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
    public ObservableCollection<Page> Pages { get; } = [];
    private int pageIndex;

    private Page CurrentPage => Pages[pageIndex];

    private void InitializePages( )
    {
        Pages.Add(new Page
        {
            Number = 1,
            Scale = 1.0,
            OffsetX = 8192,
            OffsetY = 8192,
            Preview = StrokeCollectionExtension.PreviewEmpty( )
        });
        pageIndex = 0;
    }

    private void SaveCurrentPageState( )
    {
        var page = CurrentPage;
        CanvasNext.SwapHistory(out var undo, out var redo, null, null);
        page.UndoStack = undo;
        page.RedoStack = redo;
        page.Scale = CanvasNext.CurrentScale;
        page.OffsetX = CanvasNext.OffsetX;
        page.OffsetY = CanvasNext.OffsetY;
        page.Preview = page.Strokes.Preview( );
    }

    private void LoadPageState(Page page)
    {
        CanvasNext.ResetTouchState( );

        CanvasNext.Strokes = page.Strokes;
        CanvasNext.CurrentScale = page.Scale;
        CanvasNext.OffsetX = page.OffsetX;
        CanvasNext.OffsetY = page.OffsetY;
        CanvasNext.SwapHistory(out _, out _, page.UndoStack, page.RedoStack);
    }

    private void SwitchPage(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= Pages.Count || pageIndex == this.pageIndex)
        {
            return;
        }

        SaveCurrentPageState( );

        this.pageIndex = pageIndex;
        LoadPageState(CurrentPage);
        UpdatePageUI( );

        PagePreviewsBox.SelectedIndex = this.pageIndex;
    }

    private void NewPage( )
    {
        SaveCurrentPageState( );

        Pages.Add(new Page
        {
            Number = Pages.Count + 1,
            Scale = 1.0,
            OffsetX = 8192,
            OffsetY = 8192,
            Preview = StrokeCollectionExtension.PreviewEmpty( )
        });

        pageIndex = Pages.Count - 1;
        LoadPageState(CurrentPage);
        UpdatePageUI( );

        PagePreviewsBox.SelectedIndex = pageIndex;
    }

    private void PrevPage(object o, RoutedEventArgs e)
    {
        if (pageIndex > 0)
        {
            SwitchPage(pageIndex - 1);
        }
    }

    private void NewNextPage(object o, RoutedEventArgs e)
    {
        if (pageIndex < Pages.Count - 1)
        {
            SwitchPage(pageIndex + 1);
        }
        else
        {
            NewPage( );
        }
    }

    private void PagePreviewsBoxSelectionChanged(object o, SelectionChangedEventArgs e)
    {
        if (PagePreviewsBox.SelectedIndex >= 0 && PagePreviewsBox.SelectedIndex != pageIndex)
        {
            SwitchPage(PagePreviewsBox.SelectedIndex);
        }
    }

    private void UpdatePageUI( )
    {
        (AllPageToogle.Content as TextBlock)?.Text = $"{pageIndex + 1}/{Pages.Count}";
        NewNextPageButton.Tag = pageIndex < Pages.Count - 1 ? "\uE72A" : "\uE710";
        PrevPageButton.IsEnabled = pageIndex > 0;
    }
}
