using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Ink;

namespace LightBoard;

internal sealed record BoardPage(StrokeCollection Strokes, double Scale, double OffsetX, double OffsetY);

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
    public const string Extension = ".lbf";

    private const string ManifestName = "manifest.json";

    private const string PagePrefix = "pages/";

    private const int CurrentVersion = 1;

    public static BoardContent Read(string path)
    {
        using var zip = ZipFile.OpenRead(path);

        var manifestEntry = zip.GetEntry(ManifestName)
            ?? throw new InvalidDataException("文件缺少 manifest.json");
        var manifest = DeserializeManifest(manifestEntry);

        var pages = new List<BoardPage>(manifest.Pages.Count);
        for (var i = 0; i < manifest.Pages.Count; i++)
        {
            var entry = zip.GetEntry($"{PagePrefix}{i + 1:D3}.isf");
            var strokes = entry is null ? [] : ReadStrokes(entry);
            var info = manifest.Pages[i];
            pages.Add(new BoardPage(strokes, info.Scale, info.OffsetX, info.OffsetY));
        }

        return new BoardContent(manifest.Version, pages);
    }

    public static void Write(string path, IReadOnlyList<Page> pages)
    {
        WriteAtomic(path, zip =>
        {
            var manifest = new BoardManifest(
                CurrentVersion,
                [.. pages.Select(p => new BoardPageInfo(p.Scale, p.OffsetX, p.OffsetY))]);

            WriteManifest(zip, manifest);

            for (var i = 0; i < pages.Count; i++)
            {
                var entry = zip.CreateEntry($"{PagePrefix}{i + 1:D3}.isf");
                using var stream = entry.Open( );
                pages[i].Strokes.Save(stream, true);
            }
        });
    }

    private static void WriteAtomic(string path, Action<ZipArchive> write)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = $"{path}.tmp";
        using (var zip = ZipFile.Open(temp, ZipArchiveMode.Create))
        {
            write(zip);
        }

        File.Move(temp, path, true);
    }

    private static BoardManifest DeserializeManifest(ZipArchiveEntry entry)
    {
        using var stream = entry.Open( );
        return JsonSerializer.Deserialize(stream, BoardSerializerContext.Default.BoardManifest)
            ?? throw new InvalidDataException("manifest.json 解析失败");
    }

    private static void WriteManifest(ZipArchive zip, BoardManifest manifest)
    {
        var entry = zip.CreateEntry(ManifestName);
        using var stream = entry.Open( );
        JsonSerializer.Serialize(stream, manifest, BoardSerializerContext.Default.BoardManifest);
    }

    private static StrokeCollection ReadStrokes(ZipArchiveEntry entry)
    {
        using var stream = entry.Open( );
        return new StrokeCollection(stream);
    }
}

[JsonSerializable(typeof(BoardManifest))]
internal sealed partial class BoardSerializerContext : JsonSerializerContext;
