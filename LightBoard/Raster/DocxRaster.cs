using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;
using Syncfusion.DocIORenderer;

namespace LightBoard.Raster;

/// <summary>
/// DOCX 栅格化策略：整篇一次性渲染为各页 PNG 流并缓存。
/// 渲染在 <see cref="OpenAsync"/> 中以后台任务触发，页数须等其完成（<see cref="WaitReadyAsync"/>）才可知。
/// </summary>
internal sealed class DocxRaster(string path) : IRasterDocument
{
    private readonly string path = path;

    private readonly TaskCompletionSource<object?> readySource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private MemoryStream[]? pages;

    public RasterKind Kind => RasterKind.Docx;

    public bool IsReady { get; private set; }

    public int PageCount { get; private set; }

    public Task OpenAsync(CancellationToken ct)
    {
        _ = RenderAllAsync(ct);
        return Task.CompletedTask;
    }

    public Task WaitReadyAsync(CancellationToken ct)
    {
        return readySource.Task.WaitAsync(ct);
    }

    public async Task<Stream?> RenderPageAsync(int index, CancellationToken ct)
    {
        await WaitReadyAsync(ct);

        if (pages is null || index < 0 || index >= pages.Length)
        {
            return null;
        }

        return pages[index];
    }

    public void Dispose( )
    {
        if (pages is not null)
        {
            foreach (var stream in pages)
            {
                stream?.Dispose( );
            }

            pages = null;
        }
    }

    private async Task RenderAllAsync(CancellationToken ct)
    {
        try
        {
            var streams = await Task.Run(( ) =>
            {
                using var sourceStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var document = new WordDocument(sourceStream, FormatType.Automatic);
                using var renderer = new DocIORenderer( );
                var images = document.RenderAsImages( );

                try
                {
                    var result = new MemoryStream[images.Length];
                    for (var i = 0; i < images.Length; i++)
                    {
                        images[i].Position = 0;
                        var ms = new MemoryStream( );
                        images[i].CopyTo(ms);
                        ms.Position = 0;
                        result[i] = ms;
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
            }, ct);

            pages = streams;
            PageCount = streams.Length;
            IsReady = true;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            App.LogException(ex);
        }
        finally
        {
            readySource.TrySetResult(null);
        }
    }
}
