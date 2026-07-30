using System;
using System.IO;
using System.Threading.Tasks;

using Syncfusion.DocIO.DLS;
using Syncfusion.DocIORenderer;
using Syncfusion.PresentationRenderer;

namespace LightBoard.Raster;

internal abstract class RasterDocument : IDisposable
{
    private readonly TaskCompletionSource<object?> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private byte[][]? pages;

    public bool IsReady => ready.Task.IsCompleted;

    public int PageCount { get; private set; }

    public Task WaitReadyAsync( )
    {
        return ready.Task;
    }

    public abstract Task OpenAsync(IProgress<(int done, int total)>? progress);

    /// <summary>
    /// 返回流的所有权归调用方，用毕须释放。
    /// </summary>
    public Task<Stream?> RenderPageAsync(int index)
    {
        if (pages is null || index < 0 || index >= pages.Length)
        {
            return Task.FromResult<Stream?>(null);
        }

        return Task.FromResult<Stream?>(new MemoryStream(pages[index], writable: false));
    }

    public virtual void Dispose( )
    {
        pages = null;
    }

    protected void SetPages(byte[][] rendered)
    {
        pages = rendered;
        PageCount = rendered.Length;
        ready.TrySetResult(null);
    }
}

internal sealed class PptxDocument(string path) : RasterDocument
{
    private readonly string path = path;

    public override async Task OpenAsync(IProgress<(int done, int total)>? progress)
    {
        var rendered = await Task.Run(( ) =>
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

        SetPages(rendered);
    }
}

// Syncfusion 只支持整篇渲染，故页数须等全部渲染完才可知。
internal sealed class DocxDocument(string path) : RasterDocument
{
    private readonly string path = path;

    public override async Task OpenAsync(IProgress<(int done, int total)>? progress)
    {
        var rendered = await Task.Run(( ) =>
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
                    result[i] = Image.ToArray(images[i]);
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

        SetPages(rendered);
    }
}
