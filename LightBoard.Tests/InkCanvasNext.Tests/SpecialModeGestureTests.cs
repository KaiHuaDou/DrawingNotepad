using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasNext.Tests;

/// <summary>
/// 特殊模式（Select/EraseArea/EraseStroke/形状/盖章）下手势的可用性：
/// 手势接管（平移/缩放/擦除）在覆盖工具模式下仍可用，且不产生错误的墨迹/选区副作用。
/// </summary>
public class SpecialModeGestureTests
{
    private static readonly Point P1 = new(100, 100);
    private static readonly Point Close2 = new(180, 140);
    private static readonly Point Close3 = new(300, 100);
    private static readonly Point Close4 = new(350, 120);
    private static readonly Point Close5 = new(400, 140);
    private static readonly Point Moved = new(100, 200);

    // ---------- Select 模式 ----------

    [Fact]
    public void SelectMode_TwoFingers_StaysSelection( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.Select;

            var a = host.Device( );
            a.Down(host.Target, P1);
            Assert.Equal(TouchState.Selection, host.Canvas.State);

            host.Device( ).Down(host.Target, Close2);
            Assert.Equal(TouchState.Selection, host.Canvas.State);

            a.Move(host.Target, new Point(200, 200));
            Assert.Equal(TouchState.Selection, host.Canvas.State);
        });
    }

    [Fact]
    public void SelectMode_ThreeFingers_EntersPan( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.Select;

            host.Device( ).Down(host.Target, P1);
            host.Device( ).Down(host.Target, Close2);
            host.Device( ).Down(host.Target, Close3);
            Assert.Equal(TouchState.Pan, host.Canvas.State);
        });
    }

    [Fact]
    public void SelectMode_FiveFingers_EntersEraser( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.Select;

            host.Device( ).Down(host.Target, P1);
            host.Device( ).Down(host.Target, Close2);
            host.Device( ).Down(host.Target, Close3);
            host.Device( ).Down(host.Target, Close4);
            host.Device( ).Down(host.Target, Close5);
            Assert.Equal(TouchState.Eraser, host.Canvas.State);
        });
    }

    [Fact]
    public void SelectMode_SingleFinger_DoesNotDrawStroke( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.Select;

            var finger = host.Device( );
            finger.Down(host.Target, P1);
            finger.Move(host.Target, Moved);
            finger.Up(host.Target, Moved);
            Assert.Equal(TouchState.Idle, host.Canvas.State);
            Assert.Empty(host.Canvas.Strokes);
        });
    }

    [Fact]
    public void SelectMode_StampConsumesTouch_StaysIdle( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.Select;
            host.Canvas.StampAction = StampAction.Clone;

            host.Device( ).Down(host.Target, P1);
            Assert.Equal(TouchState.Idle, host.Canvas.State);
            Assert.Empty(host.Canvas.Strokes);
        });
    }

    [Fact]
    public void SelectMode_LassoSelectsStroke_ThenPinchKeepsSelection( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.Select;
            host.Canvas.Strokes.Add(MakeStroke(new Point(200, 150), new Point(250, 350)));

            // 单指绕圈套索选中笔画
            var finger = host.Device( );
            finger.Down(host.Target, new Point(150, 100));
            finger.Move(host.Target, new Point(300, 100));
            finger.Move(host.Target, new Point(300, 400));
            finger.Move(host.Target, new Point(150, 400));
            finger.Move(host.Target, new Point(150, 100));
            finger.Up(host.Target, new Point(150, 100));
            Assert.Equal(TouchState.Idle, host.Canvas.State);
            Assert.NotNull(host.Canvas.GetSelectionScreenBounds(host.Canvas));

            // 已有选区后双指缩放/旋转仍保持 Selection，松开后选区保留
            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, new Point(225, 250));
            Assert.Equal(TouchState.Selection, host.Canvas.State);
            b.Down(host.Target, new Point(235, 250));
            Assert.Equal(TouchState.Selection, host.Canvas.State);

            a.Move(host.Target, new Point(245, 260));
            Assert.Equal(TouchState.Selection, host.Canvas.State);

            a.Up(host.Target, new Point(245, 260));
            b.Up(host.Target, new Point(235, 250));
            Assert.Equal(TouchState.Idle, host.Canvas.State);
            Assert.NotNull(host.Canvas.GetSelectionScreenBounds(host.Canvas));
        });
    }

    // ---------- 橡皮模式 ----------

    [Fact]
    public void EraseAreaMode_SingleFinger_ErasesStroke( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.EraseArea;
            host.Canvas.Strokes.Add(MakeStroke(new Point(170, 100), new Point(190, 100)));

            var finger = host.Device( );
            finger.Down(host.Target, new Point(165, 100));
            Assert.Equal(TouchState.EvalDraw, host.Canvas.State);
            Assert.Single(host.Canvas.Strokes); // EvalDraw 仅显示橡皮，不擦除

            finger.Move(host.Target, new Point(186, 100)); // 移过位移阈值进入 Draw 才开始擦除
            Assert.Equal(TouchState.Draw, host.Canvas.State);
            Assert.Empty(host.Canvas.Strokes);
        });
    }

    [Fact]
    public void EraseAreaMode_TwoFingers_EntersPanZoom( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.EraseArea;

            host.Device( ).Down(host.Target, P1);
            host.Device( ).Down(host.Target, Close2);
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);
        });
    }

    [Fact]
    public void EraseStrokeMode_TwoFingers_EntersPanZoom( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.EraseStroke;

            host.Device( ).Down(host.Target, P1);
            host.Device( ).Down(host.Target, Close2);
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);
        });
    }

    [Fact]
    public void PalmEraser_FiveFingers_MoveErasesStroke( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Strokes.Add(MakeStroke(new Point(266, 120), new Point(300, 120)));

            host.Device( ).Down(host.Target, P1);
            host.Device( ).Down(host.Target, Close2);
            host.Device( ).Down(host.Target, Close3);
            host.Device( ).Down(host.Target, Close4);
            host.Device( ).Down(host.Target, Close5);

            Assert.Equal(TouchState.Eraser, host.Canvas.State);
            Assert.Empty(host.Canvas.Strokes);
        });
    }

    // ---------- 形状模式 ----------

    [Fact]
    public void ShapeLineMode_SingleFinger_CommitsStroke( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.Line;

            var finger = host.Device( );
            finger.Down(host.Target, P1);
            finger.Move(host.Target, Moved);
            finger.Up(host.Target, Moved);

            Assert.Equal(TouchState.Idle, host.Canvas.State);
            Assert.Single(host.Canvas.Strokes);
        });
    }

    [Fact]
    public void ShapeCircleMode_SingleFinger_CommitsStroke( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.Circle;

            var finger = host.Device( );
            finger.Down(host.Target, P1);
            finger.Move(host.Target, new Point(200, 100));
            finger.Up(host.Target, new Point(200, 100));

            Assert.Equal(TouchState.Idle, host.Canvas.State);
            Assert.Single(host.Canvas.Strokes);
        });
    }

    [Fact]
    public void ShapeLineMode_SecondFinger_TakesOverGesture_NoStroke( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.Line;

            var a = host.Device( );
            a.Down(host.Target, P1);
            host.Device( ).Down(host.Target, Close2);
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);

            a.Up(host.Target, P1);
            Assert.Empty(host.Canvas.Strokes);
        });
    }

    // ---------- PanZoom 手势可用性 ----------

    [Fact]
    public void PanZoom_TwoFingers_MoveZoomsCanvas( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, P1);
            b.Down(host.Target, Close2);
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);
            Assert.Equal(1.0, host.Canvas.CurrentScale);

            a.Move(host.Target, new Point(200, 200));
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);
            Assert.NotEqual(1.0, host.Canvas.CurrentScale);
        });
    }

    private static Stroke MakeStroke(Point a, Point b)
    {
        return new Stroke(
            [new StylusPoint(a.X, a.Y, 0.5f), new StylusPoint(b.X, b.Y, 0.5f)],
            new DrawingAttributes { Color = Colors.Black, Width = 3, Height = 3 });
    }
}
