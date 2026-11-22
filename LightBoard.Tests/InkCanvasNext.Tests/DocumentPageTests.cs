using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace InkCanvasNext.Tests;

/// <summary>文档页面盒与画布尺寸：页面盒更大时扩展画布，墨迹层保持原点</summary>
public class DocumentPageTests
{
    [Fact]
    public void SetDocumentPage_LargerThanCanvas_ExtendsCanvasAndKeepsOrigin( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;
            var width = canvas.InnerCanvas.Width;
            var height = canvas.InnerCanvas.Height;
            var page = new WriteableBitmap(10, 10, 96, 96, PixelFormats.Pbgra32, null);
            var box = new Rect(0, 0, width + 200, height + 200);

            canvas.SetDocumentPage(page, box);
            canvas.UpdateLayout( );

            Assert.True(canvas.InnerCanvas.Width >= box.Right);
            Assert.True(canvas.InnerCanvas.Height >= box.Bottom);

            var origin = canvas.InnerCanvas.TransformToVisual(canvas.CanvasGrid).Transform(new Point(0, 0));
            Assert.Equal(0, origin.X, 6);
            Assert.Equal(0, origin.Y, 6);
        });
    }

    [Fact]
    public void SetDocumentPage_NegativeBox_ShiftsInkWithPage( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;
            var stroke = new Stroke(
            [
                new StylusPoint(100, 100),
                new StylusPoint(200, 200),
            ]);
            canvas.Strokes.Add(stroke);
            var before = stroke.GetBounds( );

            var page = new WriteableBitmap(10, 10, 96, 96, PixelFormats.Pbgra32, null);
            canvas.SetDocumentPage(page, new Rect(-100, -50, 10, 10));

            var after = stroke.GetBounds( );
            Assert.Equal(before.Left + 100, after.Left, 6);
            Assert.Equal(before.Top + 50, after.Top, 6);
        });
    }

    [Fact]
    public void SetDocumentPage_Null_RemovesPageWithoutResizing( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;
            var width = canvas.InnerCanvas.Width;

            canvas.SetDocumentPage(null, null);

            Assert.Equal<double>(width, canvas.InnerCanvas.Width);
        });
    }
}
