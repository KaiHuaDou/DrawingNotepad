using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;

using InkCanvasNext;

namespace LightBoard;

public record ExportProgress(string Tip, int Done, int Total);

public partial class Page : INotifyPropertyChanged
{
    public int Number
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Number)));
        }
    }
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
}

public partial class App
{
    private static int pageIndex = -1;

    public static event EventHandler? PageChanged;

    public static Page CurrentPage => Pages[pageIndex];

    public static int PageIndex
    {
        get => pageIndex;
        private set
        {
            pageIndex = value;
            PageChanged?.Invoke(Current.MainWindow, EventArgs.Empty);
        }
    }

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

    public static void DeletePage(int index)
    {
        if (Pages.Count <= 1 || index < 0 || index >= Pages.Count)
        {
            return;
        }

        var current = pageIndex;
        var deletingCurrent = index == current;

        if (deletingCurrent)
        {
            // 移除同步改写页面列表的选中项，下标先落到移除后仍有效的页
            pageIndex = Math.Min(index, Pages.Count - 2);
        }
        else if (index < current)
        {
            // 当前页对象未变，页序前移；重绑会用页上的旧快照盖掉撤销历史
            pageIndex--;
        }

        Pages.RemoveAt(index);
        RenumberPages( );
        BoardRevision++;

        if (deletingCurrent)
        {
            // 页对象已移除，赋值只为发出重绑
            PageIndex = pageIndex;
        }
    }

    private static void RenumberPages( )
    {
        for (var i = 0; i < Pages.Count; i++)
        {
            Pages[i].Number = i + 1;
        }
    }

    public static bool IsBoardEmpty( )
    {
        return !Pages.Any(p => p.Strokes.Count != 0 || p.Background is not null);
    }

    public static void ExportAllImage(int scale, string directory, DpiScale dpi, IProgress<ExportProgress>? progress = null)
    {
        const string tip = "正在导出图片";

        Directory.CreateDirectory(directory);

        // 页集合与每页内容都在 UI 线程取快照：导出在后台线程进行，期间仍可翻页与编辑
        var pages = Current.Dispatcher.Invoke(( ) => Pages.ToArray( ));

        var count = pages.Length;
        var pad = (int) (Math.Log10(count) + 1);
        progress?.Report(new(tip, 0, count));

        // 与 PDF 导出一致：空白页与只有背景的页同样导出，页码与页面列表一一对应
        for (var i = 0; i < count; i++)
        {
            var (Number, Strokes, Background, Box) = Current.Dispatcher.Invoke(( ) => SnapshotPage(pages[i]));
            var fileName = Path.Join(directory, $"{Number.ToString( ).PadLeft(pad, '0')}.png");

            Strokes.ExportPng(fileName, dpi, scale, background: Background, box: Box);
            progress?.Report(new(tip, i + 1, count));
        }
    }

    // 导出用页快照：笔画克隆与背景取图都要求 UI 线程，调用方负责在 UI 线程执行
    // 背景图要冻结后才能交给导出线程渲染
    internal static (int Number, StrokeCollection Strokes, ImageSource? Background, Rect? Box) SnapshotPage(Page page)
    {
        var background = page.Background;
        var image = background is null ? null : BackgroundImage(background);
        if (image?.CanFreeze == true)
        {
            image.Freeze( );
        }

        return (
            page.Number,
            new StrokeCollection([.. page.Strokes.Select(s => s.Clone( ))]),
            image,
            background?.Box);
    }
}
