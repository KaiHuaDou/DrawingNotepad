using System;
using System.IO;
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
    private readonly FixedDocumentSequence sequence;
    private readonly string tempPath;
    private readonly XpsDocument xps;

    private DocumentService(XpsDocument xps, string tempPath, FixedDocumentSequence sequence)
    {
        this.xps = xps;
        this.tempPath = tempPath;
        this.sequence = sequence;
        PageCount = CountPages(sequence);
    }

    public int PageCount { get; }

    public static async Task<DocumentService> OpenAsync(string path)
    {
        var tempPath = await RunOnStaAsync(( ) => ConvertToTempXps(path));
        try
        {
            var xps = new XpsDocument(tempPath, FileAccess.Read);
            return new DocumentService(xps, tempPath, xps.GetFixedDocumentSequence( ));
        }
        catch
        {
            try { File.Delete(tempPath); } catch { }

            throw;
        }
    }

    public void Dispose( )
    {
        xps.Close( );
        try { File.Delete(tempPath); } catch { }
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

    private static string ConvertToTempXps(string path)
    {
        var tempPath = Path.Combine(Path.GetTempPath( ), $"lightboard-{Guid.NewGuid( ):N}.xps");

        if (Path.GetExtension(path).ToUpperInvariant( ) is ".PPTX" or ".PPT")
        {
            ConvertPptx(path, tempPath);
        }
        else
        {
            // 未知扩展名（如 .wps）按 Word 尝试打开。
            ConvertDocx(path, tempPath);
        }

        return tempPath;
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
}
