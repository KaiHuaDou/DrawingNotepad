using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;

using LightBoard;

namespace InkCanvasNext.Tests;

/// <summary>
/// 画布尺寸保障：新增笔画、挂载（打开 ISF/LBF）或选区变换把内容推到画布右/下边界外时扩展画布；
/// 文件可能产生自其他分辨率设备，画布按本机屏幕派生未必能容纳外来笔画
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
    public void Strokes_AddedBeyondCanvas_ExtendsCanvas( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var size = host.Canvas.CanvasSize;

            host.Canvas.Strokes.Add(StrokeAt(size.Width + 300, size.Height + 200, size.Width + 600, size.Height + 500));

            Assert.True(host.Canvas.CanvasSize.Width >= size.Width + 600);
            Assert.True(host.Canvas.CanvasSize.Height >= size.Height + 500);
            Assert.Equal(host.Canvas.InnerCanvas.Width, host.Canvas.CanvasSize.Width, 5);
            Assert.Equal(host.Canvas.InnerCanvas.Height, host.Canvas.CanvasSize.Height, 5);
        });
    }

    [Fact]
    public void SelectionTransform_MovedBeyondCanvas_ExtendsCanvas( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.Select;
            host.Canvas.Strokes.Add(StrokeAt(200, 150, 250, 350));

            var lasso = host.Device( );
            lasso.Down(host.Target, new Point(150, 100));
            lasso.Move(host.Target, new Point(300, 100));
            lasso.Move(host.Target, new Point(300, 400));
            lasso.Move(host.Target, new Point(150, 400));
            lasso.Move(host.Target, new Point(150, 100));
            lasso.Up(host.Target, new Point(150, 100));
            Assert.True(host.Canvas.HasSelection);

            var width = host.Canvas.CanvasSize.Width;

            var finger = host.Device( );
            finger.Down(host.Target, new Point(225, 250));
            finger.Move(host.Target, new Point(width + 400, 250));
            finger.Up(host.Target, new Point(width + 400, 250));

            Assert.True(host.Canvas.CanvasSize.Width >= width + 400);
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
