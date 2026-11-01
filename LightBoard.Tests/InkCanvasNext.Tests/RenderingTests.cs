using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace InkCanvasNext.Tests;

/// <summary>渲染 API 的底色参数与像素输出</summary>
public class RenderingTests
{
    [Fact]
    public void PreviewEmpty_FillsRequestedColor( )
    {
        StaTest.Run(( ) =>
        {
            var bitmap = StrokeCollectionExtension.PreviewEmpty(Colors.Red);

            Assert.Equal(300, bitmap.PixelWidth);
            Assert.Equal(162, bitmap.PixelHeight);
            AssertPixel(bitmap, 0, 0, Colors.Red);
        });
    }

    [Fact]
    public void Preview_PaintsRequestedCanvasColor( )
    {
        StaTest.Run(( ) =>
        {
            var bitmap = new StrokeCollection( ).Preview(canvas: Colors.Red);

            AssertPixel(bitmap, 10, 10, Colors.Red);
        });
    }

    [Fact]
    public void Render_PaintsRequestedCanvasColor( )
    {
        StaTest.Run(( ) =>
        {
            // 用极小的笔画控制输出位图尺寸，避免大画布回退尺寸放大内存占用
            var strokes = new StrokeCollection
            {
                new Stroke(
                [new StylusPoint(0, 0), new StylusPoint(1, 1)],
                new DrawingAttributes { Color = Colors.Black, Width = 1, Height = 1 })
            };

            var bitmap = strokes.Render(new DpiScale(96, 96), canvas: Colors.Red);

            AssertPixel(bitmap, 1, 1, Colors.Red);
        });
    }

    private static void AssertPixel(RenderTargetBitmap bitmap, int x, int y, Color expected)
    {
        var pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);

        // Pbgra32 的字节序是 B、G、R、A
        Assert.Equal(expected.B, pixel[0]);
        Assert.Equal(expected.G, pixel[1]);
        Assert.Equal(expected.R, pixel[2]);
        Assert.Equal(expected.A, pixel[3]);
    }
}
