using System.Windows;

namespace InkCanvasNext.Tests;

/// <summary>
/// 视图写入：读到的偏移就是屏幕位置，写入不依赖布局
/// </summary>
public class ViewStateTests
{
    private static readonly Point P1 = new(100, 100);

    /// <summary>手势中写整个视图立即生效，读回与屏幕映射一致</summary>
    [Fact]
    public void CurrentView_WrittenDuringGesture_AppliesImmediately( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var a = host.Device( );
            var b = host.Device( );
            var second = TestPoints.CloseTo(host, P1);
            var step = TestPoints.Displacement(host) * 2;

            a.Down(host.Target, P1);
            b.Down(host.Target, second);
            a.Move(host.Target, new Point(P1.X - step, P1.Y));
            b.Move(host.Target, new Point(second.X - step, second.Y));

            Assert.True(host.Canvas.OffsetX > 0);

            var written = new View(1, 0, 0);
            host.Canvas.CurrentView = written;

            Assert.Equal(written, host.Canvas.CurrentView);
            Assert.Equal(0, MappedOffsetX(host), 6);
        });
    }

    /// <summary>手势中读取的偏移与屏幕映射一致，不调布局也能读出</summary>
    [Fact]
    public void CurrentView_DuringGesture_MatchesScreenMapping( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var a = host.Device( );
            var b = host.Device( );
            var second = TestPoints.CloseTo(host, P1);
            var step = TestPoints.Displacement(host) * 2;

            a.Down(host.Target, P1);
            b.Down(host.Target, second);
            a.Move(host.Target, new Point(P1.X - step, P1.Y - step));
            b.Move(host.Target, new Point(second.X - step, second.Y - step));

            var view = host.Canvas.CurrentView;
            Assert.True(view.OffsetX > 0);
            Assert.True(view.OffsetY > 0);
            Assert.Equal(view.OffsetX, MappedOffsetX(host), 6);
            Assert.Equal(view.OffsetY, MappedOffsetY(host), 6);
        });
    }

    /// <summary>抬手不改变视图：手势中的偏移就是抬手后的偏移，没有第二次写入</summary>
    [Fact]
    public void GesturePan_Release_KeepsView( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var a = host.Device( );
            var b = host.Device( );
            var second = TestPoints.CloseTo(host, P1);
            var step = TestPoints.Displacement(host) * 2;

            a.Down(host.Target, P1);
            b.Down(host.Target, second);
            a.Move(host.Target, new Point(P1.X - step, P1.Y));
            b.Move(host.Target, new Point(second.X - step, second.Y));

            var visual = host.Canvas.CurrentView;

            a.Up(host.Target, new Point(P1.X - step, P1.Y));
            b.Up(host.Target, new Point(second.X - step, second.Y));
            host.Canvas.UpdateLayout( );

            Assert.Equal(TouchState.Idle, host.Canvas.State);
            Assert.Equal(visual.OffsetX, host.Canvas.OffsetX, 6);
            Assert.Equal(visual.OffsetY, host.Canvas.OffsetY, 6);
            Assert.Equal(visual.OffsetX, MappedOffsetX(host), 6);
        });
    }

    /// <summary>同一帧内多个触摸报文的位移都进入视图</summary>
    [Fact]
    public void PanZoom_MultipleMovesInFrame_AccumulateOffset( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var start = host.Canvas.CurrentView;
            var a = host.Device( );
            var b = host.Device( );
            var second = TestPoints.CloseTo(host, P1);
            // 首指位移越过缩放锁阈值，隔离出纯平移路径
            var step = TestPoints.Displacement(host) * 4;

            a.Down(host.Target, P1);
            b.Down(host.Target, second);
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);

            a.Move(host.Target, new Point(P1.X - step, P1.Y));
            b.Move(host.Target, new Point(second.X - step, second.Y));
            a.Move(host.Target, new Point(P1.X - 2 * step, P1.Y));
            b.Move(host.Target, new Point(second.X - 2 * step, second.Y));

            Assert.Equal(start.OffsetX + 2 * step, host.Canvas.CurrentView.OffsetX, 6);

            a.Up(host.Target, new Point(P1.X - 2 * step, P1.Y));
            b.Up(host.Target, new Point(second.X - 2 * step, second.Y));
            host.Canvas.UpdateLayout( );

            Assert.Equal(start.OffsetX + 2 * step, host.Canvas.OffsetX, 6);
        });
    }

    /// <summary>缩放按基线指位钉住内容点，缩放与偏移同时写进变换</summary>
    [Fact]
    public void PanZoom_Pinch_KeepsContentUnderAnchor( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost(options: new( ) { SmoothZoom = false });
            var start = host.Canvas.CurrentView;
            var content = start.ToContent(P1);
            var spacing = TestPoints.PinchSpacing(host);

            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, P1);
            b.Down(host.Target, new Point(P1.X + spacing, P1.Y));

            b.Move(host.Target, new Point(P1.X + 2 * spacing, P1.Y));

            Assert.Equal(2, host.Canvas.CurrentView.Scale, 6);
            Assert.Equal(content.X, host.Canvas.CurrentView.ToContent(P1).X, 6);
            Assert.Equal(content.Y, host.Canvas.CurrentView.ToContent(P1).Y, 6);

            a.Up(host.Target, P1);
            b.Up(host.Target, new Point(P1.X + 2 * spacing, P1.Y));
        });
    }

    /// <summary>缩放后的映射与视图一致，缩放走 LayoutTransform，布局完成后可换算</summary>
    [Fact]
    public void PanZoom_Pinch_MappingMatchesView( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost(options: new( ) { SmoothZoom = false });
            var spacing = TestPoints.PinchSpacing(host);

            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, P1);
            b.Down(host.Target, new Point(P1.X + spacing, P1.Y));
            b.Move(host.Target, new Point(P1.X + 2 * spacing, P1.Y));

            host.Canvas.UpdateLayout( );

            var view = host.Canvas.CurrentView;
            Assert.Equal(view.Scale, host.Canvas.CurrentScale, 9);
            Assert.Equal(view.OffsetX, MappedOffsetX(host), 6);
            Assert.Equal(view.OffsetY, MappedOffsetY(host), 6);

            a.Up(host.Target, P1);
            b.Up(host.Target, new Point(P1.X + 2 * spacing, P1.Y));
        });
    }

    /// <summary>PanZoom 到 EvalPan 到 Pan 的手势位移连续累计</summary>
    [Fact]
    public void PanZoom_ToPan_KeepsGestureOffset( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var start = host.Canvas.CurrentView;
            var a = host.Device( );
            var b = host.Device( );
            var second = TestPoints.CloseTo(host, P1);
            // 首指位移越过缩放锁阈值，隔离出纯平移路径
            var step = TestPoints.Displacement(host) * 4;

            a.Down(host.Target, P1);
            b.Down(host.Target, second);
            a.Move(host.Target, new Point(P1.X - step, P1.Y));
            b.Move(host.Target, new Point(second.X - step, second.Y));
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);

            a.Up(host.Target, new Point(P1.X - step, P1.Y));
            Assert.Equal(TouchState.EvalPan, host.Canvas.State);

            b.Move(host.Target, new Point(second.X - 2 * step, second.Y));
            Assert.Equal(TouchState.Pan, host.Canvas.State);

            b.Up(host.Target, new Point(second.X - 2 * step, second.Y));
            host.Canvas.UpdateLayout( );

            Assert.Equal(start.OffsetX + 2 * step, host.Canvas.OffsetX, 6);
        });
    }

    /// <summary>手势中写单轴偏移立即反映到屏幕，不需要布局</summary>
    [Fact]
    public void OffsetX_WrittenDuringGesture_AppliesWithoutLayout( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var a = host.Device( );
            var b = host.Device( );
            var second = TestPoints.CloseTo(host, P1);
            var step = TestPoints.Displacement(host) * 2;

            a.Down(host.Target, P1);
            b.Down(host.Target, second);
            a.Move(host.Target, new Point(P1.X - step, P1.Y));
            b.Move(host.Target, new Point(second.X - step, second.Y));

            Assert.True(MappedOffsetX(host) > 0);

            host.Canvas.OffsetX = 0;

            Assert.Equal(0, host.Canvas.OffsetX);
            Assert.Equal(0, MappedOffsetX(host), 6);
        });
    }

    /// <summary>进出手势族都不触发布局</summary>
    [Fact]
    public void Pan_Release_DoesNotInvalidateLayout( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.UpdateLayout( );

            var layouts = 0;
            host.Canvas.LayoutUpdated += (_, _) => layouts++;

            var a = host.Device( );
            var b = host.Device( );
            var second = TestPoints.CloseTo(host, P1);
            var step = TestPoints.Displacement(host) * 2;

            a.Down(host.Target, P1);
            b.Down(host.Target, second);
            a.Move(host.Target, new Point(P1.X - step, P1.Y));
            b.Move(host.Target, new Point(second.X - step, second.Y));
            a.Up(host.Target, new Point(P1.X - step, P1.Y));
            b.Up(host.Target, new Point(second.X - step, second.Y));

            Assert.Equal(TouchState.Idle, host.Canvas.State);
            Assert.Equal(0, layouts);
        });
    }

    private static double MappedOffsetX(TouchHost host)
    {
        return -host.Target.TranslatePoint(new Point(0, 0), host.Canvas).X;
    }

    private static double MappedOffsetY(TouchHost host)
    {
        return -host.Target.TranslatePoint(new Point(0, 0), host.Canvas).Y;
    }
}
