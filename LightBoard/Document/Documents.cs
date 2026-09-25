using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

using InkCanvasNext;

using NetOffice.OfficeApi.Enums;
using NetOffice.PowerPointApi.Enums;
using NetOffice.WordApi.Enums;

using PpApplication = NetOffice.PowerPointApi.Application;
using WdApplication = NetOffice.WordApi.Application;

namespace LightBoard;

public partial class App
{
    public static DocumentService? Document { get; internal set; }

    // 文档背景页在内容坐标中的 Uniform 适配盒子（打开/附加文档时画布的可见内容区域）；显示、缩略图与导出共用
    internal static Rect? DocumentBox { get; set; }

    public static async Task OpenDocument(string path)
    {
        var document = await DocumentService.OpenAsync(path);

        Pages.Clear( );

        var (scale, offsetX, offsetY, box) = CanvasView( );

        for (var i = 0; i < document.PageCount; i++)
        {
            AddDocumentPage(scale, offsetX, offsetY);
        }

        Document = document;
        DocumentBox = box;
        PageIndex = 0;
    }

    private static void AddDocumentPage(double scale, double offsetX, double offsetY)
    {
        var page = new Page
        {
            Number = Pages.Count + 1,
            Scale = scale,
            OffsetX = offsetX,
            OffsetY = offsetY,
        };

        // 文档页即使没有墨迹也有背景缩略图
        page.InvalidatePreview( );
        Pages.Add(page);
    }

    public static async Task<DocumentService> AttachDocument(string path)
    {
        var document = await DocumentService.OpenAsync(path);

        var (scale, offsetX, offsetY, box) = CanvasView( );

        while (Pages.Count < document.PageCount)
        {
            AddDocumentPage(scale, offsetX, offsetY);
        }

        // 当前页视图同步为捕获视图（即画布当前视图），换页往返后文档盒仍落在视口内
        var current = CurrentPage;
        current.Scale = scale;
        current.OffsetX = offsetX;
        current.OffsetY = offsetY;

        Document = document;
        DocumentBox = box;
        return document;
    }

    // 捕获画布当前视图：文档盒取可见内容区域，背景随之落在视口内、画布无需移动；各文档页视图取捕获视图，
    // 换页后外观一致。极小缩放下可见区域大于画布，盒子夹回画布内。窗口尚未完成布局（如带参数启动即打开文档）
    // 时视口尺寸未知，按 1x 整块主屏估算（无边框最大化窗口的视口即整块主屏）。
    private static (double Scale, double OffsetX, double OffsetY, Rect Box) CanvasView( )
    {
        var canvas = (Current.MainWindow as MainWindow)?.CanvasNext;

        if (canvas is null || canvas.ViewportSize is not { Width: > 0, Height: > 0 } viewport)
        {
            var width = SystemParameters.PrimaryScreenWidth;
            var height = SystemParameters.PrimaryScreenHeight;
            return (1.0, InitialOffset.X, InitialOffset.Y, new Rect(InitialOffset.X, InitialOffset.Y, width, height));
        }

        var scale = canvas.CurrentScale;
        var offsetX = canvas.OffsetX;
        var offsetY = canvas.OffsetY;

        var box = new Rect(
            offsetX / scale,
            offsetY / scale,
            viewport.Width / scale,
            viewport.Height / scale);
        box.Intersect(new Rect(0, 0, CanvasSize.Width, CanvasSize.Height));

        return (scale, offsetX, offsetY, box);
    }
}

// 文档页面渲染源：XPS 走矢量渲染、PDF 走 PDFium 位图渲染，在 DocumentService 处汇合。
internal abstract class PageSource : IDisposable
{
    public abstract int PageCount { get; }

    public abstract ImageSource? GetPage(int index);

    public abstract void Dispose( );
}

public sealed class DocumentService : IDisposable
{
    private const long CacheQuotaBytes = 512L * 1024 * 1024;

    private const long TrimWatermarkBytes = 400L * 1024 * 1024;

    private const int PageCacheCapacity = 10;

    private static readonly string[] ImageExtensions = [".bmp", ".gif", ".ico", ".jpg", ".jpeg", ".png", ".tiff"];

    private static readonly string CacheDir = Path.Join(App.AppPath, "XpsCache");
    private static readonly ConcurrentDictionary<string, Lazy<Task<(string Path, string? Temp)>>> Converting = new( );
    private readonly string? tempPath;
    private readonly LruCache pageCache = new(PageCacheCapacity);

    private DocumentService(PageSource source, string? tempPath)
    {
        Source = source;
        this.tempPath = tempPath;
    }

    internal PageSource Source { get; }

    public int PageCount => Source.PageCount;

