using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Xps.Packaging;

using NetOffice.OfficeApi.Enums;
using NetOffice.PowerPointApi.Enums;
using NetOffice.WordApi.Enums;

using PpApplication = NetOffice.PowerPointApi.Application;
using WdApplication = NetOffice.WordApi.Application;

namespace LightBoard;

public partial class App
{
    public static DocumentService? Document { get; internal set; }

    public static async Task OpenDocument(string path)
    {
        var document = await DocumentService.OpenAsync(path);

        Pages.Clear( );
        for (var i = 0; i < document.PageCount; i++)
        {
            Pages.Add(new Page
            {
                Number = i + 1,
                Scale = 1.0,
                OffsetX = 16384 - SystemParameters.WorkArea.Width / 2,
                OffsetY = 8192 - SystemParameters.WorkArea.Height / 2,
            });
        }

        Document = document;
        PageIndex = 0;
    }
}

public partial class MainWindow
{
    private static bool IsDocumentFile(string fileName)
    {
        return Path.GetExtension(fileName).ToUpperInvariant( ) is ".PPTX" or ".PPT" or ".DOCX" or ".DOC";
    }

    private void CloseDocumentViewer( )
    {
        // 先清空页面视图再释放 XPS，否则已渲染的页面会失效。
        CanvasNext.SetDocumentPage(null);

        App.Document?.Dispose( );
        App.Document = null;
    }
}
public sealed class DocumentService : IDisposable
{
    private const long CacheQuotaBytes = 512L * 1024 * 1024;

    private const long TrimWatermarkBytes = 400L * 1024 * 1024;

    private static readonly string cacheDir = Path.Join(App.AppPath, "XpsCache");
    private static readonly ConcurrentDictionary<string, Lazy<Task<(string Path, string? Temp)>>> converting = new( );
    private readonly FixedDocumentSequence sequence;
    private readonly string? tempPath;
    private readonly XpsDocument xps;

    private DocumentService(XpsDocument xps, FixedDocumentSequence sequence, string? tempPath)
    {
        this.xps = xps;
        this.sequence = sequence;
        this.tempPath = tempPath;
        PageCount = CountPages(sequence);
    }

    public int PageCount { get; }

    public static async Task<DocumentService> OpenAsync(string path)
    {
        var cachePath = await Task.Run(( ) =>
        {
            TrimCache( );
            return GetCachePath(path);
        });

        // 命中缓存直接打开；缓存损坏直接删除（删除失败忽略），落到下方重新转换。
        if (File.Exists(cachePath))
        {
            try
            {
                Touch(cachePath);
                return OpenXps(cachePath);
            }
            catch
            {
                try { File.Delete(cachePath); } catch { }
            }
        }

        // 同一源文件并发打开时只转换一次。
        var lazy = converting.GetOrAdd(cachePath, key => new Lazy<Task<(string Path, string? Temp)>>(
            ( ) => ConvertAndCacheAsync(key, path), LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            var (ready, temp) = await lazy.Value;
            return OpenXps(ready, temp);
        }
        finally
        {
            converting.TryRemove(new KeyValuePair<string, Lazy<Task<(string Path, string? Temp)>>>(cachePath, lazy));
        }
    }

    public void Dispose( )
    {
        // 缓存文件留给下次打开复用，这里只释放 XPS 句柄；临时产物（缓存写入失败时）删除，失败忽略。
        xps.Close( );
        if (tempPath != null)
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    public ImageSource? GetPage(int index)
    {
        var i = 0;
        foreach (var docRef in sequence.References)
        {
            var doc = docRef.GetDocument(false);
            if (index < i + doc.Pages.Count)
            {
                var fixedPage = doc.Pages[index - i].GetPageRoot(false);
                return RenderFixedPage(fixedPage);
            }

            i += doc.Pages.Count;
        }

        return null;
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

    private static int CountPages(FixedDocumentSequence sequence)
    {
        var count = 0;
        foreach (var docRef in sequence.References)
        {
            count += docRef.GetDocument(false).Pages.Count;
        }

        return count;
    }

    private static string GetCachePath(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create( );
        return Path.Join(cacheDir, Convert.ToHexString(sha.ComputeHash(stream)) + ".xps");
    }
    private static DocumentService OpenXps(string cachePath, string? tempPath = null)
    {
        var xps = new XpsDocument(cachePath, FileAccess.Read);
        try
        {
            return new DocumentService(xps, xps.GetFixedDocumentSequence( ), tempPath);
        }
        catch
        {
            xps.Close( );
            throw;
        }
    }

    private static RenderTargetBitmap RenderFixedPage(FixedPage fixedPage)
    {
        const double Dpi = 192; // 2x 渲染，放大查看时更清晰
        fixedPage.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = fixedPage.DesiredSize;
        if (size.Width <= 0 || double.IsNaN(size.Width) || size.Height <= 0 || double.IsNaN(size.Height))
        {
            size = new Size(fixedPage.Width, fixedPage.Height);
        }

        fixedPage.Arrange(new Rect(size));
        var pxW = (int) Math.Max(1, Math.Round(size.Width * Dpi / 96d));
        var pxH = (int) Math.Max(1, Math.Round(size.Height * Dpi / 96d));
        var bitmap = new RenderTargetBitmap(pxW, pxH, Dpi, Dpi, PixelFormats.Pbgra32);
        bitmap.Render(fixedPage);
        return bitmap;
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

    // 更新最近使用时间，供 LRU 清理排序。
    private static void Touch(string cachePath)
    {
        try { File.SetLastAccessTime(cachePath, DateTime.Now); } catch { }
    }

    // 超出配额按最近使用时间从旧到新清理；正在被打开的缓存文件删除会失败，跳过即可。
    private static void TrimCache( )
    {
        try
        {
            var dir = new DirectoryInfo(cacheDir);
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
