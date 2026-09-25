using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using InkCanvasNext;

namespace LightBoard;

public partial class Page : INotifyPropertyChanged
{
    public int Number { get; set; }
    public StrokeCollection Strokes { get; set; } = [];
    public double Scale { get; set; } = 1.0;
    public double OffsetX { get; set; } = App.InitialOffset.X;
    public double OffsetY { get; set; } = App.InitialOffset.Y;
    public HistorySnapshot? History { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void OpenStrokes(string fileName)
    {
        using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
        Strokes = [with(stream)];
        Scale = 1.0;
        OffsetX = App.InitialOffset.X;
        OffsetY = App.InitialOffset.Y;
        History = null;
        InvalidatePreview( );
        App.MarkBoardChanged( );
    }

    public void ExportStrokes(string fileName, DpiScale dpi, int scale, ImageSource? background = null)
    {
        var image = Strokes.Render(dpi, scale, canvasSize: App.CanvasSize, background: background);
        var encoder = new PngBitmapEncoder( );
        encoder.Frames.Add(BitmapFrame.Create(image));

        using var stream = new FileStream(fileName, FileMode.Create);
        encoder.Save(stream);
    }
}

public partial class MainWindow
{
    // 供测试宿主退订静态 PageChanged 事件，避免跨用例持有窗口引用
    internal void OnPageChanged(object? sender, EventArgs e)
    {
        var target = App.PageIndex;

        UpdatePageUI( );
        PagePreviewsBox.SelectedIndex = target;

        ExitStamp( );
        SelectionBorder.Visibility = Visibility.Collapsed;

        CanvasNext.ResetTouchState( );
        CanvasNext.Strokes = App.CurrentPage.Strokes;
        CanvasNext.CurrentScale = App.CurrentPage.Scale;
        CanvasNext.OffsetX = App.CurrentPage.OffsetX;
        CanvasNext.OffsetY = App.CurrentPage.OffsetY;
        CanvasNext.SwapHistory(out _, App.CurrentPage.History);

        CanvasNext.SetDocumentPage(App.Document?.GetPage(target));
    }

    private void SaveCurrentViewToPage( )
    {
        var page = App.CurrentPage;
        CanvasNext.SwapHistory(out var snapshot, null);
        page.History = snapshot;
        page.Scale = CanvasNext.CurrentScale;
        page.OffsetX = CanvasNext.OffsetX;
        page.OffsetY = CanvasNext.OffsetY;

        // 缩略图只在过期时重建（未改动的页翻过去再翻回来不再重算）
        page.RefreshPreview( );
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
        Pages.Add(new Page { Number = 1, Scale = 1.0, OffsetX = InitialOffset.X, OffsetY = InitialOffset.Y });
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

    /// <summary>
    /// 文档背景变化后把全部页的缩略图置脏：背景属于每一页，换页本身不改变背景。
    /// </summary>
    public static void InvalidatePagePreviews( )
    {
        foreach (var page in Pages)
        {
            page.InvalidatePreview( );
        }
    }

    public static void NewPage( )
    {
        Pages.Add(new Page { Number = Pages.Count + 1, Scale = 1.0, OffsetX = InitialOffset.X, OffsetY = InitialOffset.Y });
        PageIndex = Pages.Count - 1;
        MarkBoardChanged( );
    }

    public static void LoadBoard(string path)
    {
        var content = BoardFile.Read(path);

        Pages.Clear( );
        foreach (var p in content.Pages)
        {
            AddBoardPage(p);
        }

        PageIndex = 0;
    }

    public static void AttachBoard(string path)
    {
        var content = BoardFile.Read(path);
        foreach (var p in content.Pages)
        {
            AddBoardPage(p);
        }
    }

    private static void AddBoardPage(BoardPage p)
    {
        var page = new Page
        {
            Number = Pages.Count + 1,
            Strokes = p.Strokes,
            Scale = p.Scale,
            OffsetX = p.OffsetX,
            OffsetY = p.OffsetY,
        };

        if (p.Strokes.Count > 0)
        {
            page.InvalidatePreview( );
        }

        Pages.Add(page);
        MarkBoardChanged( );
    }

    /// <summary>
    /// 定时备份：内容未变化时跳过，写盘放到后台。
    /// </summary>
    private static void SaveRecoverInBackground( )
    {
        if (IsBoardEmpty( ) || RecoverRevision == BoardRevision)
        {
            return;
        }

        RecoverRevision = BoardRevision;
        var path = Path.Join(AppPath, "recover", $"{DateTime.Now:yyyyMMdd-HHmmss}.lbf");
        var snapshot = SnapshotPages(forBackgroundWrite: true);

        Task.Run(( ) => WriteRecover(path, snapshot));
    }

    /// <summary>
    /// 进程即将退出（未处理异常）时同步写完，不能等后台任务。
    /// </summary>
    private static void SaveRecover( )
    {
        if (IsBoardEmpty( ))
        {
            return;
        }

        WriteRecover(
            Path.Join(AppPath, "recover", $"{DateTime.Now:yyyyMMdd-HHmmss}.lbf"),
            SnapshotPages(forBackgroundWrite: false));
    }

    /// <summary>
    /// 生成可序列化的页面快照。<paramref name="forBackgroundWrite"/> 为真时复制墨迹：
    /// 后台写盘期间用户仍可继续编辑画布，必须用副本；UI 线程被阻塞的显式保存可直接引用活动墨迹。
    /// </summary>
    internal static BoardPage[] SnapshotPages(bool forBackgroundWrite)
    {
        return
        [
            .. Pages.Select(page => new BoardPage(
                forBackgroundWrite ? [.. page.Strokes.Select(s => s.Clone( ))] : page.Strokes,
                page.Scale,
                page.OffsetX,
                page.OffsetY)),
        ];
    }

    private static void WriteRecover(string path, IReadOnlyList<BoardPage> pages)
    {
        try
        {
            BoardFile.Write(path, pages);
        }
        catch (Exception e)
        {
            LogException(e);
        }
    }

    public static bool IsBoardEmpty( )
    {
        return Pages.All(p => p.Strokes.Count == 0);
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
                ImageSource? background = null;
                if (Document is not null)
                {
                    background = Current.Dispatcher.Invoke(( ) => Document.GetPage(page.Number - 1));
                }

                page.ExportStrokes(fileName, dpi, scale, background);
            }

            progress?.Report(new(tip, i + 1, count));
        }
    }
}

public record ExportProgress(string Tip, int Done, int Total);