    public static async Task<DocumentService> OpenAsync(string path)
    {
        var ext = Path.GetExtension(path);

        if (ImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
        {
            return new DocumentService(ImagePageSource.Open(path), null);
        }

        // XPS 即内部渲染格式，直接打开源文件；PDF 无需转换，走独立渲染源。
        if (ext.Equals(".xps", StringComparison.OrdinalIgnoreCase))
        {
            return new DocumentService(new XpsSource(path), null);
        }

        if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return new DocumentService(PdfSource.Open(path), null);
        }

        var cachePath = await Task.Run(async ( ) =>
        {
            TrimCache( );
            return await GetCachePath(path);
        });

        // 命中缓存直接打开；缓存损坏直接删除（删除失败忽略），落到下方重新转换。
        if (File.Exists(cachePath))
        {
            try
            {
                try { File.SetLastAccessTime(cachePath, DateTime.Now); } catch { }

                return new DocumentService(new XpsSource(cachePath), null);
            }
            catch
            {
                try { File.Delete(cachePath); } catch { }
            }
        }

        // 同一源文件并发打开时只转换一次。
        var lazy = Converting.GetOrAdd(cachePath, key => new Lazy<Task<(string Path, string? Temp)>>(
            ( ) => ConvertAndCacheAsync(key, path), LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            var (ready, temp) = await lazy.Value;
            return new DocumentService(new XpsSource(ready), temp);
        }
        finally
        {
            Converting.TryRemove(new KeyValuePair<string, Lazy<Task<(string Path, string? Temp)>>>(cachePath, lazy));
        }
    }

    public ImageSource? GetPage(int index)
    {
        if (pageCache.Take(index) is ImageSource cached)
        {
            return cached;
        }

        var image = Source.GetPage(index);
        if (image is null)
        {
            return null;
        }

        pageCache.Put(index, image);
        return image;
    }

    public void Dispose( )
    {
        // 缓存文件留给下次打开复用，这里只释放 XPS 句柄；临时产物（缓存写入失败时）删除，失败忽略。
        Source.Dispose( );
        if (tempPath != null)
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    private static async Task<(string Path, string? Temp)> ConvertAndCacheAsync(string cachePath, string sourcePath)
    {
        var staging = Path.Combine(Path.GetTempPath( ), $"lightboard-{Guid.NewGuid( ):N}.xps");
        try
        {
            var staged = await RunOnStaAsync(( ) => ConvertToXps(sourcePath, staging));
            // 先写临时文件再原子改名进缓存；任何失败直接忽略，退化为临时文件（Dispose 时清理）。
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
                File.Move(staged, cachePath);
                return (cachePath, null);
            }
            catch (IOException) when (File.Exists(cachePath))
            {
                // 并发下已有结果，直接复用缓存。
                try { File.Delete(staged); } catch { }

                return (cachePath, null);
            }
            catch
            {
                return (staged, staged);
            }
        }
        catch
        {
            try { File.Delete(staging); } catch { }

            throw;
        }
    }

    private static void ConvertDocx(string path, string outputPath)
    {
        // NetOffice 自动管理 COM 代理释放，无需 Marshal.ReleaseComObject，也不依赖 PIA / 类型库。
        using var app = new WdApplication( );
        app.Visible = false;
        app.DisplayAlerts = WdAlertLevel.wdAlertsNone;
        var doc = app.Documents.Open(path, Type.Missing, true, false);
        try
        {
            doc.ExportAsFixedFormat(
                outputFileName: outputPath,
                exportFormat: WdExportFormat.wdExportFormatXPS);
        }
        finally
        {
            doc.Close(WdSaveOptions.wdDoNotSaveChanges);
            app.Quit( );
        }
    }

    private static void ConvertPptx(string path, string outputPath)
    {
        using var app = new PpApplication( );
        // PowerPoint 2016+ 禁止把 Application 设为隐藏（app.Visible=msoFalse 会抛
        // "Hiding the application window is not allowed"），故只能设为可见，转换期间 PowerPoint 窗口会短暂出现。
        app.Visible = MsoTriState.msoTrue;
        var presentation = app.Presentations.Open(
            path,
            MsoTriState.msoTrue,
            MsoTriState.msoFalse,
            MsoTriState.msoFalse);
        try
        {
            presentation.SaveAs(outputPath, PpSaveAsFileType.ppSaveAsXPS);
        }
        finally
        {
            presentation.Close( );
            app.Quit( );
        }
    }

    private static string ConvertToXps(string sourcePath, string outputPath)
    {
        if (Path.GetExtension(sourcePath).ToUpperInvariant( ) is ".PPTX" or ".PPT")
        {
            ConvertPptx(sourcePath, outputPath);
        }
        else
        {
            // 未知扩展名（如 .wps）按 Word 尝试打开。
            ConvertDocx(sourcePath, outputPath);
        }

        return outputPath;
    }

    private static async Task<string> GetCachePath(string path)
    {
        await using var stream = File.OpenRead(path);
        using var hash = SHA256.Create( );
        return Path.Join(CacheDir, Convert.ToHexString(await hash.ComputeHashAsync(stream)) + ".xps");
    }

    private static Task<T> RunOnStaAsync<T>(Func<T> action)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(( ) =>
        {
            try
            {
                tcs.SetResult(action( ));
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        })
        { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start( );
        return tcs.Task;
    }

    // 超出配额按最近使用时间从旧到新清理；正在被打开的缓存文件删除会失败，跳过即可。
    private static void TrimCache( )
    {
        try
        {
            var dir = new DirectoryInfo(CacheDir);
            if (!dir.Exists)
            {
                return;
            }

            var files = dir.GetFiles("*.xps").OrderBy(f => f.LastAccessTime).ToList( );
            var total = files.Sum(f => f.Length);
            if (total <= CacheQuotaBytes)
            {
                return;
            }

            foreach (var file in files)
            {
                if (total <= TrimWatermarkBytes)
                {
                    break;
                }

                try { total -= file.Length; file.Delete( ); } catch { }
            }
        }
        catch { }
    }
}
