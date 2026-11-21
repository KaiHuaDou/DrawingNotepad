using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasNext.Tests;

/// <summary>
/// 手写笔路径：自定义工具经 PenPointer 合成触点进入触摸状态机，
/// 墨迹与按笔擦保持原生路由；反转笔的编辑模式随工具切换
/// </summary>
public class PenMachineTests
{
    private static readonly Point P1 = new(100, 100);

    [Fact]
    public void UsesPenTouch_RoutesByTool( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            host.Mode = InkCanvasNextMode.Ink;
            Assert.False(host.Canvas.UsesPenTouch(false));

            host.Mode = InkCanvasNextMode.EraseStroke;
            Assert.False(host.Canvas.UsesPenTouch(false));

            host.Mode = InkCanvasNextMode.Line;
            Assert.True(host.Canvas.UsesPenTouch(false));
            Assert.False(host.Canvas.UsesPenTouch(true));

            host.Mode = InkCanvasNextMode.EraseArea;
            Assert.True(host.Canvas.UsesPenTouch(false));
            Assert.True(host.Canvas.UsesPenTouch(true));
        });
    }

    [Fact]
    public void PenShapeLine_CommitsStrokeAtUp( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Mode = InkCanvasNextMode.Line;
            var pointer = host.Canvas.PenPointer;
            var start = RootOf(host, P1);
            var moved = TestPoints.BeyondDisplacement(host, start);

            pointer.Down(start);
            pointer.Move(moved);
            Assert.Equal(TouchState.Draw, host.Canvas.State);
            pointer.Up(moved);

            Assert.Equal(TouchState.Idle, host.Canvas.State);
            Assert.Single(host.Canvas.Strokes);
        });
    }

    [Fact]
    public void PenShapeCircle_CommitsStrokeAtUp( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Mode = InkCanvasNextMode.Circle;
            var pointer = host.Canvas.PenPointer;
            var start = RootOf(host, P1);
            var moved = TestPoints.BeyondDisplacement(host, start);

            pointer.Down(start);
            pointer.Move(moved);
            Assert.Equal(TouchState.Draw, host.Canvas.State);
            pointer.Up(moved);

            Assert.Equal(TouchState.Idle, host.Canvas.State);
            Assert.Single(host.Canvas.Strokes);
        });
    }

    [Fact]
    public void PenAreaErase_ClickErases( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Mode = InkCanvasNextMode.EraseArea;
            host.Canvas.Strokes.Add(MakeStroke(new Point(170, 100), new Point(190, 100)));
            var pointer = host.Canvas.PenPointer;
            var downAt = RootOf(host, new Point(155, 100));

            pointer.Down(downAt);
            Assert.Equal(TouchState.EvalDraw, host.Canvas.State);
            Assert.Empty(host.Canvas.Strokes);

            pointer.Up(downAt);
            Assert.Equal(TouchState.Idle, host.Canvas.State);
            Assert.Equal(Visibility.Collapsed, host.Canvas.EraserFeedback.Visibility);
        });
    }

    [Fact]
    public void PenSelect_LassoSelects( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Mode = InkCanvasNextMode.Select;
            host.Canvas.Strokes.Add(MakeStroke(new Point(200, 150), new Point(250, 350)));
            var pointer = host.Canvas.PenPointer;

            pointer.Down(RootOf(host, new Point(150, 100)));
            pointer.Move(RootOf(host, new Point(300, 100)));
            pointer.Move(RootOf(host, new Point(300, 400)));
            pointer.Move(RootOf(host, new Point(150, 400)));
            pointer.Move(RootOf(host, new Point(150, 100)));
            pointer.Up(RootOf(host, new Point(150, 100)));

            Assert.Equal(TouchState.Idle, host.Canvas.State);
            Assert.True(host.Canvas.HasSelection);
        });
    }

    [Fact]
    public void PenSelect_DragMovesSelection( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Mode = InkCanvasNextMode.Select;
            host.Canvas.Strokes.Add(MakeStroke(new Point(200, 150), new Point(250, 350)));
            var pointer = host.Canvas.PenPointer;

            LassoSelectStroke(host, pointer);
            var before = host.Canvas.Strokes[0].GetBounds( );

            pointer.Down(RootOf(host, new Point(220, 250)));
            pointer.Move(RootOf(host, new Point(270, 280)));
            pointer.Up(RootOf(host, new Point(270, 280)));

            var after = host.Canvas.Strokes[0].GetBounds( );
            Assert.Equal(TouchState.Idle, host.Canvas.State);
            Assert.Equal(before.X + 50, after.X, 2);
            Assert.Equal(before.Y + 30, after.Y, 2);
        });
    }

    [Fact]
    public void PenStamp_CommitsAtUp( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Mode = InkCanvasNextMode.Select;
            host.Canvas.Strokes.Add(MakeStroke(new Point(200, 150), new Point(250, 350)));
            var pointer = host.Canvas.PenPointer;

            LassoSelectStroke(host, pointer);
            host.Canvas.DrawingAttributes = host.Canvas.DrawingAttributes with { Stamp = StampAction.Clone };

            var downAt = RootOf(host, P1);
            pointer.Down(downAt);
            Assert.Equal(TouchState.EvalDraw, host.Canvas.State);
            Assert.Single(host.Canvas.Strokes);

            pointer.Up(TestPoints.CloseTo(host, downAt));

            Assert.Equal(TouchState.Idle, host.Canvas.State);
            Assert.Equal(2, host.Canvas.Strokes.Count);
        });
    }

    [Fact]
    public void InvertedEditingMode_FollowsTool( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            host.Mode = InkCanvasNextMode.Ink;
            Assert.Equal(InkCanvasEditingMode.EraseByStroke, host.Canvas.InnerCanvas.EditingModeInverted);

            host.Mode = InkCanvasNextMode.EraseArea;
            Assert.Equal(InkCanvasEditingMode.None, host.Canvas.InnerCanvas.EditingModeInverted);

            host.Canvas.IsReadOnly = true;
            Assert.Equal(InkCanvasEditingMode.None, host.Canvas.InnerCanvas.EditingModeInverted);

            host.Canvas.IsReadOnly = false;
            host.Mode = InkCanvasNextMode.Ink;
            Assert.Equal(InkCanvasEditingMode.EraseByStroke, host.Canvas.InnerCanvas.EditingModeInverted);
        });
    }

    [Fact]
    public void ReadOnly_PenDoesNotUseStateMachine( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.IsReadOnly = true;
            host.Mode = InkCanvasNextMode.Line;

            Assert.False(host.Canvas.UsesPenTouch(false));
            Assert.False(host.Canvas.UsesPenTouch(true));
        });
    }

    /// <summary>用单指套索选中一条经过 (150,100)-(360,400) 区域的笔画</summary>
    private static void LassoSelectStroke(TouchHost host, SyntheticTouchDevice pointer)
    {
        pointer.Down(RootOf(host, new Point(150, 100)));
        pointer.Move(RootOf(host, new Point(300, 100)));
        pointer.Move(RootOf(host, new Point(300, 400)));
        pointer.Move(RootOf(host, new Point(150, 400)));
        pointer.Move(RootOf(host, new Point(150, 100)));
        pointer.Up(RootOf(host, new Point(150, 100)));

        Assert.Equal(TouchState.Idle, host.Canvas.State);
        Assert.True(host.Canvas.HasSelection);
    }

    /// <summary>合成触点按布局换算坐标：把画布坐标反推为控件根坐标，供设备输入使用</summary>
    private static Point RootOf(TouchHost host, Point canvasPoint)
    {
        return host.Canvas.InnerCanvas.TranslatePoint(canvasPoint, host.Canvas);
    }

    private static Stroke MakeStroke(params Point[] points)
    {
        return new Stroke(
            [.. points.Select(p => new StylusPoint(p.X, p.Y, 0.5f))],
            new DrawingAttributes { Color = Colors.Black, Width = 3, Height = 3 });
    }
}
