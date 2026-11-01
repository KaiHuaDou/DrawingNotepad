using System.Diagnostics;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Threading;

namespace InkCanvasNext.Tests;

/// <summary>
/// 边缘墨迹滚动提示：热区内有墨迹才显示该侧提示，点按后平移到该侧热区无墨迹
/// 坐标一律由控件的当前视图与阈值推导，宿主尺寸变化时测试自动跟随
/// </summary>
public class EdgeHintTests
{
    [Fact]
    public void InkInMiddle_HidesAllHints( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;
            var view = new View(canvas.CurrentScale, canvas.OffsetX, canvas.OffsetY);

            AddStroke(host, view.ToContent(new Point(canvas.ViewportSize.Width / 2, canvas.ViewportSize.Height / 2)));

            AssertAllHidden(host);
        });
    }

    [Fact]
    public void InkInBottomBand_ShowsBottomHintOnly( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            AddStroke(host, BandCenter(host, EdgeScrollSide.Bottom));

            Assert.Equal([false, true, false, false], VisibleSides(host));
        });
    }

    [Fact]
    public void InkInRightBand_ShowsRightHintOnly( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            AddStroke(host, BandCenter(host, EdgeScrollSide.Right));

            Assert.Equal([false, false, false, true], VisibleSides(host));
        });
    }

    [Fact]
    public void InkInTopBandAtCanvasTop_HidesTopHint( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.OffsetX = 0;
            host.Canvas.OffsetY = 0;
            host.Canvas.UpdateLayout( );

            AddStroke(host, BandCenter(host, EdgeScrollSide.Top));

            Assert.False(host.Canvas.IsEdgeHintVisible(EdgeScrollSide.Top));
        });
    }

    [Fact]
    public void InkInTopBandAfterScroll_ShowsTopHint( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.OffsetY = 400;
            host.Canvas.UpdateLayout( );

            AddStroke(host, BandCenter(host, EdgeScrollSide.Top));

            Assert.True(host.Canvas.IsEdgeHintVisible(EdgeScrollSide.Top));
        });
    }

    [Fact]
    public void ScrollEdgeClear_ScrollsUntilBandHasNoInk( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;
            AddStroke(host, BandCenter(host, EdgeScrollSide.Bottom));

            var before = canvas.OffsetY;

            canvas.ScrollEdgeClear(EdgeScrollSide.Bottom);
            PumpUntilIdle(canvas);

            Assert.True(canvas.OffsetY > before, $"偏移应增大：before={before} after={canvas.OffsetY}");
            Assert.False(canvas.IsEdgeHintVisible(EdgeScrollSide.Bottom));
            Assert.False(HasBandInk(host, EdgeScrollSide.Bottom));
        });
    }

    [Fact]
    public void ScrollEdgeClear_AtCanvasEnd_ExtendsCanvas( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;
            canvas.OffsetY = double.MaxValue;
            canvas.UpdateLayout( );
            var heightBefore = canvas.InnerCanvas.Height;

            AddStroke(host, BandCenter(host, EdgeScrollSide.Bottom));
            Assert.True(canvas.IsEdgeHintVisible(EdgeScrollSide.Bottom));

            canvas.ScrollEdgeClear(EdgeScrollSide.Bottom);
            PumpUntilIdle(canvas);

            Assert.True(canvas.InnerCanvas.Height > heightBefore, $"画布应扩展：before={heightBefore} after={canvas.InnerCanvas.Height}");
            Assert.False(HasBandInk(host, EdgeScrollSide.Bottom));
        });
    }

    [Fact]
    public void ThresholdZero_HidesAllHints( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost(options: new( )
            {
                EdgeScrollTopThreshold = 0,
                EdgeScrollBottomThreshold = 0,
                EdgeScrollLeftThreshold = 0,
                EdgeScrollRightThreshold = 0
            });
            var canvas = host.Canvas;
            var view = new View(canvas.CurrentScale, canvas.OffsetX, canvas.OffsetY);
            AddStroke(host, view.ToContent(new Point(
                canvas.ViewportSize.Width / 2,
                canvas.ViewportSize.Height / 2)));

            AssertAllHidden(host);
        });
    }

    [Fact]
    public void TopThresholdZero_HidesOnlyTopHint( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost(options: new( ) { EdgeScrollTopThreshold = 0 });
            var canvas = host.Canvas;
            var view = new View(canvas.CurrentScale, canvas.OffsetX, canvas.OffsetY);
            AddStroke(host, view.ToContent(new Point(canvas.ViewportSize.Width / 2, 10)));
            AddStroke(host, BandCenter(host, EdgeScrollSide.Bottom));

            Assert.Equal([false, true, false, false], VisibleSides(host));
        });
    }

    [Fact]
    public void ClearStrokes_HidesAllHints( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            AddStroke(host, BandCenter(host, EdgeScrollSide.Bottom));
            Assert.True(host.Canvas.IsEdgeHintVisible(EdgeScrollSide.Bottom));

            host.Canvas.Strokes.Clear( );

            AssertAllHidden(host);
        });
    }

    [Fact]
    public void EdgeHintsChanged_FiresOnlyWhenStateChanges( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var raised = 0;
            host.Canvas.EdgeHintsChanged += (_, _) => raised++;

            AddStroke(host, BandCenter(host, EdgeScrollSide.Bottom));

            Assert.Equal(1, raised);

            AddStroke(host, BandCenter(host, EdgeScrollSide.Bottom));

            Assert.Equal(1, raised);
        });
    }

    private static bool[] VisibleSides(TouchHost host)
    {
        return
        [
            host.Canvas.IsEdgeHintVisible(EdgeScrollSide.Top),
            host.Canvas.IsEdgeHintVisible(EdgeScrollSide.Bottom),
            host.Canvas.IsEdgeHintVisible(EdgeScrollSide.Left),
            host.Canvas.IsEdgeHintVisible(EdgeScrollSide.Right)
        ];
    }

    private static void AssertAllHidden(TouchHost host)
    {
        Assert.Equal([false, false, false, false], VisibleSides(host));
    }

    /// <summary>热区中心的内容坐标</summary>
    private static Point BandCenter(TouchHost host, EdgeScrollSide side)
    {
        var canvas = host.Canvas;
        var view = new View(canvas.CurrentScale, canvas.OffsetX, canvas.OffsetY);
        var threshold = side switch
        {
            EdgeScrollSide.Top => canvas.options.EdgeScrollTopThreshold,
            EdgeScrollSide.Bottom => canvas.options.EdgeScrollBottomThreshold,
            EdgeScrollSide.Left => canvas.options.EdgeScrollLeftThreshold,
            _ => canvas.options.EdgeScrollRightThreshold,
        };
        var band = InkCanvasNext.EdgeBandContentRect(view, canvas.ViewportSize, threshold, side);

        return new Point(band.Left + band.Width / 2, band.Top + band.Height / 2);
    }

    private static void AddStroke(TouchHost host, Point center)
    {
        host.Canvas.Strokes.Add(new Stroke(
        [
            new StylusPoint(center.X - 20, center.Y),
            new StylusPoint(center.X + 20, center.Y),
        ]));
    }

    private static bool HasBandInk(TouchHost host, EdgeScrollSide side)
    {
        var canvas = host.Canvas;
        var view = new View(canvas.CurrentScale, canvas.OffsetX, canvas.OffsetY);
        var threshold = side switch
        {
            EdgeScrollSide.Top => canvas.options.EdgeScrollTopThreshold,
            EdgeScrollSide.Bottom => canvas.options.EdgeScrollBottomThreshold,
            EdgeScrollSide.Left => canvas.options.EdgeScrollLeftThreshold,
            _ => canvas.options.EdgeScrollRightThreshold,
        };
        var band = InkCanvasNext.EdgeBandContentRect(view, canvas.ViewportSize, threshold, side);

        return InkCanvasNext.BandHasInk([.. canvas.Strokes.Select(s => s.GetBounds( ))], band);
    }

    private static void PumpUntilIdle(InkCanvasNext canvas)
    {
        var clock = Stopwatch.StartNew( );
        while (canvas.IsEdgeScrolling && clock.ElapsedMilliseconds < 3000)
        {
            Dispatcher.CurrentDispatcher.Invoke(( ) => { }, DispatcherPriority.Background);
            Thread.Sleep(10);
        }

        canvas.UpdateLayout( );
    }
}
