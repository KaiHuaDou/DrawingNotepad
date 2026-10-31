using System.Windows.Input;

namespace InkCanvasNext.Tests;

/// <summary>撤销历史容量与快照恢复。</summary>
public class HistoryTests
{
    [Fact]
    public void HistoryCapacityZero_DisablesUndo( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost(options: new( ) { MaxHistoryCount = 0 });
            host.Canvas.Strokes.Add(MakeStroke(100, 100, 200, 100));

            Assert.False(host.Canvas.CanUndo);
            Assert.False(host.Canvas.CanRedo);

            host.Canvas.Undo( );
            Assert.Single(host.Canvas.Strokes);

            host.Canvas.Strokes.Add(MakeStroke(100, 200, 200, 200));
            Assert.False(host.Canvas.CanUndo);
            Assert.Equal(2, host.Canvas.Strokes.Count);
        });
    }

    [Fact]
    public void HistoryCapacityNegative_TreatedAsZero( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost(options: new( ) { MaxHistoryCount = -3 });
            host.Canvas.Strokes.Add(MakeStroke(100, 100, 200, 100));

            Assert.False(host.Canvas.CanUndo);
            host.Canvas.Undo( );
            Assert.Single(host.Canvas.Strokes);
        });
    }

    [Fact]
    public void SwapHistory_SnapshotLargerThanCapacity_TruncatesHead( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost(options: new( ) { MaxHistoryCount = 2 });

            var snapshot = new HistorySnapshot(
                [new StrokeChanges([], []), new StrokeChanges([], []), new StrokeChanges([], [])],
                2);
            host.Canvas.SwapHistory(out _, snapshot);

            Assert.Equal(1, host.Canvas.Position);
            Assert.Equal(1, host.Canvas.RedoDepth);
            Assert.True(host.Canvas.CanUndo);
            Assert.True(host.Canvas.CanRedo);

            host.Canvas.Undo( );
            Assert.Equal(0, host.Canvas.Position);
            host.Canvas.Redo( );
            Assert.Equal(1, host.Canvas.Position);
        });
    }

    [Fact]
    public void SwapHistory_PositionBeyondCapacity_ClampsToEnd( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost(options: new( ) { MaxHistoryCount = 2 });

            host.Canvas.SwapHistory(out _, new HistorySnapshot([], 5));

            Assert.Equal(0, host.Canvas.Position);
            Assert.False(host.Canvas.CanUndo);
            host.Canvas.Undo( );
            Assert.False(host.Canvas.CanRedo);
        });
    }

    [Fact]
    public void SwapHistory_NullSnapshot_ClearsHistory( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Strokes.Add(MakeStroke(100, 100, 200, 100));
            Assert.True(host.Canvas.CanUndo);

            host.Canvas.SwapHistory(out var old, null);

            Assert.NotNull(old);
            Assert.False(host.Canvas.CanUndo);
            Assert.False(host.Canvas.CanRedo);
        });
    }

    private static System.Windows.Ink.Stroke MakeStroke(double x1, double y1, double x2, double y2)
    {
        return new System.Windows.Ink.Stroke(
            [new StylusPoint(x1, y1, 0.5f), new StylusPoint(x2, y2, 0.5f)],
            new System.Windows.Ink.DrawingAttributes
            {
                Color = System.Windows.Media.Colors.Black,
                Width = 3,
                Height = 3
            });
    }
}
