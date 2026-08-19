using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;

using Syncfusion.DocIO.DLS;
using Syncfusion.DocIORenderer;
using Syncfusion.PresentationRenderer;

using static LightBoard.Raster.Image;

namespace LightBoard.Raster;

public sealed class RasterService : IDisposable
{
    private const int MemoryCapacity = 50;

    private readonly LruCache<int, ImageSource> memory = new(MemoryCapacity);
    private readonly ConcurrentDictionary<int, Task<ImageSource?>> pageLoads = [];

    private byte[][]? pages;

    public bool HasDocument => pages is not null;

    public int PageCount => pages?.Length ?? 0;

    public async Task OpenAsync(string path, IProgress<(int done, int total)>? progress = null)
    {
        // 未知扩展名（如 .wps）按 docx 尝试打开。Syncfusion 只能整篇渲染，
        // 故所有页在此一次性转成字节，后续的"按需"仅指按页解码。
        var rendered = Path.GetExtension(path).ToUpperInvariant( ) switch
        {
            ".PPTX" or ".PPT" => await RenderPptxAsync(path, progress),
            _ => await RenderDocxAsync(path, progress)
        };

        pages = rendered;
    }

    public async Task<ImageSource?> GetOrRenderAsync(int pageIndex)
    {
        var doc = pages ?? throw new InvalidOperationException("尚未打开文档。");
        if (pageIndex < 0 || pageIndex >= doc.Length)
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
        var doc = pages;
        if (doc is null || pageIndex < 0 || pageIndex >= doc.Length)
        {
            return;
        }

        await GetOrRenderAsync(pageIndex);
    }

    public void Close( )
    {
        // 给在途解码最多 2 秒收尾，超时或失败均直接放弃。
        try
        {
            Task.WhenAll(pageLoads.Values).Wait(TimeSpan.FromSeconds(2));
        }
        catch { }

        pageLoads.Clear( );
        memory.Clear( );

        pages = null;
    }

    public void Dispose( )
    {
        Close( );
    }

    private async Task<ImageSource?> LoadPageAsync(byte[][] doc, int pageIndex)
    {
        await using var stream = new MemoryStream(doc[pageIndex], writable: false);
        var image = FromStream(stream);
        memory.Set(pageIndex, image);
        return image;
    }

    private static Task<byte[][]> RenderPptxAsync(string path, IProgress<(int done, int total)>? progress)
    {
        return Task.Run(( ) =>
        {
            var ppt = Syncfusion.Presentation.Presentation.Open(path);
            try
            {
                ppt.PresentationRenderer = new PresentationRenderer( );

                var result = new byte[ppt.Slides.Count][];
                for (var i = 0; i < result.Length; i++)
                {
                    using var image = ppt.Slides[i].ConvertToImage(Syncfusion.Presentation.ExportImageFormat.Png);
                    result[i] = Image.ToArray(image);
                    progress?.Report((i + 1, result.Length));
                }

                return result;
            }
            finally
            {
                ppt.Close( );
            }
        });
    }

    private static Task<byte[][]> RenderDocxAsync(string path, IProgress<(int done, int total)>? progress)
    {
        return Task.Run(( ) =>
        {
            using var sourceStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var document = new WordDocument(sourceStream, Syncfusion.DocIO.FormatType.Automatic);
            using var renderer = new DocIORenderer( );
            var images = document.RenderAsImages( );

            try
            {
                var result = new byte[images.Length][];
                for (var i = 0; i < images.Length; i++)
                {
                    result[i] = ToArray(images[i]);
                    progress?.Report((i + 1, images.Length));
                }

                return result;
            }
            finally
            {
                foreach (var imageStream in images)
                {
                    imageStream?.Dispose( );
                }
            }
        });
    }
}
