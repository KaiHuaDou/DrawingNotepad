using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using InkCanvasNext;

namespace LightBoard;

public record ExportProgress(string Tip, int Done, int Total);

public partial class Page : INotifyPropertyChanged
{
    public int Number { get; set; }
    public StrokeCollection Strokes { get; set; } = [];
    public double Scale { get; set; } = 1.0;
    public double OffsetX { get; set; } = App.InitialOffset.X;
    public double OffsetY { get; set; } = App.InitialOffset.Y;
    public HistorySnapshot? History { get; set; }
    internal DocumentBackground? Background { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    // 页视图不在此重置，由调用方与画布视图同步
    public void OpenStrokes(string fileName)
    {
        using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
        Strokes = [with(stream)];
        History = null;
        InvalidatePreview( );
        App.BoardRevision++;
    }

    public void ExportStrokes(string fileName, DpiScale dpi, int scale, ImageSource? background, Rect? box)
    {
        var image = Strokes.Render(dpi, scale, background: background, box: box);
        var encoder = new PngBitmapEncoder( );
        encoder.Frames.Add(BitmapFrame.Create(image));

        using var stream = new FileStream(fileName, FileMode.Create);
        encoder.Save(stream);
    }
}

public partial class App
{
    public static event EventHandler? PageChanged;

    public static Page CurrentPage => Pages[PageIndex];

    public static int PageIndex
    {

        get;
        private set
        {
            field = value;
            PageChanged?.Invoke(Current.MainWindow, EventArgs.Empty);
        }
    } = -1;

    public static ObservableCollection<Page> Pages { get; } = [];

    public static void InitializePages( )
    {
        Pages.Add(new Page { Number = 1, Scale = 1.0, OffsetX = InitialOffset.X, OffsetY = InitialOffset.Y });
        PageIndex = 0;
    }

    public static void SwitchPage(int index)
    {
        // 同索引不重绑：SwapHistory 会用页上的旧快照覆盖当前撤销历史
        if (index < 0 || index >= Pages.Count || index == PageIndex)
        {
            return;
        }

        (Current.MainWindow as MainWindow)?.SaveCurrentViewToPage( );
        PageIndex = index;
    }

    public static void NewPage( )
    {
        Pages.Add(new Page { Number = Pages.Count + 1, Scale = 1.0, OffsetX = InitialOffset.X, OffsetY = InitialOffset.Y });
        PageIndex = Pages.Count - 1;
        BoardRevision++;
    }

    public static bool IsBoardEmpty( )
    {
        return !Pages.Any(p => p.Strokes.Count != 0);
    }

    public static void ExportAllImage(int scale, string directory, DpiScale dpi, IProgress<ExportProgress>? progress = null)
    {
        const string tip = "正在导出图片";

        Directory.CreateDirectory(directory);

        var count = Pages.Count;
        progress?.Report(new(tip, 0, count));
        for (var i = 0; i < count; i++)
        {
            var page = Pages[i];
            if (page.Strokes.Count > 0)
            {
                var pad = (int) (Math.Log10(count) + 1);
                var fileName = Path.Join(directory, $"{page.Number.ToString( ).PadLeft(pad, '0')}.png");

                // XPS 页树具有线程亲和性，背景统一派发到 UI 线程渲染（结果已冻结），再回后台线程合成。
                var source = page.Background;
                ImageSource? background = null;
                if (source is not null)
                {
                    background = Current.Dispatcher.Invoke(( ) => BackgroundImage(source));
                }

                page.ExportStrokes(fileName, dpi, scale, background, source?.Box);
            }

            progress?.Report(new(tip, i + 1, count));
        }
    }
}
