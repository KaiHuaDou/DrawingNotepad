using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Ink;
using System.Windows.Shapes;
using System.Windows.Input;
using System.Windows.Threading;

namespace InkCanvasNext.Tests;

/// <summary>
/// 边缘墨迹滚动提示：热区内有墨迹才显示该侧按钮，点击后平移到该侧热区无墨迹。
/// 坐标一律由控件的当前视图与阈值推导，宿主尺寸变化时测试自动跟随。
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

    [Theory]
    [InlineData("TopEdgeHint", 16, 10)]
    [InlineData("BottomEdgeHint", 16, 10)]
    [InlineData("LeftEdgeHint", 10, 16)]
    [InlineData("RightEdgeHint", 10, 16)]
    public void HintChevron_FillsItsBox_SoItIsCentered(string name, double width, double height)
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var path = (Path) Hint(host, name).Content;

            path.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            // Shape 的自然尺寸取笔迹外沿：等于箭头笔迹尺寸时，ContentPresenter 居中才准确
            // 几何求交的浮点误差在 1e-5 量级，留 3 位小数
            Assert.Equal(width, path.DesiredSize.Width, 3);
            Assert.Equal(height, path.DesiredSize.Height, 3);
        });
    }

    [Fact]
    public void InkInBottomBand_ShowsBottomHintAtEdge( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            AddStroke(host, BandCenter(host, EdgeScrollSide.Bottom));

            var button = Hint(host, "BottomEdgeHint");
            Assert.Equal(Visibility.Visible, button.Visibility);
            Assert.Equal(Visibility.Collapsed, Hint(host, "TopEdgeHint").Visibility);
            Assert.Equal(Visibility.Collapsed, Hint(host, "LeftEdgeHint").Visibility);
            Assert.Equal(Visibility.Collapsed, Hint(host, "RightEdgeHint").Visibility);

            var canvas = host.Canvas;
            var view = new View(canvas.CurrentScale, canvas.OffsetX, canvas.OffsetY);
            var band = InkCanvasNext.EdgeBandContentRect(
                view, canvas.ViewportSize, canvas.EdgeScrollBottomThreshold, EdgeScrollSide.Bottom);
            var bandTop = band.Top * view.Scale - view.OffsetY;

            Assert.Equal(canvas.ViewportSize.Height - canvas.EdgeHintMargin.Bottom - button.Height, Canvas.GetTop(button), 6);
            Assert.Equal((canvas.ViewportSize.Width - button.Width) / 2, Canvas.GetLeft(button), 6);
            Assert.True(Canvas.GetTop(button) >= bandTop, $"按钮应落在底部热区内：top={Canvas.GetTop(button)} bandTop={bandTop}");
        });
    }

    [Fact]
    public void CustomMargin_MovesHintAboveReservedArea( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;
            const double clearance = 68;

            canvas.EdgeHintMargin = new Thickness(8, 8, 8, clearance);
            AddStroke(host, BandCenter(host, EdgeScrollSide.Bottom));

            var button = Hint(host, "BottomEdgeHint");
            Assert.Equal(Visibility.Visible, button.Visibility);
            Assert.Equal(canvas.ViewportSize.Height - clearance - button.Height, Canvas.GetTop(button), 6);
        });
    }

    [Fact]
    public void ClickBottomHint_ScrollsUntilBandHasNoInk( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;
            AddStroke(host, BandCenter(host, EdgeScrollSide.Bottom));

            var button = Hint(host, "BottomEdgeHint");
            var before = canvas.OffsetY;

            Click(button);
            PumpUntilIdle(canvas);

            Assert.True(canvas.OffsetY > before, $"偏移应增大：before={before} after={canvas.OffsetY}");
            Assert.Equal(Visibility.Collapsed, button.Visibility);
            Assert.False(HasBandInk(host, EdgeScrollSide.Bottom));
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

            Assert.Equal(Visibility.Collapsed, Hint(host, "TopEdgeHint").Visibility);
        });
    }

    [Fact]
    public void InkInBottomBandAtCanvasEnd_ClickExtendsCanvas( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;
            canvas.OffsetY = double.MaxValue;
            canvas.UpdateLayout( );
            var heightBefore = canvas.InnerCanvas.Height;

            AddStroke(host, BandCenter(host, EdgeScrollSide.Bottom));
            var button = Hint(host, "BottomEdgeHint");
            Assert.Equal(Visibility.Visible, button.Visibility);

            Click(button);
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
            using var host = new TouchHost( );
            AddStroke(host, BandCenter(host, EdgeScrollSide.Bottom));
            Assert.Equal(Visibility.Visible, Hint(host, "BottomEdgeHint").Visibility);

            host.Canvas.EdgeScrollTopThreshold = 0;
            host.Canvas.EdgeScrollBottomThreshold = 0;
            host.Canvas.EdgeScrollLeftThreshold = 0;
            host.Canvas.EdgeScrollRightThreshold = 0;

            AssertAllHidden(host);
        });
    }

    [Fact]
    public void TopThresholdZero_HidesOnlyTopHint( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var top = BandCenter(host, EdgeScrollSide.Top);

            host.Canvas.EdgeScrollTopThreshold = 0;
            AddStroke(host, top);

            Assert.Equal(Visibility.Collapsed, Hint(host, "TopEdgeHint").Visibility);
        });
    }

    [Fact]
    public void ClearStrokes_HidesAllHints( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            AddStroke(host, BandCenter(host, EdgeScrollSide.Bottom));
            Assert.Equal(Visibility.Visible, Hint(host, "BottomEdgeHint").Visibility);

            host.Canvas.Strokes.Clear( );

            AssertAllHidden(host);
        });
    }

    private static Button Hint(TouchHost host, string name)
    {
        return (Button) host.Canvas.FindName(name);
    }

    private static void AssertAllHidden(TouchHost host)
    {
        Assert.Equal(Visibility.Collapsed, Hint(host, "TopEdgeHint").Visibility);
        Assert.Equal(Visibility.Collapsed, Hint(host, "BottomEdgeHint").Visibility);
        Assert.Equal(Visibility.Collapsed, Hint(host, "LeftEdgeHint").Visibility);
        Assert.Equal(Visibility.Collapsed, Hint(host, "RightEdgeHint").Visibility);
    }

    /// <summary>热区中心的内容坐标。</summary>
    private static Point BandCenter(TouchHost host, EdgeScrollSide side)
    {
        var canvas = host.Canvas;
        var view = new View(canvas.CurrentScale, canvas.OffsetX, canvas.OffsetY);
        var threshold = side switch
        {
            EdgeScrollSide.Top => canvas.EdgeScrollTopThreshold,
            EdgeScrollSide.Bottom => canvas.EdgeScrollBottomThreshold,
            EdgeScrollSide.Left => canvas.EdgeScrollLeftThreshold,
            _ => canvas.EdgeScrollRightThreshold,
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
            EdgeScrollSide.Top => canvas.EdgeScrollTopThreshold,
            EdgeScrollSide.Bottom => canvas.EdgeScrollBottomThreshold,
            EdgeScrollSide.Left => canvas.EdgeScrollLeftThreshold,
            _ => canvas.EdgeScrollRightThreshold,
        };
        var band = InkCanvasNext.EdgeBandContentRect(view, canvas.ViewportSize, threshold, side);

        return InkCanvasNext.BandHasInk([.. canvas.Strokes.Select(s => s.GetBounds( ))], band);
    }

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
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
