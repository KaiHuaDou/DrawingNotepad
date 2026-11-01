using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Threading;

using InkCanvasNext;

namespace LightBoard.Tests.Toolbar;

/// <summary>窗口负责绘制的边缘提示：按钮跟随控件的显示状态，点击后滚动画布</summary>
[Collection("Toolbar")]
public class EdgeHintTests
{
    [Fact]
    public void HintButtons_FollowCanvasState( )
    {
        UiThread.Run(( ) =>
        {
            using var host = CreateShownHost( );
            var canvas = host.Canvas;

            canvas.Strokes.Add(BandStroke(host, EdgeScrollSide.Bottom));
            host.Window.UpdateLayout( );

            Assert.Equal(Visibility.Visible, host.Window.BottomEdgeHint.Visibility);
            Assert.Equal(Visibility.Collapsed, host.Window.TopEdgeHint.Visibility);
            Assert.Equal(Visibility.Collapsed, host.Window.LeftEdgeHint.Visibility);
            Assert.Equal(Visibility.Collapsed, host.Window.RightEdgeHint.Visibility);

            canvas.Strokes.Clear( );
            host.Window.UpdateLayout( );

            Assert.All(HintButtons(host.Window), b => Assert.Equal(Visibility.Collapsed, b.Visibility));
        });
    }

    [Fact]
    public void ClickBottomHint_ScrollsCanvas( )
    {
        UiThread.Run(( ) =>
        {
            using var host = CreateShownHost( );
            var canvas = host.Canvas;
            canvas.Strokes.Add(BandStroke(host, EdgeScrollSide.Bottom));
            host.Window.UpdateLayout( );

            var before = canvas.OffsetY;
            ToolDriver.Click(host.Window.BottomEdgeHint);
            PumpUntilIdle(canvas);
            host.Window.UpdateLayout( );

            Assert.True(canvas.OffsetY > before, $"偏移应增大：before={before} after={canvas.OffsetY}");
            Assert.False(canvas.IsEdgeHintVisible(EdgeScrollSide.Bottom));
            Assert.Equal(Visibility.Collapsed, host.Window.BottomEdgeHint.Visibility);
        });
    }

    private static MainWindowHost CreateShownHost( )
    {
        var host = new MainWindowHost( );
        host.Window.WindowState = WindowState.Normal;
        host.Window.ShowActivated = false;
        host.Window.Show( );
        host.Window.UpdateLayout( );
        return host;
    }

    private static IEnumerable<Button> HintButtons(MainWindow window)
    {
        return [window.TopEdgeHint, window.BottomEdgeHint, window.LeftEdgeHint, window.RightEdgeHint];
    }

    /// <summary>热区中心附近的横线，内容坐标与视口坐标在初始视图下一致</summary>
    private static Stroke BandStroke(MainWindowHost host, EdgeScrollSide side)
    {
        var canvas = host.Canvas;
        var view = new View(canvas.CurrentScale, canvas.OffsetX, canvas.OffsetY);
        var threshold = side switch
        {
            EdgeScrollSide.Top => canvas.edgeScrollTopThreshold,
            EdgeScrollSide.Bottom => canvas.edgeScrollBottomThreshold,
            EdgeScrollSide.Left => canvas.edgeScrollLeftThreshold,
            _ => canvas.edgeScrollRightThreshold,
        };
        var band = InkCanvasNext.InkCanvasNext.EdgeBandContentRect(view, canvas.ViewportSize, threshold, side);
        var center = new Point(band.Left + band.Width / 2, band.Top + band.Height / 2);

        return new Stroke([new StylusPoint(center.X - 20, center.Y), new StylusPoint(center.X + 20, center.Y)]);
    }

    private static void PumpUntilIdle(InkCanvasNext.InkCanvasNext canvas)
    {
        var clock = Stopwatch.StartNew( );
        while (canvas.IsEdgeScrolling && clock.ElapsedMilliseconds < 3000)
        {
            Dispatcher.CurrentDispatcher.Invoke(( ) => { }, DispatcherPriority.Background);
            Thread.Sleep(10);
        }
    }
}
