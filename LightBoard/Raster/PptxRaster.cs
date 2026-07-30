using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Syncfusion.Presentation;
using Syncfusion.PresentationRenderer;

namespace LightBoard.Raster;

internal sealed class PptxRaster(string path) : IRasterDocument
{
    private readonly string path = path;

    private readonly SemaphoreSlim renderGate = new(1, 1);

    private readonly ConcurrentDictionary<int, MemoryStream> pageStreams = new( );

    private readonly TaskCompletionSource<object?> readySource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private IPresentation? presentation;

    public RasterKind Kind => RasterKind.Pptx;

    public bool IsReady { get; private set; }

    public int PageCount { get; private set; }

    public Task OpenAsync(CancellationToken ct)
    {
        return Task.Run(( ) =>
        {
            presentation = Presentation.Open(path);
            presentation.PresentationRenderer = new PresentationRenderer( );
            PageCount = presentation.Slides.Count;
            IsReady = true;
            readySource.TrySetResult(null);
        }, ct);
    }

    public Task WaitReadyAsync(CancellationToken ct)
    {
        return readySource.Task.WaitAsync(ct);
    }

    public async Task<Stream?> RenderPageAsync(int index, CancellationToken ct)
    {
        if (pageStreams.TryGetValue(index, out var cached))
        {
            return cached;
        }

        await renderGate.WaitAsync(ct);
        try
        {
            // 双检：等待 gate 期间可能已有其他任务渲染了该页。
            if (pageStreams.TryGetValue(index, out var cached2))
            {
                return cached2;
            }

            presentation ??= OpenPresentation( );

            var stream = await Task.Run(( ) =>
            {
                using var image = presentation.Slides[index].ConvertToImage(ExportImageFormat.Png);
                image.Position = 0;
                var stream = new MemoryStream( );
                image.CopyTo(stream);
                stream.Position = 0;
                return stream;
            }, ct);

            pageStreams[index] = stream;
            return stream;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            renderGate.Release( );
        }
    }

    public void Dispose( )
    {
        presentation?.Close( );
        foreach (var pair in pageStreams)
        {
            pair.Value?.Dispose( );
        }

        pageStreams.Clear( );
        renderGate.Dispose( );
    }

    private IPresentation OpenPresentation( )
    {
        var presentation = Presentation.Open(path);
        presentation.PresentationRenderer = new PresentationRenderer( );
        return presentation;
    }
}
