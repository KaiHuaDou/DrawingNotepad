using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasNext.Tests;

/// <summary>只读模式：输入不改墨迹与选区，平移缩放仍可用。</summary>
public class ReadOnlyTests
{
    [Fact]
    public void SingleFinger_DoesNotDraw( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.IsReadOnly = true;
            var finger = host.Device( );

            finger.Down(host.Target, new Point(100, 100));
            finger.Move(host.Target, TestPoints.BeyondDisplacement(host, new Point(100, 100)));
            finger.Up(host.Target, TestPoints.BeyondDisplacement(host, new Point(100, 100)));

            Assert.Empty(host.Canvas.Strokes);
            Assert.Equal(TouchState.Idle, host.Canvas.State);
        });
    }

    [Fact]
    public void FiveFingers_PanInsteadOfPalmErase( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.IsReadOnly = true;
            var cluster = TestPoints.CloseCluster(host, new Point(100, 100), 5);

            foreach (var point in cluster)
            {
                host.Device( ).Down(host.Target, point);
            }

            Assert.Equal(TouchState.Pan, host.Canvas.State);
        });
    }

    [Fact]
    public void TwoFarFingers_DoNotEnterMultiDraw( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.IsReadOnly = true;
            var start = new Point(100, 100);

            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, start);
            b.Down(host.Target, TestPoints.FarFrom(host, start));
            a.Move(host.Target, new Point(start.X + 80, start.Y));
            b.Move(host.Target, TestPoints.FarFrom(host, start));

            Assert.Equal(TouchState.EvalDraw, host.Canvas.State);
            Assert.Empty(host.Canvas.Strokes);
        });
    }

    [Fact]
    public void SelectMode_DoesNotSelect( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Strokes.Add(new Stroke(
                [new StylusPoint(100, 100, 0.5f), new StylusPoint(200, 100, 0.5f)],
                new DrawingAttributes { Color = Colors.Black, Width = 3, Height = 3 }));
            host.Canvas.Mode = InkCanvasNextMode.Select;
            host.Canvas.IsReadOnly = true;

            var finger = host.Device( );
            finger.Down(host.Target, new Point(150, 100));
            finger.Up(host.Target, new Point(150, 100));

            Assert.False(host.Canvas.HasSelection);
        });
    }

    [Fact]
    public void ToggleDuringGesture_ResetsTouchState( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var finger = host.Device( );
            finger.Down(host.Target, new Point(100, 100));
            Assert.Equal(TouchState.EvalDraw, host.Canvas.State);

            host.Canvas.IsReadOnly = true;

            Assert.Equal(TouchState.Idle, host.Canvas.State);
        });
    }

    [Fact]
    public void ProgrammaticUndo_StillWorks( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Strokes.Add(new Stroke(
                [new StylusPoint(100, 100, 0.5f), new StylusPoint(200, 100, 0.5f)],
                new DrawingAttributes { Color = Colors.Black, Width = 3, Height = 3 }));
            host.Canvas.IsReadOnly = true;

            host.Canvas.Undo( );

            Assert.Empty(host.Canvas.Strokes);
        });
    }
}
