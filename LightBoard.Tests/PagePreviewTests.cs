using System.Windows.Ink;
using System.Windows.Input;

namespace LightBoard.Tests;

// 缩略图的重建本身在后台线程进行，这里只断言与线程无关的行为：
// 过期标记的流转、读取永不返回 null、刷新不改动页面自带墨迹。
public class PagePreviewTests
{
    [Fact]
    public void NewPage_PreviewIsNotStale( )
    {
        Assert.False(new Page( ).PreviewStale);
    }

    [Fact]
    public void InvalidatePreview_MarksStale( )
    {
        var page = new Page( );

        page.InvalidatePreview( );

        Assert.True(page.PreviewStale);
    }

    [Fact]
    public void Preview_WithoutInvalidation_ReturnsSameImage( )
    {
        var page = new Page( );

        Assert.Same(page.Preview, page.Preview);
    }

    [Fact]
    public void RefreshPreview_LeavesPageStrokesUntouched( )
    {
        var page = new Page( );
        page.Strokes.Add(new Stroke(
            [new StylusPoint(10, 10), new StylusPoint(120, 80)],
            new DrawingAttributes( )));
        page.InvalidatePreview( );

        page.RefreshPreview( );

        Assert.Single(page.Strokes);
        Assert.NotNull(page.Preview);
    }
}
