using System.Linq;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasNext.Tests;

/// <summary>
/// 擦除周期的历史记账：一次擦除作为一步撤销，周期内的外部变更按发生次序单独成项
/// </summary>
public class EraseBatchTests
{
    private static readonly Point Start = new(160, 300);

    [Fact]
    public void EraseBatch_ExternalClearDuringErase_UndoRestoresBoth( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.EraseArea;

            var target = TestPoints.BeyondDisplacement(host, Start);
            host.Canvas.Strokes.Add(HorizontalLine(300, target.X));
            host.Canvas.Strokes.Add(HorizontalLine(500, 250));

            var finger = BeginErase(host, target);
            var afterErase = host.Canvas.Strokes.Count;
            Assert.True(afterErase > 2);

            host.Canvas.Strokes.Clear( );
            Assert.Empty(host.Canvas.Strokes);

            finger.Up(host.Target, target);

            host.Canvas.Undo( );
            Assert.Equal(afterErase, host.Canvas.Strokes.Count);

            host.Canvas.Undo( );
            Assert.Equal(2, host.Canvas.Strokes.Count);
        });
    }

    [Fact]
    public void EraseBatch_UndoDuringErase_KeepsHistoryAligned( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.EraseArea;

            var target = TestPoints.BeyondDisplacement(host, Start);
            var original = HorizontalLine(300, target.X);
            host.Canvas.Strokes.Add(original);

            var finger = BeginErase(host, target);
            Assert.True(host.Canvas.Strokes.Count > 1);
            Assert.Equal(2, host.Canvas.Position);

            host.Canvas.Undo( );
            Assert.Same(original, Assert.Single(host.Canvas.Strokes));
            Assert.Equal(1, host.Canvas.Position);
            Assert.True(host.Canvas.CanRedo);

            host.Canvas.Redo( );
            Assert.True(host.Canvas.Strokes.Count > 1);
            Assert.Equal(2, host.Canvas.Position);

            finger.Up(host.Target, target);
        });
    }

    [Fact]
    public void EraseBatch_ContinueAfterUndo_TruncatesRedo( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.EraseArea;

            var target = TestPoints.BeyondDisplacement(host, Start);
            host.Canvas.Strokes.Add(HorizontalLine(300, target.X));

            var finger = BeginErase(host, target);
            host.Canvas.Undo( );
            Assert.True(host.Canvas.CanRedo);

            finger.Move(host.Target, new Point(target.X + host.Canvas.EraserDiameter / 2, target.Y));

            Assert.False(host.Canvas.CanRedo);
            Assert.Equal(2, host.Canvas.Position);

            finger.Up(host.Target, target);
        });
    }

    [Fact]
    public void EraseBatch_DeleteFragmentThenUndo_NoResurrection( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.EraseArea;

            var target = TestPoints.BeyondDisplacement(host, Start);
            var original = HorizontalLine(300, target.X);
            host.Canvas.Strokes.Add(original);

            var finger = BeginErase(host, target);
            var afterErase = host.Canvas.Strokes.Count;
            Assert.True(afterErase > 1);

            host.Canvas.Strokes.Remove(host.Canvas.Strokes.First( ));
            Assert.Equal(afterErase - 1, host.Canvas.Strokes.Count);
            Assert.Equal(3, host.Canvas.Position);

            finger.Up(host.Target, target);

            host.Canvas.Undo( );
            Assert.Equal(afterErase, host.Canvas.Strokes.Count);

            host.Canvas.Undo( );
            Assert.Same(original, Assert.Single(host.Canvas.Strokes));
        });
    }

    [Fact]
    public void EraseBatch_MultiStrokeOneCycle_OneUndoStep( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.EraseArea;

            var target = TestPoints.BeyondDisplacement(host, Start);
            host.Canvas.Strokes.Add(HorizontalLine(280, target.X));
            host.Canvas.Strokes.Add(HorizontalLine(300, target.X));
            host.Canvas.Strokes.Add(HorizontalLine(320, target.X));

            var finger = BeginErase(host, target);
            Assert.Equal(4, host.Canvas.Position);
            Assert.NotEqual(3, host.Canvas.Strokes.Count);

            host.Canvas.Undo( );

            Assert.Equal(3, host.Canvas.Strokes.Count);
            Assert.Equal(3, host.Canvas.Position);

            finger.Up(host.Target, target);
        });
    }

    [Fact]
    public void EraseBatch_NoHit_NoHistoryEntry( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.EraseArea;

            var target = TestPoints.BeyondDisplacement(host, Start);
            var finger = BeginErase(host, target);
            finger.Up(host.Target, target);

            Assert.False(host.Canvas.CanUndo);
            Assert.Equal(0, host.Canvas.Position);
        });
    }

    [Fact]
    public void ApplyStrokes_DuringErase_StopsErase( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.EraseArea;

            var target = TestPoints.BeyondDisplacement(host, Start);
            host.Canvas.Strokes.Add(HorizontalLine(300, target.X));

            var finger = BeginErase(host, target);
            Assert.Equal(TouchState.Draw, host.Canvas.State);

            var replacement = new StrokeCollection( );
            host.Canvas.Strokes = replacement;

            Assert.Equal(TouchState.Idle, host.Canvas.State);
            Assert.Equal(Visibility.Collapsed, host.Canvas.EraserFeedback.Visibility);
            Assert.False(host.Canvas.CanUndo);

            var fresh = HorizontalLine(300, target.X);
            replacement.Add(fresh);
            finger.Move(host.Target, new Point(target.X + host.Canvas.EraserDiameter / 2, target.Y));

            Assert.Same(fresh, Assert.Single(host.Canvas.Strokes));
        });
    }

    [Fact]
    public void SwapHistory_DuringErase_SealsBatch( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.EraseArea;

            var target = TestPoints.BeyondDisplacement(host, Start);
            host.Canvas.Strokes.Add(HorizontalLine(300, target.X));

            var finger = BeginErase(host, target);
            Assert.Equal(2, host.Canvas.Position);

            host.Canvas.SwapHistory(out var old, null);

            Assert.NotNull(old);
            Assert.Equal(2, old!.Changes.Count);
            Assert.False(host.Canvas.CanUndo);

            finger.Move(host.Target, new Point(target.X + host.Canvas.EraserDiameter / 2, target.Y));

            Assert.True(host.Canvas.CanUndo);
            Assert.Equal(1, host.Canvas.Position);

            finger.Up(host.Target, target);
        });
    }

    /// <summary>落指停在 EvalDraw，移到 target 进入 Draw 并按橡皮半径擦除</summary>
    private static MockTouchDevice BeginErase(TouchHost host, Point target)
    {
        var finger = host.Device( );
        finger.Down(host.Target, Start);
        finger.Move(host.Target, target);
        return finger;
    }

    /// <summary>过 centerX 的横线，两端各超出橡皮直径两倍，中段擦除后仍有两片</summary>
    private static Stroke HorizontalLine(double y, double centerX)
    {
        const double reach = EraserParameters.DiameterDefault * 2;
        var points = new StylusPointCollection( );
        for (var x = centerX - reach; x <= centerX + reach; x += 10)
        {
            points.Add(new StylusPoint(x, y, 0.5f));
        }

        return new Stroke(points, new DrawingAttributes { Color = Colors.Black, Width = 3, Height = 3 });
    }
}
