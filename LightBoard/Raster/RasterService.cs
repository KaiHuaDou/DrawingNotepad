using System;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

using static LightBoard.Raster.Image;

namespace LightBoard.Raster;

public sealed class RasterSession(IRasterDocument document)
{
    ~RasterSession( )
    {
        Document.Dispose( );
    }

    public IRasterDocument Document { get; } = document;

    public bool IsPageCountReady => Document.IsReady;

    public int PageCount => Document.PageCount;

    public Task PageCountReady => Document.WaitReadyAsync(CancellationToken.None);
}

public sealed class RasterService : IDisposable
{
    private const int MemoryCapacity = 50;

    private readonly LruCache<int, ImageSource> memory = new(MemoryCapacity);
    private readonly ConcurrentDictionary<int, Task<ImageSource?>> pageLoads = [];
    private readonly CancellationTokenSource tokenSource = new( );

    public RasterSession? Session { get; private set; }

    public void Close( )
    {
        tokenSource.Cancel( );

        try
        {
            Task.WhenAll(pageLoads.Values).Wait(TimeSpan.FromSeconds(2));
        }
        catch { }

        pageLoads.Clear( );
        memory.Clear( );

        Session?.Document.Dispose( );
        Session = null;
    }

    public void Dispose( )
    {
        Close( );
        tokenSource.Dispose( );
    }

    public async Task<ImageSource?> GetOrRenderAsync(int pageIndex)
    {
        var doc = Session?.Document ?? throw new InvalidOperationException("尚未打开文档。");
        if (pageIndex < 0 || (doc.IsReady && pageIndex >= doc.PageCount))
        {
            return null;
        }

        if (memory.TryGet(pageIndex, out var cached))
        {
            return cached;
        }

        var task = pageLoads.GetOrAdd(pageIndex, _ => LoadPageAsync(doc, pageIndex));
        try
        {
            return await task;
        }
        catch (Exception ex)
        {
            return Error(ex.Message);
        }
        finally
        {
            pageLoads.TryRemove(new KeyValuePair<int, Task<ImageSource?>>(pageIndex, task));
        }
    }

    public async Task<RasterSession> OpenAsync(string path)
    {
        var kind = Path.GetExtension(path).ToUpperInvariant( ) switch
        {
            ".PPTX" or ".PPT" => RasterKind.Pptx,
            ".DOCX" or ".DOC" => RasterKind.Docx,
            _ => RasterKind.Docx // 尝试认作 docx，以处理打开 .wps 等的情况
        };

#pragma warning disable CA2000

        IRasterDocument document = kind == RasterKind.Pptx
            ? new PptxRaster(path)
            : new DocxRaster(path);

#pragma warning restore CA2000

        await document.OpenAsync(tokenSource.Token);

        var session = new RasterSession(document);
        Session = session;
        return session;
    }

    public async Task PrefetchAsync(int pageIndex)
    {
        var current = Session;
        if (current?.IsPageCountReady != true)
        {
            return;
        }

        if (pageIndex < 0 || pageIndex >= current.PageCount)
        {
            return;
        }

        await GetOrRenderAsync(pageIndex);
    }

    private async Task<ImageSource?> LoadPageAsync(IRasterDocument document, int pageIndex)
    {
        var ct = tokenSource.Token;

        await document.WaitReadyAsync(ct);

        var stream = await document.RenderPageAsync(pageIndex, ct);
        if (stream is null)
        {
            return null;
        }

        var image = await Task.Run(( ) => FromStream(stream), ct);
        memory.Set(pageIndex, image);
        return image;
    }
}
