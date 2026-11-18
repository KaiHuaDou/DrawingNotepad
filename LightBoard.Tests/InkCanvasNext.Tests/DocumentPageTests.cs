using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace InkCanvasNext.Tests;

/// <summary>文档页面盒与画布尺寸：页面盒更大时扩展画布，墨迹层保持原点</summary>
public class DocumentPageTests
{
    [Fact]
    public void SetDocumentPage_LargerThanCanvas_ExtendsCanvasAndKeepsOrigin( )
    {
        StaTest.Run((Action) (( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;
            var width = canvas.InnerCanvas.Width;
            var height = canvas.InnerCanvas.Height;
            var page = new WriteableBitmap(10, 10, 96, 96, PixelFormats.Pbgra32, null);
            var box = new Rect((double) 0, (double) 0, width + 200, height + 200);

            canvas.SetDocumentPage(page, box);
            canvas.UpdateLayout( );

            Assert.True(canvas.InnerCanvas.Width >= box.Right);
            Assert.True(canvas.InnerCanvas.Height >= box.Bottom);

            var origin = canvas.InnerCanvas.TransformToVisual((Visual) canvas.CanvasGrid).Transform(new Point(0, 0));
            Assert.Equal((double) 0, origin.X, 6);
            Assert.Equal((double) 0, origin.Y, 6);
        }));
    }

    [Fact]
    public void SetDocumentPage_Null_RemovesPageWithoutResizing( )
    {
        StaTest.Run((Action) (( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;
            var width = canvas.InnerCanvas.Width;

            canvas.SetDocumentPage(null, null);

            Assert.Equal<double>(width, canvas.InnerCanvas.Width);
        }));
    }
}
