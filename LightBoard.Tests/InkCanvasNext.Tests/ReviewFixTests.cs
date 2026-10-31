using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasNext.Tests;

/// <summary>
/// 审查修复项的回归测试：历史容量、空笔触属性、历史快照、合成鼠标复位、落章扩展画布、卸载复位。
/// </summary>
public class ReviewFixTests
{
    [Fact]
    public void SetMaxHistoryCount_Zero_DisablesHistoryWithoutThrowing( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Strokes.Add(MakeStroke(100, 100, 200, 100));
            Assert.True(host.Canvas.CanUndo);

            host.Canvas.SetMaxHistoryCount(0);
            Assert.False(host.Canvas.CanUndo);

            host.Canvas.Strokes.Add(MakeStroke(100, 200, 200, 200));
            host.Canvas.Undo( );
            Assert.False(host.Canvas.CanUndo);
            Assert.Equal(2, host.Canvas.Strokes.Count);

            host.Canvas.SetMaxHistoryCount(-3);
            host.Canvas.Strokes.Add(MakeStroke(100, 300, 200, 300));
            Assert.False(host.Canvas.CanUndo);

            host.Canvas.SetMaxHistoryCount(2);
            host.Canvas.Strokes.Add(MakeStroke(100, 400, 200, 400));
            Assert.True(host.Canvas.CanUndo);
        });
    }

    [Fact]
    public void DefaultDrawingAttributes_NullAssignment_KeepsPreviousValue( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var attributes = host.Canvas.DefaultDrawingAttributes;

            host.Canvas.DefaultDrawingAttributes = null!;

            Assert.Same(attributes, host.Canvas.DefaultDrawingAttributes);
        });
    }

    [Fact]
    public void SwapHistory_ChangesBeyondCapacity_KeepsPointerOnSameEntry( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.SetMaxHistoryCount(2);

            var snapshot = new HistorySnapshot(
                [new StrokeChanges([], []), new StrokeChanges([], []), new StrokeChanges([], [])],
                2);
            host.Canvas.SwapHistory(out _, snapshot);

            // 头部丢一条后指针同步前移：已应用 1 条、可重做 1 条
            Assert.Equal(1, host.Canvas.Position);
            Assert.Equal(1, host.Canvas.RedoDepth);
            Assert.True(host.Canvas.CanUndo);

            host.Canvas.SwapHistory(out _, new HistorySnapshot([], 5));
            Assert.False(host.Canvas.CanUndo);
        });
    }

    [Fact]
    public void MouseSequence_ResetTouchState_AllowsNextDown( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.Line;
            var start = new Point(100, 100);

            host.Canvas.MousePointer.Down(start);
            Assert.Equal(TouchState.EvalDraw, host.Canvas.State);

            host.Canvas.ResetTouchState( );
            Assert.Equal(TouchState.Idle, host.Canvas.State);

            host.Canvas.MousePointer.Down(start);
            Assert.Equal(TouchState.EvalDraw, host.Canvas.State);
            host.Canvas.MousePointer.Up(start);
        });
    }

    [Fact]
    public void StampCloneNearRightEdge_ExtendsCanvas( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.Select;
            host.Canvas.Strokes.Add(MakeStroke(100, 100, 200, 100));
            LassoSelectAll(host);
            Assert.True(host.Canvas.HasSelection);

            var width = host.Canvas.InnerCanvasElement.Width;
            host.Canvas.StampCloneAt(new Point(width - 1, 100));

            Assert.True(host.Canvas.InnerCanvasElement.Width > width);
        });
    }

    [Fact]
    public void Unloaded_ResetsTouchState( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var finger = host.Device( );
            finger.Down(host.Target, new Point(100, 100));
            Assert.Equal(TouchState.EvalDraw, host.Canvas.State);

            host.Canvas.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));

            Assert.Equal(TouchState.Idle, host.Canvas.State);
        });
    }

    private static void LassoSelectAll(TouchHost host)
    {
        var lasso = host.Device( );
        lasso.Down(host.Target, new Point(50, 50));
        lasso.Move(host.Target, new Point(400, 50));
        lasso.Move(host.Target, new Point(400, 300));
        lasso.Move(host.Target, new Point(50, 300));
        lasso.Move(host.Target, new Point(50, 50));
        lasso.Up(host.Target, new Point(50, 50));
    }

    private static Stroke MakeStroke(double x1, double y1, double x2, double y2)
    {
        return new Stroke(
            [new StylusPoint(x1, y1, 0.5f), new StylusPoint(x2, y2, 0.5f)],
            new DrawingAttributes { Color = Colors.Black, Width = 3, Height = 3 });
    }
}
