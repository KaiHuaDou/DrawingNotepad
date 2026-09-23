using System;
using System.Windows;
using System.Windows.Ink;

namespace InkCanvasNext.Tests;

/// <summary>
/// 形状绘制的多指行为：形状只由插入序第一指驱动，其余手指的移动与抬起均被忽略。
/// 所有测试体在 STA 线程上执行（WPF 控件与触摸输入管线要求）。
/// </summary>
public class ShapeTouchTests
{
    private static readonly Point P1 = new(100, 100);
    private static readonly Point Close2 = new(180, 140);      // 与 P1 相距 ~89px，恒满足 d <= l
    private static readonly Point Moved = new(300, 200);       // 与 P1 相距 100px > 20px 位移阈值
    private static readonly Point End1 = new(500, 320);        // 第一指的结束点
    private static readonly Point Stray2 = new(260, 560);      // 其余手指，全程不得牵引形状

    private static Point EndOf(Stroke stroke)
    {
        var points = stroke.StylusPoints;
        return new Point(points[^1].X, points[^1].Y);
    }

    [Fact]
    public void Draw_SecondFingerMoveAndLift_AreIgnored( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.Line;
            var finger1 = host.Device( );
            var finger2 = host.Device( );

            finger1.Down(host.Target, P1);
            finger1.Move(host.Target, Moved); // EvalDraw -> Draw，形状在途
            finger2.Down(host.Target, Close2);
            finger2.Move(host.Target, Stray2);
            finger2.Up(host.Target, Stray2);

            // 第二指移动与抬起均被忽略：不提交、不牵引终点，形状继续跟随第一指
            Assert.Empty(host.Canvas.Strokes);
            Assert.Equal(TouchState.Draw, host.Canvas.State);

            finger1.Move(host.Target, End1);
            finger1.Up(host.Target, End1);

            var stroke = Assert.Single(host.Canvas.Strokes);
            Assert.Equal(End1, EndOf(stroke));
        });
    }

    [Fact]
    public void Draw_ThirdFingerMove_AreIgnored( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.Line;
            var finger1 = host.Device( );
            var finger2 = host.Device( );
            var finger3 = host.Device( );

            finger1.Down(host.Target, P1);
            finger1.Move(host.Target, Moved);
            finger2.Down(host.Target, Close2);
            finger3.Down(host.Target, Stray2);
            finger3.Move(host.Target, new Point(320, 300));
            finger1.Move(host.Target, End1);
            finger1.Up(host.Target, End1);

            var stroke = Assert.Single(host.Canvas.Strokes);
            Assert.Equal(End1, EndOf(stroke));
        });
    }

    [Fact]
    public void Draw_SecondFingerDoesNotSteerCircle( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.Circle;
            var finger1 = host.Device( );
            var finger2 = host.Device( );

            finger1.Down(host.Target, P1);
            finger1.Move(host.Target, new Point(200, 100));
            finger2.Down(host.Target, Close2);
            finger2.Move(host.Target, Stray2);
            finger1.Up(host.Target, new Point(200, 100));

            // 圆心锚定第一指按下点，半径只由第一指决定（此处 100）
            var stroke = Assert.Single(host.Canvas.Strokes);
            Assert.Equal(200, stroke.StylusPoints[0].X, 6);
            Assert.Equal(100, stroke.StylusPoints[0].Y, 6);
        });
    }

    [Fact]
    public void Draw_SingleFinger_CommitsAtLiftPoint( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.Line;
            var finger = host.Device( );

            finger.Down(host.Target, P1);
            finger.Move(host.Target, Moved);
            finger.Up(host.Target, End1);

            var stroke = Assert.Single(host.Canvas.Strokes);
            Assert.Equal(End1, EndOf(stroke));
        });
    }

    [Fact]
    public void EvalDraw_SecondFinger_CancelsShape_EntersPanZoom( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.Line;
            host.Device( ).Down(host.Target, P1);
            host.Device( ).Down(host.Target, Close2);

            // 未移动即落第二指：捏合缩放优先，形状预览放弃
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);
            Assert.Empty(host.Canvas.Strokes);
        });
    }
}
