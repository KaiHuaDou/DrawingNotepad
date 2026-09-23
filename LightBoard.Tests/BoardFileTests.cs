using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows.Ink;
using System.Windows.Input;

namespace LightBoard.Tests;

// BoardFile 与 Page 依赖 WPF Ink API，测试宿主主线程为 STA，可直接调用。
public class BoardFileTests
{
    private static string TempPath( )
    {
        return Path.Join(Path.GetTempPath( ), $"lb-board-{Guid.NewGuid( ):N}.lbf");
    }

    private static Page MakePage(int number, double scale, double offsetX, double offsetY, bool withStroke)
    {
        var page = new Page { Number = number, Scale = scale, OffsetX = offsetX, OffsetY = offsetY };
        if (withStroke)
        {
            page.Strokes.Add(new Stroke(
                [new StylusPoint(10, 10), new StylusPoint(120, 80)],
                new DrawingAttributes( )));
        }

        return page;
    }

    [Fact]
    public void Write_Read_RoundTrip( )
    {
        var pages = new List<Page>
        {
            MakePage(1, 1.25, 123.5, -45.25, withStroke: true),
            MakePage(2, 1.0, 0, 0, withStroke: false),
        };

        var path = TempPath( );
        try
        {
            BoardFile.Write(path, pages);

            var content = BoardFile.Read(path);

            Assert.Equal(2, content.Pages.Count);
            Assert.Equal(1.25, content.Pages[0].Scale, 12);
            Assert.Equal(123.5, content.Pages[0].OffsetX, 12);
            Assert.Equal(-45.25, content.Pages[0].OffsetY, 12);
            Assert.Single(content.Pages[0].Strokes);
            Assert.Empty(content.Pages[1].Strokes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_NewerVersion_Throws( )
    {
        var path = TempPath( );
        try
        {
            BoardFile.Write(path, [MakePage(1, 1.0, 0, 0, withStroke: false)]);
            RewriteManifestVersion(path, 2);

            Assert.Throws<InvalidDataException>(( ) => BoardFile.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_MissingPageEntry_Throws( )
    {
        var path = TempPath( );
        try
        {
            BoardFile.Write(path,
            [
                MakePage(1, 1.0, 0, 0, withStroke: false),
                MakePage(2, 1.0, 0, 0, withStroke: false),
            ]);

            using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
            {
                zip.GetEntry("pages/002.isf")!.Delete( );
            }

            Assert.Throws<InvalidDataException>(( ) => BoardFile.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // 压缩包条目无法就地改写，Update 模式下删除后重建 manifest.json。
    private static void RewriteManifestVersion(string path, int version)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
        var entry = zip.GetEntry("manifest.json")!;
        BoardManifest manifest;
        using (var stream = entry.Open( ))
        {
            manifest = JsonSerializer.Deserialize(stream, BoardSerializerContext.Default.BoardManifest)!;
        }

        manifest.Version = version;
        entry.Delete( );
        using var output = zip.CreateEntry("manifest.json").Open( );
        JsonSerializer.Serialize(output, manifest, BoardSerializerContext.Default.BoardManifest);
    }
}
