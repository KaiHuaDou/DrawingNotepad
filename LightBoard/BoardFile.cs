using System;
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

internal static class BoardFile
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
            pages.Add(new BoardPage([with(pageStream)], info.Scale, info.OffsetX, info.OffsetY));
        }

        return new BoardContent(manifest.Version, pages);
    }

    public static void Write(string path, IReadOnlyList<Page> pages)
    {
        var tempPath = $"{path}.tmp";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            using var zip = ZipFile.Open(tempPath, ZipArchiveMode.Create);
            var manifest = new BoardManifest(
                CurrentVersion,
                [.. pages.Select(p => new BoardPageInfo(p.Scale, p.OffsetX, p.OffsetY))]);

            var manifestEntry = zip.CreateEntry(Manifest);
            using var manifestStream = manifestEntry.Open( );
            JsonSerializer.Serialize(manifestStream, manifest, BoardSerializerContext.Default.BoardManifest);

            for (var i = 0; i < pages.Count; i++)
            {
                var entry = zip.CreateEntry($"{PagePrefix}{i + 1:D3}.isf");
                using var stream = entry.Open( );
                pages[i].Strokes.Save(stream, true);
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
