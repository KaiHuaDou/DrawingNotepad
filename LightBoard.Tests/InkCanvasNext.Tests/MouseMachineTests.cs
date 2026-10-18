using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace InkCanvasNext.Tests;

/// <summary>
/// 鼠标路径测试：自定义工具（形状/选区/面积擦/盖章）经合成触点进入触摸状态机，
/// 原生工具（墨迹/按笔擦）绕过状态机维持原生收笔。
/// 翻译层用 RaiseEvent 冒烟（位置取真实光标，仅断言状态与 Handled）；
/// 状态机行为用 MousePointer 直驱（坐标精确，与触摸测试同约定）。
/// </summary>
public class MouseMachineTests
{
    private static readonly Point P1 = new(100, 100);

    // ---------- 抬手贴边自动滚动 ----------

    /// <summary>贴下边抬手：0.1 秒动画后视口向下滚动（合成触点与触摸同款）。
    /// 坐标用控件根坐标系（真实鼠标语义）：自动滚动读取的是视口坐标。</summary>
    [Fact]
    public void Mouse_UpAtBottomEdge_AutoScrolls( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.EraseArea;
            var pointer = host.Canvas.MousePointer;
            var before = host.Canvas.OffsetY;
            var bottom = host.Canvas.ActualHeight - 40;

            pointer.Down(new Point(640, 300));
            pointer.Up(new Point(640, bottom));

            PumpAnimation( );

            Assert.True(host.Canvas.OffsetY > before, $"贴下边抬手应向下滚动：before={before} after={host.Canvas.OffsetY}");
        });
    }

    /// <summary>视口中部抬手：不触发滚动。</summary>
    [Fact]
    public void Mouse_UpMidCanvas_DoesNotAutoScroll( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.EraseArea;
            var pointer = host.Canvas.MousePointer;
            var before = host.Canvas.OffsetY;

            pointer.Down(new Point(640, host.Canvas.ActualHeight / 2));
            pointer.Up(new Point(640, host.Canvas.ActualHeight / 2));

            PumpAnimation( );

            Assert.Equal(before, host.Canvas.OffsetY);
        });
    }

    /// <summary>推帧驱动 0.1 秒自动滚动动画至完成。</summary>
    private static void PumpAnimation( )
    {
        var clock = Stopwatch.StartNew( );
        while (clock.ElapsedMilliseconds < 400)
        {
            Dispatcher.CurrentDispatcher.Invoke(( ) => { }, DispatcherPriority.Background);
            Thread.Sleep(10);
        }
    }

    // ---------- 翻译层冒烟 ----------

    [Fact]
    public void ShapeTool_MouseDown_EntersEvalDraw_AndHandled( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.Line;

            host.Target.RaiseEvent(LeftButton(Mouse.PreviewMouseDownEvent));
            Assert.Equal(TouchState.EvalDraw, host.Canvas.State);

            host.Target.RaiseEvent(LeftButton(Mouse.PreviewMouseUpEvent));
            Assert.Equal(TouchState.Idle, host.Canvas.State);
        });
    }

    [Fact]
    public void InkTool_MouseDown_BypassesMachine( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.Ink;

            host.Target.RaiseEvent(LeftButton(Mouse.PreviewMouseDownEvent));
            Assert.Equal(TouchState.Idle, host.Canvas.State);
        });
    }

    // ---------- 状态机集成（MousePointer 直驱） ----------

    [Fact]
    public void ShapeLine_Mouse_CommitsStrokeAtUp( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.Line;
            var pointer = host.Canvas.MousePointer;
            var moved = TestPoints.BeyondDisplacement(host, P1);

            pointer.Down(P1);
            pointer.Move(moved);
            Assert.Equal(TouchState.Draw, host.Canvas.State);
            pointer.Up(moved);

            Assert.Equal(TouchState.Idle, host.Canvas.State);
            Assert.Single(host.Canvas.Strokes);
        });
    }

    [Fact]
    public void AreaErase_Mouse_ErasesOnlyAfterDisplacementThreshold( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.EraseArea;
            host.Canvas.Strokes.Add(MakeStroke(new Point(170, 100), new Point(190, 100)));
            var pointer = host.Canvas.MousePointer;
            // 起点选 (155, 100)：越过位移阈值后橡皮圆心落在 (186, 100)，直径 50 恰好完整覆盖短笔画
            var downAt = RootOf(host, new Point(155, 100));
            var moved = TestPoints.BeyondDisplacement(host, downAt);

            pointer.Down(downAt);
            Assert.Equal(TouchState.EvalDraw, host.Canvas.State);
            Assert.Single(host.Canvas.Strokes); // EvalDraw 仅显示橡皮，不擦除

            pointer.Move(moved);
            Assert.Equal(TouchState.Draw, host.Canvas.State);
            Assert.Empty(host.Canvas.Strokes);
        });
    }

    [Fact]
    public void Select_Mouse_LassoSelectsStroke( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.Select;
            host.Canvas.Strokes.Add(MakeStroke(new Point(200, 150), new Point(250, 350)));
            var pointer = host.Canvas.MousePointer;

            pointer.Down(RootOf(host, new Point(150, 100)));
            pointer.Move(RootOf(host, new Point(300, 100)));
            pointer.Move(RootOf(host, new Point(300, 400)));
            pointer.Move(RootOf(host, new Point(150, 400)));
            pointer.Move(RootOf(host, new Point(150, 100)));
            pointer.Up(RootOf(host, new Point(150, 100)));

            Assert.Equal(TouchState.Idle, host.Canvas.State);
            Assert.NotNull(host.Canvas.GetSelectionScreenBounds(host.Canvas));
        });
    }

    /// <summary>盖章在抬手位置落章（合流后的统一时机）：按下不落，抬手克隆已选笔画。</summary>
    [Fact]
    public void Stamp_Mouse_CommitsAtUp_NotAtDown( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.Select;
            host.Canvas.Strokes.Add(MakeStroke(new Point(200, 150), new Point(250, 350)));

            var pointer = host.Canvas.MousePointer;
            LassoSelectStroke(host, pointer);
            host.Canvas.StampAction = StampAction.Clone;

            var downAt = RootOf(host, P1);
            pointer.Down(downAt);
            Assert.Equal(TouchState.EvalDraw, host.Canvas.State);
            Assert.Single(host.Canvas.Strokes); // 按下不落章

            pointer.Up(TestPoints.CloseTo(host, downAt));

            Assert.Equal(TouchState.Idle, host.Canvas.State);
            Assert.Equal(2, host.Canvas.Strokes.Count); // 抬手落章，克隆一份
        });
    }

    /// <summary>用单指套索选中一条经过 (150,100)-(360,400) 区域的笔画。</summary>
    private static void LassoSelectStroke(TouchHost host, MouseTouchDevice pointer)
    {
        pointer.Down(RootOf(host, new Point(150, 100)));
        pointer.Move(RootOf(host, new Point(300, 100)));
        pointer.Move(RootOf(host, new Point(300, 400)));
        pointer.Move(RootOf(host, new Point(150, 400)));
        pointer.Move(RootOf(host, new Point(150, 100)));
        pointer.Up(RootOf(host, new Point(150, 100)));

        Assert.Equal(TouchState.Idle, host.Canvas.State);
        Assert.True(host.Canvas.HasSelection);
    }

    /// <summary>合成触点按布局换算坐标：把画布坐标反推为控件根坐标，供设备输入使用。</summary>
    private static Point RootOf(TouchHost host, Point canvasPoint)
    {
        return host.Canvas.InnerCanvasElement.TranslatePoint(canvasPoint, host.Canvas);
    }

    private static MouseButtonEventArgs LeftButton(RoutedEvent routedEvent)
    {
        return new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
        {
            RoutedEvent = routedEvent
        };
    }

    private static Stroke MakeStroke(params Point[] points)
    {
        return new Stroke(
            [.. points.Select(p => new StylusPoint(p.X, p.Y, 0.5f))],
            new DrawingAttributes { Color = Colors.Black, Width = 3, Height = 3 });
    }
}
