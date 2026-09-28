using System.Windows;
using System.Windows.Controls;

namespace InkCanvasNext.Tests;

/// <summary>
/// 通过自定义 MockTouchDevice 驱动 InkCanvasNext 触摸状态机，断言各状态的迁移。
/// 所有测试体在 STA 线程上执行（WPF 控件与触摸输入管线要求）。
/// </summary>
public class TouchStateMachineTests
{
    // 坐标位于根可视（InkCanvasNext）坐标系。MockTouchDevice 预先捕获到 InnerCanvas，
    // 命中测试被跳过，因此坐标无需落在视口内；间距/位移相关的坐标一律由 TestPoints 按运行时参数推导。
    private static readonly Point P1 = new(100, 100);

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
            finger.Move(host.Target, TestPoints.BeyondDisplacement(host, P1));
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
            host.Device( ).Down(host.Target, TestPoints.CloseTo(host, P1));
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
            host.Device( ).Down(host.Target, TestPoints.CloseTo(host, P1));
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
            host.Device( ).Down(host.Target, TestPoints.FarFrom(host, P1));
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
            host.Device( ).Down(host.Target, TestPoints.FarFrom(host, P1));
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
            finger.Move(host.Target, TestPoints.BeyondDisplacement(host, P1)); // EvalDraw -> Draw
            host.Device( ).Down(host.Target, TestPoints.FarFrom(host, P1));

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
            finger.Move(host.Target, TestPoints.BeyondDisplacement(host, P1));
            Assert.Equal(TouchState.Draw, host.Canvas.State);

