using System.Windows;
using System.Windows.Interop;

namespace InkCanvasNext.Tests;

/// <summary>
/// 通过自定义 MockTouchDevice 驱动 InkCanvasNext 触摸状态机，断言各状态的迁移。
/// 所有测试体在 STA 线程上执行（WPF 控件与触摸输入管线要求）。
/// </summary>
public class TouchStateMachineTests
{
    // 坐标位于根可视（InkCanvasNext）坐标系。MockTouchDevice 预先捕获到 InnerCanvas，
    // 命中测试被跳过，因此坐标无需落在视口内，间距可任意放大以满足 d > l 的 MultiDraw 判定。
    private static readonly Point P1 = new(100, 100);
    private static readonly Point Close2 = new(180, 140);      // 与 P1 相距 ~89px，恒满足 d <= l
    private static readonly Point Far2 = new(100, 20_000);     // 与 P1 相距 ~19900px，恒满足 d > l
    private static readonly Point Close3 = new(300, 100);
    private static readonly Point Close4 = new(350, 120);
    private static readonly Point Close5 = new(400, 140);
    private static readonly Point Moved = new(100, 200);       // 与 P1 相距 100px > 20px 位移阈值

    [Fact]
    public void Idle_OneFinger_EntersEvalDraw_ThenRelease_ReturnsIdle( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var finger = host.Device( );

            finger.Down(host.Target, P1);
            Assert.Equal(TouchState.EvalDraw, host.Canvas.State);

            finger.Up(host.Target, P1);
            Assert.Equal(TouchState.Idle, host.Canvas.State);
        });
    }

    [Fact]
    public void EvalDraw_MoveBeyondThreshold_EntersDraw( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var finger = host.Device( );

            finger.Down(host.Target, P1);
            finger.Move(host.Target, Moved);
            Assert.Equal(TouchState.Draw, host.Canvas.State);
        });
    }

    [Fact]
    public void Idle_TwoCloseFingers_EntersPanZoom( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            host.Device( ).Down(host.Target, P1);
            host.Device( ).Down(host.Target, Close2);
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);
        });
    }

    [Fact]
    public void EvalDraw_SecondFingerClose_EntersPanZoom( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            host.Device( ).Down(host.Target, P1);
            host.Device( ).Down(host.Target, Close2);
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);
        });
    }

    [Fact]
    public void Idle_TwoFarFingers_EntersMultiDraw( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            host.Device( ).Down(host.Target, P1);
            host.Device( ).Down(host.Target, Far2);
            Assert.Equal(TouchState.MultiDraw, host.Canvas.State);
        });
    }

    [Fact]
    public void EvalDraw_SecondFingerFar_EntersMultiDraw( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            host.Device( ).Down(host.Target, P1);
            host.Device( ).Down(host.Target, Far2);
            Assert.Equal(TouchState.MultiDraw, host.Canvas.State);
        });
    }

    [Fact]
    public void Draw_SecondFingerFar_EntersMultiDraw( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            var finger = host.Device( );
            finger.Down(host.Target, P1);
            finger.Move(host.Target, Moved); // EvalDraw -> Draw
            host.Device( ).Down(host.Target, Far2);

            Assert.Equal(TouchState.MultiDraw, host.Canvas.State);
        });
    }

    [Fact]
    public void Draw_Release_ReturnsIdle( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            var finger = host.Device( );
            finger.Down(host.Target, P1);
            finger.Move(host.Target, Moved);
            Assert.Equal(TouchState.Draw, host.Canvas.State);

            finger.Up(host.Target, Moved);
            Assert.Equal(TouchState.Idle, host.Canvas.State);
        });
    }

    [Fact]
    public void Idle_ThreeFingers_EntersPan( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            host.Device( ).Down(host.Target, P1);
            host.Device( ).Down(host.Target, Close2);
            host.Device( ).Down(host.Target, Close3);
            Assert.Equal(TouchState.Pan, host.Canvas.State);
        });
    }

    [Fact]
    public void Idle_FiveFingers_EntersEraser( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            host.Device( ).Down(host.Target, P1);
            host.Device( ).Down(host.Target, Close2);
            host.Device( ).Down(host.Target, Close3);
            host.Device( ).Down(host.Target, Close4);
            host.Device( ).Down(host.Target, Close5);
            Assert.Equal(TouchState.Eraser, host.Canvas.State);
        });
    }

    [Fact]
    public void Idle_SelectMode_OneFinger_EntersSelection( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.Select;

            var finger = host.Device( );
            finger.Down(host.Target, P1);
            Assert.Equal(TouchState.Selection, host.Canvas.State);

            // 选区入口优先于 EvalDraw，且 count==0 回 Idle
            finger.Up(host.Target, P1);
            Assert.Equal(TouchState.Idle, host.Canvas.State);
        });
    }

    // ---------- PanZoom（含迁移规则变更：1、3+ 指均进 Pan，仅 2 指停留） ----------

    [Fact]
    public void PanZoom_StaysPanZoom_WhileTwoFingers( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, P1);
            b.Down(host.Target, Close2);
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);

            a.Move(host.Target, new Point(150, 150));
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);
        });
    }

    [Fact]
    public void PanZoom_ReleaseToOneFinger_EntersPan( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, P1);
            b.Down(host.Target, Close2);
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);

            b.Up(host.Target, Close2);
            Assert.Equal(TouchState.Pan, host.Canvas.State);
        });
    }

    [Fact]
    public void PanZoom_AddThirdFinger_EntersPan( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            host.Device( ).Down(host.Target, P1);
            host.Device( ).Down(host.Target, Close2);
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);

            host.Device( ).Down(host.Target, Close3);
            Assert.Equal(TouchState.Pan, host.Canvas.State);
        });
    }

    [Fact]
    public void PanZoom_ReleaseAll_ReturnsIdle( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, P1);
            b.Down(host.Target, Close2);
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);

            a.Up(host.Target, P1);
            b.Up(host.Target, Close2);
            Assert.Equal(TouchState.Idle, host.Canvas.State);
        });
    }

    [Fact]
    public void PanZoom_ToPan_MovePansCanvas( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, P1);
            b.Down(host.Target, Close2);
            b.Up(host.Target, Close2); // PanZoom -> Pan（剩 a）

            var before = host.Canvas.OffsetX;
            a.Move(host.Target, new Point(50, 100)); // 向左平移 50px
            host.Canvas.UpdateLayout( ); // ScrollViewer 的滚动偏移需一次布局后才生效
            Assert.True(host.Canvas.OffsetX > before, "Pan 状态下手势移动应平移画布");
            Assert.Equal(TouchState.Pan, host.Canvas.State);
        });
    }

    // ---------- MultiDraw ----------

    [Fact]
    public void MultiDraw_ReleaseToOneFinger_EntersDraw( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, P1);
            b.Down(host.Target, Far2);
            Assert.Equal(TouchState.MultiDraw, host.Canvas.State);

            b.Up(host.Target, Far2);
            Assert.Equal(TouchState.Draw, host.Canvas.State);
        });
    }

    [Fact]
    public void MultiDraw_ReleaseAll_ReturnsIdle( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, P1);
            b.Down(host.Target, Far2);
            Assert.Equal(TouchState.MultiDraw, host.Canvas.State);

            a.Up(host.Target, P1);
            b.Up(host.Target, Far2);
            Assert.Equal(TouchState.Idle, host.Canvas.State);
        });
    }

    // ---------- Selection ----------

    [Fact]
    public void Selection_Release_ReturnsIdle( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.Select;

            var finger = host.Device( );
            finger.Down(host.Target, P1);
            Assert.Equal(TouchState.Selection, host.Canvas.State);

            finger.Up(host.Target, P1);
            Assert.Equal(TouchState.Idle, host.Canvas.State);
        });
    }
}
