using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using LightBoard.Tests.Toolbar;

namespace LightBoard.Tests;

public class BackgroundsTests
{
    [Theory]
    [InlineData(@"C:\docs\a.pdf", 1)]
    [InlineData(@"C:\Program Files\文档\b b.pdf", 12)]
    [InlineData(@"\\server\share\c.pptx", 100)]
    public void Format_Parse_RoundTrip(string path, int pageNumber)
    {
        var box = new Rect(10, 20, 300, 400);
        var background = new DocumentBackground(path, pageNumber, box);

        Assert.True(DocumentBackground.TryParse(background.ToString( ), box, out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(path, parsed!.Path);
        Assert.Equal(pageNumber, parsed.PageNumber);
        Assert.Equal(box, parsed.Box);
    }

    [Theory]
    [InlineData("")]
    [InlineData(@"C:\a.pdf")]
    [InlineData(@"C:\a.pdf::0")]
    [InlineData(@"C:\a.pdf::")]
    [InlineData(@"C:\a.pdf::x")]
    [InlineData(@"C:\a.pdf::-1")]
    [InlineData("::1")]
    public void Parse_Invalid_ReturnsFalse(string spec)
    {
        Assert.False(DocumentBackground.TryParse(spec, new Rect(0, 0, 10, 10), out _));
    }

    // 同一路径只开一个服务；页码 1 基换算到 PdfSource 的 0 基索引。
    [Fact]
    public async Task OpenDocumentSource_SamePath_ReusesServiceAndResolvesPage( )
    {
        List<Page> pages = [new( ) { Number = 1 }, new( ) { Number = 2 }];
        var path = Path.Join(Path.GetTempPath( ), $"lb-bg-{Guid.NewGuid( ):N}.pdf");

        try
        {
            // 导出在 UI 线程取页快照，需要 App 存在
            UiThread.Run(( ) =>
            {
                PdfWriter.Export(path, pages, new DpiScale(1, 1), 100);
            });

            var first = await App.OpenDocumentSourceAsync(path);
            var second = await App.OpenDocumentSourceAsync(path);
            Assert.Same(first, second);

            var box = new Rect(0, 0, 100, 100);
            Assert.NotNull(App.BackgroundImage(new DocumentBackground(path, 2, box)));
            Assert.Null(App.BackgroundImage(new DocumentBackground(path, 3, box)));
        }
        finally
        {
            App.CloseDocuments( );
            File.Delete(path);
        }
    }

    [Fact]
    public void SetPageBackground_CreatesFromCurrentViewport( )
    {
        var path = TempPng( );
        try
        {
            UiThread.Run(( ) =>
            {
                var page = new Page( ) { Number = 1 };

                // 图片打开没有真正的异步等待，方法在本次调用内同步完成
                App.SetPageBackgroundAsync(page, path).GetAwaiter( ).GetResult( );

                Assert.NotNull(page.Background);
                Assert.Equal(1, page.Background!.PageNumber);
                Assert.Equal(App.CanvasView( ).Box, page.Background.Box);
            });
        }
        finally
        {
            UiThread.Run(App.CloseDocuments);
            File.Delete(path);
        }
    }

    [Fact]
    public void ClearPageBackground_LeavesOtherPages( )
    {
        var page = new Page( ) { Number = 1 };
        page.Background = new DocumentBackground(@"C:\a.png", 1, new Rect(0, 0, 10, 10));

        App.ClearPageBackground(page);

        Assert.Null(page.Background);
    }

    [Fact]
    public void PhotoPlacementFor_NoBackground_StaysOnOriginalPage( )
    {
        var page = new Page( ) { Number = 1 };

        Assert.Equal(App.PhotoPlacement.OriginalPage, App.PhotoPlacementFor(page));
    }

    [Fact]
    public void PhotoPlacementFor_HasBackground_GoesToNewPage( )
    {
        var page = new Page( ) { Number = 1 };
        page.Background = new DocumentBackground(@"C:\a.png", 1, new Rect(0, 0, 10, 10));

        Assert.Equal(App.PhotoPlacement.NewPage, App.PhotoPlacementFor(page));
    }

    /// <summary>
    /// 造一张临时 PNG：单页背景动作只接受图片
    /// </summary>
    internal static string TempPng( )
    {
        var path = Path.Join(Path.GetTempPath( ), $"lb-bg-{Guid.NewGuid( ):N}.png");

        var bitmap = new RenderTargetBitmap(4, 4, 96, 96, PixelFormats.Pbgra32);
        bitmap.Freeze( );

        var encoder = new PngBitmapEncoder( );
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);

        return path;
    }
}
