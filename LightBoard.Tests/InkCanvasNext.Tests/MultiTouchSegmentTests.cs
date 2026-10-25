using System.Windows;

namespace InkCanvasNext.Tests;

// 多指笔画按窗口分段缓存几何：活动段每帧重建，冻结段各占一个绘制视觉。
// 分段只影响绘制路径，提交的 Stroke 必须保留全部触点。
public class MultiTouchSegmentTests
{
    private const int Moves = 200;

    private static readonly Point P1 = new(400, 300);

    [Fact]
    public void LongStroke_AddsFrozenSegments_AndKeepsAllPoints( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var finger1 = host.Device( );
            var finger2 = host.Device( );

            finger1.Down(host.Target, P1);
            finger2.Down(host.Target, TestPoints.FarFrom(host, P1));
            Assert.Equal(TouchState.MultiDraw, host.Canvas.State);

            finger1.Move(host.Target, new Point(P1.X, P1.Y));
            var initial = host.Canvas.MultiTouchVisualCount;

            for (var i = 1; i < Moves; i++)
            {
                finger1.Move(host.Target, new Point(P1.X + i * 0.5, P1.Y));
            }

            // 200 点超过一个分段窗口，冻结段逐个挂上
            Assert.True(host.Canvas.MultiTouchVisualCount >= initial + 2);

            finger1.Up(host.Target, new Point(P1.X + Moves * 0.5, P1.Y));
            finger2.Up(host.Target, TestPoints.FarFrom(host, P1));

            Assert.Equal(Moves + 1, host.Canvas.Strokes[0].StylusPoints.Count);
            Assert.Equal(0, host.Canvas.MultiTouchVisualCount);
        });
    }

    [Fact]
    public void HighlighterStroke_KeepsSingleActiveVisual( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.DefaultDrawingAttributes.IsHighlighter = true;
            var finger1 = host.Device( );
            var finger2 = host.Device( );

            finger1.Down(host.Target, P1);
            finger2.Down(host.Target, TestPoints.FarFrom(host, P1));
            Assert.Equal(TouchState.MultiDraw, host.Canvas.State);

            finger1.Move(host.Target, new Point(P1.X, P1.Y));
            var initial = host.Canvas.MultiTouchVisualCount;

            for (var i = 1; i < Moves; i++)
            {
                finger1.Move(host.Target, new Point(P1.X + i * 0.5, P1.Y));
            }

            // 半透明笔迹不能拆段：段间共享的接头点会叠色
            Assert.Equal(initial, host.Canvas.MultiTouchVisualCount);

            finger1.Up(host.Target, new Point(P1.X + Moves * 0.5, P1.Y));
            finger2.Up(host.Target, TestPoints.FarFrom(host, P1));

            Assert.Equal(Moves + 1, host.Canvas.Strokes[0].StylusPoints.Count);
        });
    }
}
