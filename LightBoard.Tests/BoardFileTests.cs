using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows.Ink;
using System.Windows.Input;

namespace LightBoard.Tests;

// BoardFile 依赖 WPF Ink API，测试宿主主线程为 STA，可直接调用。
public class BoardFileTests
{
    private static string TempPath( )
    {
        return Path.Join(Path.GetTempPath( ), $"lb-board-{Guid.NewGuid( ):N}.lbf");
    }

    private static BoardPage MakePage(double scale, double offsetX, double offsetY, bool withStroke)
    {
        var strokes = new StrokeCollection( );
        if (withStroke)
        {
            strokes.Add(new Stroke(
                [new StylusPoint(10, 10), new StylusPoint(120, 80)],
                new DrawingAttributes( )));
        }

        return new BoardPage(strokes, scale, offsetX, offsetY);
    }

    [Fact]
    public void Write_Read_RoundTrip( )
    {
        BoardPage[] pages =
        [
            MakePage(1.25, 123.5, -45.25, withStroke: true),
            MakePage(1.0, 0, 0, withStroke: false),
        ];

        var path = TempPath( );
        try
        {
            Board.Write(path, pages);

            var content = Board.Read(path);

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
            Board.Write(path, [MakePage(1.0, 0, 0, withStroke: false)]);
            RewriteManifestVersion(path, 2);

            Assert.Throws<InvalidDataException>(( ) => Board.Read(path));
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
            Board.Write(path,
            [
                MakePage(1.0, 0, 0, withStroke: false),
                MakePage(1.0, 0, 0, withStroke: false),
            ]);

            using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
            {
                zip.GetEntry("pages/002.isf")!.Delete( );
            }

            Assert.Throws<InvalidDataException>(( ) => Board.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // Pages 为空是损坏文件而非可打开的空白板：LoadBoard 会把 PageIndex 置 0 而 Pages 无页，
    // CurrentPage 随即越界崩溃，必须在 Read 处拒绝。
    [Fact]
    public void Read_EmptyPages_Throws( )
    {
        var path = TempPath( );
        try
        {
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("manifest.json");
                using var stream = entry.Open( );
                JsonSerializer.Serialize(stream, new BoardManifest(1, []), BoardSerializerContext.Default.BoardManifest);
            }

            Assert.Throws<InvalidDataException>(( ) => Board.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Write_PathWithoutDirectory_CreatesFileInWorkingDirectory( )
    {
        var name = $"lb-board-{Guid.NewGuid( ):N}.lbf";
        try
        {
            Board.Write(name, [MakePage(1.0, 0, 0, withStroke: false)]);

            Assert.True(File.Exists(name));
        }
        finally
        {
            File.Delete(name);
        }
    }

    // 单页 ISF 超过 WPF 内部 4096 拷贝缓冲：旧实现把非 seekable 的 ZIP 条目流直接交给
    // StrokeCollection，deflate 短读被当作流结束，数据被静默截断后 DecodeISF 失败。
    // 截断的偏差量级在数十以上，ISF 往返的固有噪声远小于此（FitToCurve 关闭后几何为折线）。
    [Fact]
    public void Write_Read_RoundTrip_LargeInkNotTruncated( )
    {
        var strokes = new StrokeCollection( );
        for (var i = 0; i < 60; i++)
        {
            var points = new StylusPointCollection( );
            for (var j = 0; j < 200; j++)
            {
                points.Add(new StylusPoint(i * 5 + j * 0.5, 300 + Math.Sin(i * 0.7 + j * 0.1) * 100));
            }

            strokes.Add(new Stroke(points, new DrawingAttributes { FitToCurve = false }));
        }

        var path = TempPath( );
        try
        {
            Board.Write(path, [new BoardPage(strokes, 1.0, 0, 0)]);

            using (var zip = ZipFile.OpenRead(path))
            {
                Assert.True(zip.GetEntry("pages/001.isf")!.Length > 4096, "前置条件：解压后的 ISF 应超过 WPF 拷贝缓冲");
            }

            var content = Board.Read(path);

            var read = content.Pages[0].Strokes;
            Assert.Equal(strokes.Count, read.Count);

            // 全量几何一致：截断的偏差量级在数十以上，ISF 往返的坐标量化噪声实测约 0.012，取 0.1 容差
            var originalBounds = strokes.GetBounds( );
            var readBounds = read.GetBounds( );
            Assert.Equal(originalBounds.X, readBounds.X, 0.1);
            Assert.Equal(originalBounds.Y, readBounds.Y, 0.1);
            Assert.Equal(originalBounds.Width, readBounds.Width, 0.1);
            Assert.Equal(originalBounds.Height, readBounds.Height, 0.1);
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
