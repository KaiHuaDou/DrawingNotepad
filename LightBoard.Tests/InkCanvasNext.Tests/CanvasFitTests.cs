using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;

using LightBoard;

namespace InkCanvasNext.Tests;

/// <summary>
/// 画布尺寸保障：新增笔画、挂载（打开 ISF/LBF）或选区变换把内容推到画布四边之外时扩展画布；
/// 左/上扩展同时平移内容，文件可能产生自其他分辨率设备
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
    public void Strokes_AddedBeyondLeftTop_ShiftsContentAndKeepsView( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;
            var viewBefore = canvas.CurrentView;
            var sizeBefore = canvas.CanvasSize;
            var shifts = new List<Vector>( );
            StrokeCollection? shifted = null;
            canvas.ContentShifted += (_, e) =>
            {
                shifts.Add(e.Offset);
                shifted = e.Strokes;
            };

            var stroke = StrokeAt(-300, -200, -100, -50);
            var raw = stroke.GetBounds( );
            canvas.Strokes.Add(stroke);
            canvas.UpdateLayout( );

            Assert.Single(shifts);
            var shift = shifts[0];
            Assert.Equal(-raw.Left, shift.X, 6);
            Assert.Equal(-raw.Top, shift.Y, 6);
            Assert.Same(canvas.Strokes, shifted);

            var bounds = stroke.GetBounds( );
            Assert.Equal(0, bounds.Left, 6);
            Assert.Equal(0, bounds.Top, 6);
            Assert.Equal(sizeBefore.Width + shift.X, canvas.CanvasSize.Width, 6);
            Assert.Equal(sizeBefore.Height + shift.Y, canvas.CanvasSize.Height, 6);

            // 屏幕上的内容位置不变：偏移量与内容平移量同步
            var viewAfter = canvas.CurrentView;
            Assert.Equal(viewBefore.OffsetX + shift.X * viewBefore.Scale, viewAfter.OffsetX, 6);
            Assert.Equal(viewBefore.OffsetY + shift.Y * viewBefore.Scale, viewAfter.OffsetY, 6);
        });
    }

    [Fact]
    public void Strokes_AddedBeyondLeftTopDuringGesture_ShiftsAfterRelease( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;
            var sizeBefore = canvas.CanvasSize;
            var shifts = 0;
            canvas.ContentShifted += (_, _) => shifts++;

            var finger = host.Device( );
            finger.Down(host.Target, new Point(100, 100));
            Assert.NotEqual(TouchState.Idle, canvas.State);

            var first = StrokeAt(-300, -200, -100, -50);
            var second = StrokeAt(-500, -400, -450, -350);
            canvas.Strokes.Add(first);
            canvas.Strokes.Add(second);
            Assert.Equal(sizeBefore.Width, canvas.CanvasSize.Width, 6);
            Assert.Equal(sizeBefore.Height, canvas.CanvasSize.Height, 6);
            Assert.Equal(0, shifts);

            finger.Up(host.Target, new Point(100, 100));
            Assert.Equal(TouchState.Idle, canvas.State);
            Assert.True(canvas.CanvasSize.Width > sizeBefore.Width);
            Assert.True(canvas.CanvasSize.Height > sizeBefore.Height);

            // 手势中的多次越界合并成一次平移，平移量取最远的那条笔画
            Assert.Equal(1, shifts);
            Assert.True(first.GetBounds( ).Left >= 0);
            Assert.True(first.GetBounds( ).Top >= 0);
            Assert.Equal(0, second.GetBounds( ).Left, 6);
            Assert.Equal(0, second.GetBounds( ).Top, 6);
        });
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
                StrokeAt(CanvasViewport.CanvasSize.Width + 500, 100, CanvasViewport.CanvasSize.Width + 800, 300),
            ];

            Assert.True(host.Canvas.InnerCanvas.Width >= CanvasViewport.CanvasSize.Width + 800);
            Assert.Equal(CanvasViewport.CanvasSize.Height, host.Canvas.InnerCanvas.Height, 5);
        });
    }

    [Fact]
    public void Strokes_AddedBeyondCanvas_ExtendsCanvas( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var size = host.Canvas.CanvasSize;
            var shifts = 0;
            host.Canvas.ContentShifted += (_, _) => shifts++;

            var stroke = StrokeAt(size.Width + 300, size.Height + 200, size.Width + 600, size.Height + 500);
            host.Canvas.Strokes.Add(stroke);

            Assert.True(host.Canvas.CanvasSize.Width >= size.Width + 600);
            Assert.True(host.Canvas.CanvasSize.Height >= size.Height + 500);
            Assert.Equal(host.Canvas.InnerCanvas.Width, host.Canvas.CanvasSize.Width, 5);
            Assert.Equal(host.Canvas.InnerCanvas.Height, host.Canvas.CanvasSize.Height, 5);

            // 向右/下只扩尺寸，不触发内容平移
            Assert.Equal(0, shifts);
            Assert.Equal(size.Width + 300, stroke.StylusPoints[0].X, 6);
            Assert.Equal(size.Height + 200, stroke.StylusPoints[0].Y, 6);
        });
    }

    [Fact]
    public void SelectionTransform_MovedBeyondCanvas_ExtendsCanvas( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.DrawingAttributes = host.Canvas.DrawingAttributes with { Mode = InkCanvasNextMode.Select };
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnsureStrokesFit_WithinCanvas_KeepsCanvasSize(bool withStroke)
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            if (withStroke)
            {
                host.Canvas.Strokes.Add(StrokeAt(100, 100, 400, 300));
            }

            host.Canvas.EnsureStrokesFit( );

            Assert.Equal(CanvasViewport.CanvasSize.Width, host.Canvas.InnerCanvas.Width, 5);
            Assert.Equal(CanvasViewport.CanvasSize.Height, host.Canvas.InnerCanvas.Height, 5);
        });
    }
}
