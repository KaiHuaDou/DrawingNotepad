using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasNext.Tests;

/// <summary>
/// 通过自定义 MockTouchDevice 驱动 InkCanvasNext 触摸状态机，断言各状态的迁移
/// 所有测试体在 STA 线程上执行（WPF 控件与触摸输入管线要求）
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

    /// <summary>Draw 中落第二指不看间距：并拢的第二指同样结束原生笔画并迁入多指书写</summary>
    [Fact]
    public void Draw_SecondFingerClose_EntersMultiDraw( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            var finger = host.Device( );
            finger.Down(host.Target, P1);
            finger.Move(host.Target, TestPoints.BeyondDisplacement(host, P1));
            Assert.Equal(TouchState.Draw, host.Canvas.State);

            host.Device( ).Down(host.Target, TestPoints.CloseTo(host, P1));

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

    /// <summary>掌擦抬起部分手指后仍留在擦除状态：剩余单指继续擦除抬指后落入的笔画</summary>
    [Fact]
    public void Eraser_ReleaseToSingleFinger_KeepsErasing( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var cluster = TestPoints.CloseCluster(host, P1, 5);
            var fingers = cluster.Select(_ => host.Device( )).ToArray( );
            for (var i = 0; i < fingers.Length; i++)
            {
                fingers[i].Down(host.Target, cluster[i]);
            }

            Assert.Equal(TouchState.Eraser, host.Canvas.State);

            for (var i = 1; i < fingers.Length; i++)
            {
                fingers[i].Up(host.Target, cluster[i]);
            }

            Assert.Equal(TouchState.Eraser, host.Canvas.State);
            Assert.Equal(1, host.Canvas.ActiveTouchCount);

            // 抬指后才落笔：五指初始包络不覆盖它，擦除只能由剩余单指完成
            var strokePoint = new Point(cluster[0].X + TestPoints.Spacing(host), cluster[0].Y);
            var stroke = new Stroke(
                [new StylusPoint(strokePoint.X - 35, strokePoint.Y), new StylusPoint(strokePoint.X + 35, strokePoint.Y)],
                new DrawingAttributes { Color = Colors.Black, Width = 3, Height = 3 });
            host.Canvas.Strokes.Add(stroke);

            fingers[0].Move(host.Target, strokePoint);

            Assert.DoesNotContain(stroke, host.Canvas.Strokes);
        });
    }

    // ---------- 掌擦触点数（设置项：四指 / 五指） ----------

    /// <summary>默认掌擦触点数五：四指并拢仍是平移</summary>
    [Fact]
    public void Idle_FourCloseFingers_DefaultPalmFingers_EntersPan( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var cluster = TestPoints.CloseCluster(host, P1, 4);

            foreach (var point in cluster)
            {
                host.Device( ).Down(host.Target, point);
            }

            Assert.Equal(TouchState.Pan, host.Canvas.State);
        });
    }

    /// <summary>掌擦触点数为四：三指并拢仍是平移</summary>
    [Fact]
    public void Idle_ThreeCloseFingers_PalmFingersFour_EntersPan( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost(options: new( ) { PalmEraseFingers = 4 });
            var cluster = TestPoints.CloseCluster(host, P1, 3);

            foreach (var point in cluster)
            {
                host.Device( ).Down(host.Target, point);
            }

            Assert.Equal(TouchState.Pan, host.Canvas.State);
        });
    }

    /// <summary>掌擦触点数为四：四指并拢直接掌擦</summary>
    [Fact]
    public void Idle_FourCloseFingers_PalmFingersFour_EntersEraser( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost(options: new( ) { PalmEraseFingers = 4 });
            var cluster = TestPoints.CloseCluster(host, P1, 4);

            foreach (var point in cluster)
            {
                host.Device( ).Down(host.Target, point);
            }

            Assert.Equal(TouchState.Eraser, host.Canvas.State);
        });
    }

    /// <summary>掌擦触点数为四：四指张开仍按间距判为多指书写</summary>
    [Fact]
    public void Idle_FourSpreadFingers_PalmFingersFour_EntersMultiDraw( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost(options: new( ) { PalmEraseFingers = 4 });
            var step = TestPoints.Spacing(host) + 1;

            for (var i = 0; i < 4; i++)
            {
                host.Device( ).Down(host.Target, new Point(P1.X + i * step, P1.Y));
            }

            Assert.Equal(TouchState.MultiDraw, host.Canvas.State);
        });
    }

    /// <summary>掌擦触点数为四：双指缩放中并拢落下四指，第三指先迁 Pan，第四指由 Pan 迁入掌擦</summary>
    [Fact]
    public void PanZoom_TwoMoreCloseFingers_PalmFingersFour_EntersEraser( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost(options: new( ) { PalmEraseFingers = 4 });
            var cluster = TestPoints.CloseCluster(host, P1, 4);

            host.Device( ).Down(host.Target, cluster[0]);
            host.Device( ).Down(host.Target, cluster[1]);
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);

            host.Device( ).Down(host.Target, cluster[2]);
            host.Device( ).Down(host.Target, cluster[3]);

            Assert.Equal(TouchState.Eraser, host.Canvas.State);
        });
    }

    /// <summary>掌擦触点数为四：掌擦中张开并超出触点数，非擦除工具转多指书写</summary>
    [Fact]
    public void Eraser_SpreadBeyondPalmFingers_EntersMultiDraw( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost(options: new( ) { PalmEraseFingers = 4 });
            var cluster = TestPoints.CloseCluster(host, P1, 4);

            foreach (var point in cluster)
            {
                host.Device( ).Down(host.Target, point);
            }

            Assert.Equal(TouchState.Eraser, host.Canvas.State);

            host.Device( ).Down(host.Target, TestPoints.FarFrom(host, P1));

            Assert.Equal(TouchState.MultiDraw, host.Canvas.State);
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

    /// <summary>Pan 没有回 PanZoom 的迁移：抬指后再落第二指仍停在 Pan，需全部抬起重新开始双指手势</summary>
    [Fact]
    public void Pan_AddSecondFinger_StaysPan( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var cluster = TestPoints.CloseCluster(host, P1, 3);

            var a = host.Device( );
            var b = host.Device( );
            var c = host.Device( );
            a.Down(host.Target, cluster[0]);
            b.Down(host.Target, cluster[1]);
            c.Down(host.Target, cluster[2]);
            Assert.Equal(TouchState.Pan, host.Canvas.State);

            c.Up(host.Target, cluster[2]);
            Assert.Equal(TouchState.Pan, host.Canvas.State);

            host.Device( ).Down(host.Target, TestPoints.CloseTo(host, cluster[0]));
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

    /// <summary>多指书写抬到单指后仍留在 MultiDraw，剩下的人继续画</summary>
    [Fact]
    public void MultiDraw_ReleaseToOneFinger_StaysMultiDraw( )
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
            Assert.Equal(TouchState.MultiDraw, host.Canvas.State);
        });
    }

    /// <summary>MultiDraw 抬到单指后，剩余手指的笔画不被截断，继续追加点直到抬手提交</summary>
    [Fact]
    public void MultiDraw_ReleaseToOneFinger_ContinuesDrawing( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            var a = host.Device( );
            var b = host.Device( );
            var farPoint = TestPoints.FarFrom(host, P1);
            a.Down(host.Target, P1);
            b.Down(host.Target, farPoint);
            a.Move(host.Target, new Point(P1.X + 10, P1.Y));
            Assert.Equal(TouchState.MultiDraw, host.Canvas.State);

            b.Up(host.Target, farPoint);
            Assert.Equal(TouchState.MultiDraw, host.Canvas.State);

            var committed = host.Canvas.Strokes.Count;
            a.Move(host.Target, new Point(P1.X + 60, P1.Y));
            Assert.Equal(committed, host.Canvas.Strokes.Count);

            a.Up(host.Target, new Point(P1.X + 60, P1.Y));
            Assert.Equal(committed + 1, host.Canvas.Strokes.Count);
            Assert.True(host.Canvas.Strokes[^1].StylusPoints.Count >= 3);
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

    /// <summary>控件卸载：在途触摸复位，多指笔画按抬手提交</summary>
    [Fact]
    public void Unloaded_ResetsTouchState( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var finger = host.Device( );
            finger.Down(host.Target, new Point(100, 100));
            Assert.Equal(TouchState.EvalDraw, host.Canvas.State);

            host.Canvas.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));

            Assert.Equal(TouchState.Idle, host.Canvas.State);
        });
    }

    /// <summary>多指作画中重置输入（换页/窗口失活路径）：在途笔画按抬手提交，状态回 Idle</summary>
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

    /// <summary>MultiDraw --> Draw：迁出覆盖编辑模式的接管态时，把编辑模式恢复为当前工具</summary>
    [Fact]
    public void MultiDraw_ReleaseToOneFinger_KeepsEditingModeNone( )
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
            Assert.Equal(TouchState.MultiDraw, host.Canvas.State);
            Assert.Equal(InkCanvasEditingMode.None, host.Canvas.InnerCanvasElement.EditingMode);

            a.Up(host.Target, P1);
            Assert.Equal(TouchState.Idle, host.Canvas.State);
            Assert.Equal(InkCanvasEditingMode.Ink, host.Canvas.InnerCanvasElement.EditingMode);
        });
    }

    /// <summary>双指手势中切换工具：EditingMode 延迟到迁出路径统一应用为当前模式（取代 prevMode/RestoreMode）</summary>
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

    /// <summary>单指按住未起笔时切换工具：迁出 EvalDraw 回 Idle 时应用新模式</summary>
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
