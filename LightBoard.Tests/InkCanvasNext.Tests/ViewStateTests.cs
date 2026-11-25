using System.Windows;

namespace InkCanvasNext.Tests;

/// <summary>
/// 视图写入：手势增量、外部写入与抬手提交
/// </summary>
public class ViewStateTests
{
    private static readonly Point P1 = new(100, 100);

    /// <summary>
    /// 外部写视图时手势增量清零，后续事件按新基准继续累计
    /// </summary>
    [Fact]
    public void CurrentView_WrittenDuringGesture_DiscardsPan( )
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

            Assert.True(host.Canvas.EffectiveView.OffsetX > host.Canvas.GestureView.OffsetX);

            var written = new View(1, 0, 0);
            host.Canvas.CurrentView = written;

            Assert.Equal(written, host.Canvas.EffectiveView);
        });
    }

    /// <summary>
    /// 抬手离开平移组后 RenderTransform 清零，滚动偏移接住抬手前的可见视图
    /// </summary>
    [Fact]
    public void GesturePan_CommittedOnExit( )
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

            var visual = host.Canvas.EffectiveView;
            Assert.True(visual.OffsetX > host.Canvas.GestureView.OffsetX);

            a.Up(host.Target, new Point(P1.X - step, P1.Y));
            b.Up(host.Target, new Point(second.X - step, second.Y));
            host.Canvas.UpdateLayout( );

            Assert.Equal(visual.OffsetX, host.Canvas.OffsetX, 6);
            Assert.Equal(visual.OffsetY, host.Canvas.OffsetY, 6);
            Assert.Equal(host.Canvas.GestureView.OffsetX, host.Canvas.OffsetX, 6);
        });
    }

    /// <summary>
    /// 同一帧内多个触摸报文的位移都进入有效视图，抬手后提交到滚动偏移
    /// </summary>
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

            Assert.Equal(start.OffsetX + 2 * step, host.Canvas.EffectiveView.OffsetX, 6);

            a.Up(host.Target, new Point(P1.X - 2 * step, P1.Y));
            b.Up(host.Target, new Point(second.X - 2 * step, second.Y));
            host.Canvas.UpdateLayout( );

            Assert.Equal(start.OffsetX + 2 * step, host.Canvas.OffsetX, 6);
        });
    }

    /// <summary>
    /// 缩放按基线指位钉住内容点，手势增量走 RenderTransform
    /// </summary>
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

            Assert.Equal(2, host.Canvas.EffectiveView.Scale, 6);
            Assert.Equal(content.X, host.Canvas.EffectiveView.ToContent(P1).X, 6);
            Assert.Equal(content.Y, host.Canvas.EffectiveView.ToContent(P1).Y, 6);

            a.Up(host.Target, P1);
            b.Up(host.Target, new Point(P1.X + 2 * spacing, P1.Y));
        });
    }

    /// <summary>
    /// PanZoom 到 EvalPan 到 Pan 的手势增量连续累计
    /// </summary>
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

    /// <summary>
    /// 手势中写单轴偏移与写整个视图语义一致：手势增量作废，读回等于写入值
    /// </summary>
    [Fact]
    public void OffsetX_WrittenDuringGesture_DiscardsPan( )
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

            Assert.True(host.Canvas.EffectiveView.OffsetX > host.Canvas.GestureView.OffsetX);

            host.Canvas.OffsetX = 0;

            Assert.Equal(0.0, host.Canvas.OffsetX);
            Assert.Equal(host.Canvas.GestureView.OffsetX, host.Canvas.EffectiveView.OffsetX);
        });
    }

    /// <summary>
    /// PanZoom 的平移分量只进 CanvasPan：已提交视图与滚动偏移全程不变
    /// </summary>
    [Fact]
    public void PanZoom_MovesKeepBaseView( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var start = host.Canvas.GestureView;
            var a = host.Device( );
            var b = host.Device( );
            var second = TestPoints.CloseTo(host, P1);
            var step = TestPoints.Displacement(host) * 2;

            a.Down(host.Target, P1);
            b.Down(host.Target, second);
            a.Move(host.Target, new Point(P1.X - step, P1.Y));
            b.Move(host.Target, new Point(second.X - step, second.Y));

            Assert.Equal(start, host.Canvas.GestureView);
            Assert.Equal(start.OffsetX, host.Canvas.CanvasScroll.HorizontalOffset, 6);
            Assert.Equal(start.OffsetY, host.Canvas.CanvasScroll.VerticalOffset, 6);
            Assert.True(host.Canvas.EffectiveView.OffsetX > start.OffsetX);
        });
    }

    /// <summary>
    /// 对角平移抬手时两轴都要在同一帧提交落地，不能留一轴还在命令队列里
    /// </summary>
    [Fact]
    public void GesturePan_Diagonal_CommitLandsBothAxes( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var a = host.Device( );
            var b = host.Device( );
            var second = TestPoints.CloseTo(host, P1);
            var step = TestPoints.Displacement(host) * 2;
            var movedA = new Point(P1.X - step, P1.Y - step);
            var movedB = new Point(second.X - step, second.Y - step);

            a.Down(host.Target, P1);
            b.Down(host.Target, second);
            a.Move(host.Target, movedA);
            b.Move(host.Target, movedB);

            var visual = host.Canvas.EffectiveView;
            Assert.True(visual.OffsetX > host.Canvas.GestureView.OffsetX);
            Assert.True(visual.OffsetY > host.Canvas.GestureView.OffsetY);

            a.Up(host.Target, movedA);
            b.Up(host.Target, movedB);

            Assert.Equal(visual.OffsetX, host.Canvas.CanvasScroll.HorizontalOffset, 6);
            Assert.Equal(visual.OffsetY, host.Canvas.CanvasScroll.VerticalOffset, 6);
        });
    }
}
