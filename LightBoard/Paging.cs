using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    public StrokeCollection Strokes { get; } = [];
    public double Scale { get; set; } = 1.0;
    public double OffsetX { get; set; } = 8192;
    public double OffsetY { get; set; } = 8192;
    public Stack<StrokeChanges>? UndoStack { get; set; }
    public Stack<StrokeChanges>? RedoStack { get; set; }

    private ImageSource preview = new BitmapImage( );
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
            Preview = GeneratePreview([])
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

        page.Preview = GeneratePreview(page.Strokes);
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
            Preview = GeneratePreview([])
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

    private static RenderTargetBitmap GeneratePreview(StrokeCollection strokes)
    {
        const int PreviewWidth = 170;
        const int PreviewHeight = PreviewWidth / 16 * 9;

        var background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
        var visual = new DrawingVisual( );

        using (var context = visual.RenderOpen( ))
        {
            context.DrawRectangle(background, null, new Rect(0, 0, PreviewWidth, PreviewHeight));

            if (strokes.Count > 0)
            {
                var bounds = strokes.GetBounds( );
                if (!bounds.IsEmpty)
                {
                    var scaleX = PreviewWidth / bounds.Width;
                    var scaleY = PreviewHeight / bounds.Height;
                    var scale = Math.Min(scaleX, scaleY) * 0.8;

                    var centerX = bounds.Left + bounds.Width / 2;
                    var centerY = bounds.Top + bounds.Height / 2;
                    var matrix = new Matrix(scale, 0, 0, scale,
                        PreviewWidth / 2 - centerX * scale,
                        PreviewHeight / 2 - centerY * scale);

                    foreach (var stroke in strokes)
                    {
                        var copy = stroke.Clone( );
                        copy.Transform(matrix, false);
                        copy.Draw(context);
                    }
                }
            }
        }

        var render = new RenderTargetBitmap(PreviewWidth, PreviewHeight, 96, 96, PixelFormats.Pbgra32);
        render.Render(visual);
        return render;
    }
}
