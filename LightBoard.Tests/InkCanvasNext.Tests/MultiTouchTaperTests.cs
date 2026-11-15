using System.Windows;

namespace InkCanvasNext.Tests;

/// <summary>
/// 多指笔画的提交末端随笔锋收细
/// </summary>
public class MultiTouchTaperTests
{
    private const int Moves = 40;

    private static readonly Point Start = new(400, 300);

    [Fact]
    public void CommittedStroke_HasTaperedTail( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var finger1 = host.Device( );
            var finger2 = host.Device( );

            finger1.Down(host.Target, Start);
            finger2.Down(host.Target, TestPoints.FarFrom(host, Start));
            Assert.Equal(TouchState.MultiDraw, host.Canvas.State);

            for (var i = 1; i < Moves; i++)
            {
                finger1.Move(host.Target, new Point(Start.X + i * 5, Start.Y));
            }

            finger1.Up(host.Target, new Point(Start.X + Moves * 5, Start.Y));
            finger2.Up(host.Target, TestPoints.FarFrom(host, Start));

            var stroke = host.Canvas.Strokes.Single(s => s.StylusPoints.Count > 8);
            var points = stroke.StylusPoints;
            Assert.Equal(0.5, (double) points[0].PressureFactor, 5);
            Assert.Equal(0, (double) points[^1].PressureFactor, 5);
        });
    }

    [Fact]
    public void HighlighterStroke_CommittedWithoutTaper( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var attributes = host.Canvas.DrawingAttributes.DrawingAttributes;
            attributes.IsHighlighter = true;
            attributes.IgnorePressure = true;
            host.Canvas.DrawingAttributes = host.Canvas.DrawingAttributes with { DrawingAttributes = attributes };
            var finger1 = host.Device( );
            var finger2 = host.Device( );

            finger1.Down(host.Target, Start);
            finger2.Down(host.Target, TestPoints.FarFrom(host, Start));

            for (var i = 1; i < Moves; i++)
            {
                finger1.Move(host.Target, new Point(Start.X + i * 5, Start.Y));
            }

            finger1.Up(host.Target, new Point(Start.X + Moves * 5, Start.Y));
            finger2.Up(host.Target, TestPoints.FarFrom(host, Start));

            var stroke = host.Canvas.Strokes.Single(s => s.StylusPoints.Count > 8);
            Assert.All(stroke.StylusPoints, p => Assert.Equal(0.5, (double) p.PressureFactor, 5));
        });
    }
}
