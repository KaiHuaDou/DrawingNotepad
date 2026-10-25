using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

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
            PdfWriter.Export(path, pages, new DpiScale(1, 1), 100);

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
}
