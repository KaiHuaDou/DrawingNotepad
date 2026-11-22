using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using LightBoard;

namespace InkCanvasNext.Tests;

/// <summary>多指/选区共用的自绘层与干墨在同一棵视觉树里的可见性：整树渲染后两种颜色都必须出现</summary>
public class StrokeVisualRenderTests
{
    [Fact]
    public void StrokeVisual_RendersLikeDryInk( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost(500, 400);

            // 内容坐标加上视口偏移，让笔迹落在窗口可见区域内
            var offset = App.InitialOffset;
            var dry = LinePoints(offset.X + 60, offset.Y + 60, offset.X + 260, offset.Y + 60);
            host.Canvas.Strokes.Add(new Stroke(dry, new DrawingAttributes
            {
                Width = 12,
                Height = 12,
                FitToCurve = false,
                Color = Colors.Blue
            }));

            var layer = new VisualCanvas( );
            host.Canvas.CanvasGrid.Children.Add(layer);

            var visual = new StrokeVisual(new DrawingAttributes
            {
                Width = 12,
                Height = 12,
                FitToCurve = false,
                Color = Colors.Red
            }, new StylusPoint(offset.X + 60, offset.Y + 140), null)
            {
                VisualCanvas = layer
            };
            visual.Add(new StylusPoint(offset.X + 260, offset.Y + 140));
            visual.Redraw( );

            host.Canvas.UpdateLayout( );
            var bitmap = new RenderTargetBitmap(500, 400, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(host.Canvas);

            Assert.True(ContainsColor(bitmap, Colors.Blue), "干墨不可见");
            Assert.True(ContainsColor(bitmap, Colors.Red), "自绘笔画不可见");

            visual.Cleanup( );
            host.Canvas.CanvasGrid.Children.Remove(layer);
        });
    }

    private static StylusPointCollection LinePoints(double x0, double y0, double x1, double y1)
    {
        return
        [
            with(2),
            new StylusPoint(x0, y0),
            new StylusPoint(x1, y1)
        ];
    }

    private static bool ContainsColor(RenderTargetBitmap bitmap, Color expected)
    {
        var stride = bitmap.PixelWidth * 4;
        var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);

        for (var i = 0; i + 3 < pixels.Length; i += 4)
        {
            // Pbgra32 的字节序是 B、G、R、A
            if (pixels[i] == expected.B && pixels[i + 1] == expected.G && pixels[i + 2] == expected.R && pixels[i + 3] > 0)
            {
                return true;
            }
        }

        return false;
    }
}
