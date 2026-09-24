using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;

using LightBoard;

namespace InkCanvasNext.Tests;

/// <summary>
/// 画布尺寸保障：挂载（打开 ISF/LBF）或追加的笔画越出画布右/下边界时扩展画布；
/// 文件可能产生自其他分辨率设备，画布按本机屏幕派生未必能容纳外来笔画。
/// </summary>
public class CanvasFitTests
{
    private static Stroke StrokeAt(double x1, double y1, double x2, double y2)
    {
        return new Stroke(
        [
            new StylusPoint(x1, y1),
            new StylusPoint(x2, y2),
        ]);
    }

    [Fact]
    public void Mount_StrokesBeyondCanvas_ExtendsCanvas( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            // 模拟打开外来文件：挂载右边界外 800px 处有笔画的集合（OnPageChanged 路径）
            host.Canvas.Strokes =
            [
                StrokeAt(App.CanvasSize.Width + 500, 100, App.CanvasSize.Width + 800, 300),
            ];

            Assert.True(host.Canvas.InnerCanvas.Width >= App.CanvasSize.Width + 800);
            Assert.Equal(App.CanvasSize.Height, host.Canvas.InnerCanvas.Height, 5);
        });
    }

    [Fact]
    public void EnsureStrokesFit_StrokesWithinCanvas_KeepsCanvasSize( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Strokes.Add(StrokeAt(100, 100, 400, 300));

            host.Canvas.EnsureStrokesFit( );

            Assert.Equal(App.CanvasSize.Width, host.Canvas.InnerCanvas.Width, 5);
            Assert.Equal(App.CanvasSize.Height, host.Canvas.InnerCanvas.Height, 5);
        });
    }

    [Fact]
    public void EnsureStrokesFit_EmptyCanvas_KeepsCanvasSize( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            host.Canvas.EnsureStrokesFit( );

            Assert.Equal(App.CanvasSize.Width, host.Canvas.InnerCanvas.Width, 5);
            Assert.Equal(App.CanvasSize.Height, host.Canvas.InnerCanvas.Height, 5);
        });
    }
}
