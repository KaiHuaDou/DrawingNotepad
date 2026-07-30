using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;

using static LightBoard.Raster.Image;

namespace LightBoard.Raster;

public sealed class RasterService : IDisposable
{
    private const int MemoryCapacity = 50;

    private readonly LruCache<int, ImageSource> memory = new(MemoryCapacity);
    private readonly ConcurrentDictionary<int, Task<ImageSource?>> pageLoads = [];

    private RasterDocument? document;

    public bool HasDocument => document is not null;

    public bool IsReady => document?.IsReady == true;

    public int PageCount => document?.PageCount ?? 0;

    public Task WaitReadyAsync( )
    {
        return document?.WaitReadyAsync( ) ?? Task.CompletedTask;
    }

    public async Task OpenAsync(string path, IProgress<(int done, int total)>? progress = null)
    {
        // 未知扩展名（如 .wps）按 docx 尝试打开。
        RasterDocument opened = Path.GetExtension(path).ToUpperInvariant( ) switch
        {
            ".PPTX" or ".PPT" => new PptxDocument(path),
            _ => new DocxDocument(path)
        };

        try
        {
            await opened.OpenAsync(progress);
        }
        catch
        {
            opened.Dispose( );
            throw;
        }

        document = opened;
    }

    public async Task<ImageSource?> GetOrRenderAsync(int pageIndex)
    {
        var doc = document ?? throw new InvalidOperationException("尚未打开文档。");
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

    public async Task PrefetchAsync(int pageIndex)
    {
        var doc = document;
        if (doc?.IsReady != true || pageIndex < 0 || pageIndex >= doc.PageCount)
        {
            return;
        }

        await GetOrRenderAsync(pageIndex);
    }

    public void Close( )
    {
        // 给在途渲染最多 2 秒收尾，超时或失败均直接放弃。
        try
        {
            Task.WhenAll(pageLoads.Values).Wait(TimeSpan.FromSeconds(2));
        }
        catch { }

        pageLoads.Clear( );
        memory.Clear( );

        document?.Dispose( );
        document = null;
    }

    public void Dispose( )
    {
        Close( );
    }

    private async Task<ImageSource?> LoadPageAsync(RasterDocument doc, int pageIndex)
    {
        await doc.WaitReadyAsync( );

        await using var stream = await doc.RenderPageAsync(pageIndex);
        if (stream is null)
        {
            return Error("Stream is null");
        }

        var image = FromStream(stream);
        memory.Set(pageIndex, image);
        return image;
    }
}
