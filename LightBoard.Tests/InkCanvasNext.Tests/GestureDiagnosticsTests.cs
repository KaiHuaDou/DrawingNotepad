using System.Windows;

namespace InkCanvasNext.Tests;

/// <summary>
/// 手势诊断属性：状态面板读取的实时量必须与状态机内部基线一致
/// </summary>
public class GestureDiagnosticsTests
{
    private static readonly Point P1 = new(100, 100);

    /// <summary>双指张开时 distance 跟随当前指距，distance0 保持手势基线</summary>
    [Fact]
    public void PanZoom_DistanceTracksFingers_BaselineStaysAtGestureStart( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            var spacing = TestPoints.PinchSpacing(host);
            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, P1);
            b.Down(host.Target, new Point(P1.X + spacing, P1.Y));

            Assert.Equal(TouchState.PanZoom, host.Canvas.State);
            Assert.Equal(spacing, host.Canvas.PinchDistance0, 6);
            Assert.Equal(spacing, host.Canvas.PinchDistance, 6);

            // 对称张开：两指各外移 20，中点不动，不触发缩放锁死
            a.Move(host.Target, new Point(P1.X - 20, P1.Y));
            b.Move(host.Target, new Point(P1.X + spacing + 20, P1.Y));

            Assert.Equal(spacing, host.Canvas.PinchDistance0, 6);
            Assert.Equal(spacing + 40, host.Canvas.PinchDistance, 6);
        });
    }

    /// <summary>两指同向位移越过阈值后缩放锁死，诊断标记同步给出</summary>
    [Fact]
    public void PanZoom_TranslateBeyondThreshold_LocksZoom( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            var displace = host.Canvas.options.PanZoomDisplaceThreshold;
            var dy = displace * 1.05 + 1;
            var spacing = Math.Max(TestPoints.PinchSpacing(host), dy * 1.7);

            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, P1);
            b.Down(host.Target, new Point(P1.X + spacing, P1.Y));
            Assert.False(host.Canvas.ZoomLocked);

            a.Move(host.Target, new Point(P1.X, P1.Y - dy));
            b.Move(host.Target, new Point(P1.X + spacing, P1.Y - dy));

            Assert.True(host.Canvas.ZoomLocked);
        });
    }

    /// <summary>平移位移按手指移动方向累计，而非视口增量方向</summary>
    [Fact]
    public void Pan_AccumulatesFingerDisplacement( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, P1);
            b.Down(host.Target, TestPoints.CloseTo(host, P1));
            b.Up(host.Target, TestPoints.CloseTo(host, P1)); // PanZoom -> EvalPan，基线重立

            Assert.Equal(0.0, host.Canvas.PanDisplacement.X);
            Assert.Equal(0.0, host.Canvas.PanDisplacement.Y);

            // 左移 50、上移 10：越过单指起笔阈值提交 Pan
            a.Move(host.Target, new Point(50, 90));
            Assert.Equal(TouchState.Pan, host.Canvas.State);
            Assert.Equal(-50, host.Canvas.PanDisplacement.X, 6);
            Assert.Equal(-10, host.Canvas.PanDisplacement.Y, 6);

            // 锁定后继续累计
            a.Move(host.Target, new Point(40, 90));
            Assert.Equal(-60, host.Canvas.PanDisplacement.X, 6);
            Assert.Equal(-10, host.Canvas.PanDisplacement.Y, 6);
        });
    }

    /// <summary>状态特征随当前状态给出，回 Idle 后回到全默认</summary>
    [Fact]
    public void Traits_FollowCurrentState( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            Assert.False(host.Canvas.IsGestureActive);
            Assert.Equal(new StateTraits( ), host.Canvas.Traits);

            var cluster = TestPoints.CloseCluster(host, P1, 3);
            var a = host.Device( );
            var b = host.Device( );
            var c = host.Device( );
            a.Down(host.Target, cluster[0]);
            b.Down(host.Target, cluster[1]);
            c.Down(host.Target, cluster[2]);

            Assert.Equal(TouchState.EvalPan, host.Canvas.State);
            Assert.True(host.Canvas.IsGestureActive);
            Assert.True(host.Canvas.Traits.BlocksNativeInput);
            Assert.True(host.Canvas.Traits.OverridesEditing);

            a.Up(host.Target, cluster[0]);
            b.Up(host.Target, cluster[1]);
            c.Up(host.Target, cluster[2]);

            Assert.Equal(TouchState.Idle, host.Canvas.State);
            Assert.Equal(new StateTraits( ), host.Canvas.Traits);
        });
    }
}
