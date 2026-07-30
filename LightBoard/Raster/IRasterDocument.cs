using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LightBoard.Raster;

public enum RasterKind
{
    Pptx,
    Docx
}

public interface IRasterDocument : IDisposable
{
    RasterKind Kind { get; }

    bool IsReady { get; }

    int PageCount { get; }

    Task OpenAsync(CancellationToken ct);

    Task WaitReadyAsync(CancellationToken ct);

    Task<Stream?> RenderPageAsync(int index, CancellationToken ct);
}
