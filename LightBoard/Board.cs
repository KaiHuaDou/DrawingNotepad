using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Ink;

namespace LightBoard;

internal sealed record BoardPage(
    StrokeCollection Strokes,
    double Scale,
    double OffsetX,
    double OffsetY
);

internal sealed record BoardContent(int Version, IReadOnlyList<BoardPage> Pages);

internal sealed class BoardManifest(int version, IReadOnlyList<BoardPageInfo> pages)
{
    public int Version { get; set; } = version;
    public IReadOnlyList<BoardPageInfo> Pages { get; set; } = pages;
}

internal sealed class BoardPageInfo(double scale, double offsetX, double offsetY)
{
    public double Scale { get; set; } = scale;
    public double OffsetX { get; set; } = offsetX;
    public double OffsetY { get; set; } = offsetY;
}

internal static class Board
{
    public const string Ext = ".lbf";

    private const string Manifest = "manifest.json";

    private const string PagePrefix = "pages/";

    private const int CurrentVersion = 1;

    public static BoardContent Read(string path)
    {
        using var zip = ZipFile.OpenRead(path);

        var manifestEntry = zip.GetEntry(Manifest)
            ?? throw new InvalidDataException("文件缺少 manifest.json");

        using var manifestStream = manifestEntry.Open( );
        var manifest = JsonSerializer.Deserialize(manifestStream, BoardSerializerContext.Default.BoardManifest)
            ?? throw new InvalidDataException("manifest.json 解析失败");

        if (manifest.Version > CurrentVersion)
        {
            throw new InvalidDataException($"文件版本 {manifest.Version} 高于当前支持的版本 {CurrentVersion}");
        }

        var pages = new List<BoardPage>(manifest.Pages.Count);
        for (var i = 0; i < manifest.Pages.Count; i++)
        {
            var entry = zip.GetEntry($"{PagePrefix}{i + 1:D3}.isf")
                ?? throw new InvalidDataException($"文件缺少第 {i + 1} 页数据");

            var info = manifest.Pages[i];
            using var pageStream = entry.Open( );

            // ZIP 条目流不可 seek，且 Read 会在 deflate 块边界返回短块；WPF 对非 seekable 流
            // 按"读到短块即停"拷贝，超过一个缓冲区的数据会被静默截断。必须先完整转入可 seek 的流。
            using var pageBuffer = new MemoryStream( );
            pageStream.CopyTo(pageBuffer);
            pageBuffer.Position = 0;
            pages.Add(new BoardPage([with(pageBuffer)], info.Scale, info.OffsetX, info.OffsetY));
        }

        return new BoardContent(manifest.Version, pages);
    }

    public static void Write(string path, IReadOnlyList<BoardPage> pages)
    {
        var tempPath = $"{path}.tmp";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            // 归档必须在 File.Move 之前关闭，否则临时文件句柄仍被占用
            using (var zip = ZipFile.Open(tempPath, ZipArchiveMode.Create))
            {
                var manifest = new BoardManifest(
                    CurrentVersion,
                    [.. pages.Select(p => new BoardPageInfo(p.Scale, p.OffsetX, p.OffsetY))]);

                var manifestEntry = zip.CreateEntry(Manifest);
                // 流必须在后续 CreateEntry 之前关闭，ZipArchive 不允许多个条目同时处于打开状态
                using (var manifestStream = manifestEntry.Open( ))
                {
                    JsonSerializer.Serialize(manifestStream, manifest, BoardSerializerContext.Default.BoardManifest);
                }

                for (var i = 0; i < pages.Count; i++)
                {
                    var entry = zip.CreateEntry($"{PagePrefix}{i + 1:D3}.isf");
                    using var stream = entry.Open( );
                    pages[i].Strokes.Save(stream, true);
                }
            }

            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            try { File.Delete(tempPath); } catch { }

            throw;
        }
    }
}

[JsonSerializable(typeof(BoardManifest))]
internal sealed partial class BoardSerializerContext : JsonSerializerContext;
