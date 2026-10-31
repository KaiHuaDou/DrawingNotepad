using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace InkCanvasNext.Tests;

/// <summary>文档页面盒与画布尺寸：页面盒更大时扩展画布，墨迹层保持原点。</summary>
public class DocumentPageTests
{
    [Fact]
    public void SetDocumentPage_LargerThanCanvas_ExtendsCanvasAndKeepsOrigin( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;
            var width = canvas.InnerCanvasElement.Width;
            var height = canvas.InnerCanvasElement.Height;
            var page = new WriteableBitmap(10, 10, 96, 96, PixelFormats.Pbgra32, null);
            var box = new Rect(0, 0, width + 200, height + 200);

            canvas.SetDocumentPage(page, box);
            canvas.UpdateLayout( );

            Assert.True(canvas.InnerCanvasElement.Width >= box.Right);
            Assert.True(canvas.InnerCanvasElement.Height >= box.Bottom);

            var origin = canvas.InnerCanvasElement.TransformToVisual(canvas.CanvasGrid).Transform(new Point(0, 0));
            Assert.Equal(0, origin.X, 6);
            Assert.Equal(0, origin.Y, 6);
        });
    }

    [Fact]
    public void SetDocumentPage_Null_RemovesPageWithoutResizing( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;
            var width = canvas.InnerCanvasElement.Width;

            canvas.SetDocumentPage(null, null);

            Assert.Equal(width, canvas.InnerCanvasElement.Width);
        });
    }
}