            finger.Up(host.Target, TestPoints.BeyondDisplacement(host, P1));
            Assert.Equal(TouchState.Idle, host.Canvas.State);
        });
    }

    [Fact]
    public void Idle_ThreeFingers_EntersPan( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var cluster = TestPoints.CloseCluster(host, P1, 3);

            host.Device( ).Down(host.Target, cluster[0]);
            host.Device( ).Down(host.Target, cluster[1]);
            host.Device( ).Down(host.Target, cluster[2]);
            Assert.Equal(TouchState.Pan, host.Canvas.State);
        });
    }

    [Fact]
    public void Idle_FiveFingers_EntersEraser( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var cluster = TestPoints.CloseCluster(host, P1, 5);

            foreach (var point in cluster)
            {
                host.Device( ).Down(host.Target, point);
            }

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
            b.Down(host.Target, TestPoints.CloseTo(host, P1));
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
            b.Down(host.Target, TestPoints.CloseTo(host, P1));
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);

            b.Up(host.Target, TestPoints.CloseTo(host, P1));
            Assert.Equal(TouchState.Pan, host.Canvas.State);
        });
    }

    [Fact]
    public void PanZoom_AddThirdFinger_EntersPan( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var cluster = TestPoints.CloseCluster(host, P1, 3);

            host.Device( ).Down(host.Target, cluster[0]);
            host.Device( ).Down(host.Target, cluster[1]);
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);

            host.Device( ).Down(host.Target, cluster[2]);
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
            b.Down(host.Target, TestPoints.CloseTo(host, P1));
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);

            a.Up(host.Target, P1);
            b.Up(host.Target, TestPoints.CloseTo(host, P1));
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
            b.Down(host.Target, TestPoints.CloseTo(host, P1));
            b.Up(host.Target, TestPoints.CloseTo(host, P1)); // PanZoom -> Pan（剩 a）

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
            b.Down(host.Target, TestPoints.FarFrom(host, P1));
            Assert.Equal(TouchState.MultiDraw, host.Canvas.State);

            b.Up(host.Target, TestPoints.FarFrom(host, P1));
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
            b.Down(host.Target, TestPoints.FarFrom(host, P1));
            Assert.Equal(TouchState.MultiDraw, host.Canvas.State);

            a.Up(host.Target, P1);
            b.Up(host.Target, TestPoints.FarFrom(host, P1));
            Assert.Equal(TouchState.Idle, host.Canvas.State);
        });
    }

    /// <summary>多指作画中重置输入（换页/窗口失活路径）：在途笔画按抬手提交，状态回 Idle。</summary>
    [Fact]
    public void MultiDraw_ResetTouchState_CommitsInflightStrokes( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            var a = host.Device( );
            var b = host.Device( );
            var farPoint = TestPoints.FarFrom(host, P1);
            a.Down(host.Target, P1);
            b.Down(host.Target, farPoint);
            Assert.Equal(TouchState.MultiDraw, host.Canvas.State);

            a.Move(host.Target, new Point(120, 120));
            b.Move(host.Target, new Point(farPoint.X, farPoint.Y + 100));

            host.Canvas.ResetTouchState( );

            Assert.Equal(TouchState.Idle, host.Canvas.State);
            Assert.Equal(2, host.Canvas.Strokes.Count);
        });
    }

    // ---------- 编辑模式的覆盖与恢复（OverridesEditing 元数据） ----------

    /// <summary>MultiDraw --> Draw：迁出覆盖编辑模式的接管态时，把编辑模式恢复为当前工具。</summary>
    [Fact]
    public void MultiDraw_ReleaseToOneFinger_RestoresEditingMode( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var farPoint = TestPoints.FarFrom(host, P1);

            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, P1);
            b.Down(host.Target, farPoint);
            Assert.Equal(TouchState.MultiDraw, host.Canvas.State);
            Assert.Equal(InkCanvasEditingMode.None, host.Canvas.InnerCanvasElement.EditingMode);

            b.Up(host.Target, farPoint);
            Assert.Equal(TouchState.Draw, host.Canvas.State);
            Assert.Equal(InkCanvasEditingMode.Ink, host.Canvas.InnerCanvasElement.EditingMode);

            a.Up(host.Target, P1);
            Assert.Equal(TouchState.Idle, host.Canvas.State);
        });
    }

    /// <summary>双指手势中切换工具：EditingMode 延迟到迁出路径统一应用为当前模式（取代 prevMode/RestoreMode）。</summary>
    [Fact]
    public void PanZoom_ModeSwitchDuringGesture_AppliesNewModeOnExit( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var closePoint = TestPoints.CloseTo(host, P1);

            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, P1);
            b.Down(host.Target, closePoint);
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);
            Assert.Equal(InkCanvasEditingMode.None, host.Canvas.InnerCanvasElement.EditingMode);

            host.Canvas.Mode = InkCanvasNextMode.EraseStroke;
            Assert.Equal(InkCanvasEditingMode.None, host.Canvas.InnerCanvasElement.EditingMode);

            a.Up(host.Target, P1);
            b.Up(host.Target, closePoint);
            Assert.Equal(TouchState.Idle, host.Canvas.State);
            Assert.Equal(InkCanvasEditingMode.EraseByStroke, host.Canvas.InnerCanvasElement.EditingMode);
        });
    }

    /// <summary>单指按住未起笔时切换工具：迁出 EvalDraw 回 Idle 时应用新模式。</summary>
    [Fact]
    public void EvalDraw_ModeSwitchDuringStroke_AppliesNewModeOnRelease( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            var finger = host.Device( );
            finger.Down(host.Target, P1);
            Assert.Equal(TouchState.EvalDraw, host.Canvas.State);
            Assert.Equal(InkCanvasEditingMode.Ink, host.Canvas.InnerCanvasElement.EditingMode);

            host.Canvas.Mode = InkCanvasNextMode.EraseStroke;
            Assert.Equal(InkCanvasEditingMode.Ink, host.Canvas.InnerCanvasElement.EditingMode);

            finger.Up(host.Target, P1);
            Assert.Equal(TouchState.Idle, host.Canvas.State);
            Assert.Equal(InkCanvasEditingMode.EraseByStroke, host.Canvas.InnerCanvasElement.EditingMode);
            Assert.Empty(host.Canvas.Strokes);
        });
    }

    // ---------- ResetTouchState（换页/窗口失活路径） ----------

    [Fact]
    public void PanZoom_ResetTouchState_RestoresEditingMode( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var closePoint = TestPoints.CloseTo(host, P1);

            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, P1);
            b.Down(host.Target, closePoint);
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);
            Assert.Equal(InkCanvasEditingMode.None, host.Canvas.InnerCanvasElement.EditingMode);

            host.Canvas.ResetTouchState( );

            Assert.Equal(TouchState.Idle, host.Canvas.State);
            Assert.Equal(InkCanvasEditingMode.Ink, host.Canvas.InnerCanvasElement.EditingMode);
        });
    }

    [Fact]
    public void Eraser_ResetTouchState_RestoresEditingMode( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var spacing = TestPoints.Spacing(host);

            for (var i = 0; i < 5; i++)
            {
                host.Device( ).Down(host.Target, new Point(P1.X + i * spacing / 10, P1.Y));
            }

            Assert.Equal(TouchState.Eraser, host.Canvas.State);
            Assert.Equal(InkCanvasEditingMode.None, host.Canvas.InnerCanvasElement.EditingMode);

            host.Canvas.ResetTouchState( );

            Assert.Equal(TouchState.Idle, host.Canvas.State);
            Assert.Equal(InkCanvasEditingMode.Ink, host.Canvas.InnerCanvasElement.EditingMode);
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
