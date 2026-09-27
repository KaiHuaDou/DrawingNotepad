using System;
using System.Windows;
using System.Windows.Ink;

namespace InkCanvasNext.Tests;

/// <summary>
/// 形状绘制的多指行为：形状只在单指上下文（EvalDraw/Draw）存活；
/// Draw 落第二指无条件升级 MultiDraw 并按当前预览提交形状，EvalDraw 落第二指捏合缩放优先、仍放弃形状。
/// 所有测试体在 STA 线程上执行（WPF 控件与触摸输入管线要求）。
/// </summary>
public class ShapeTouchTests
{
    private static readonly Point P1 = new(100, 100);
    private static readonly Point Close2 = new(180, 140);      // 与 P1 相距 ~89px，恒满足 d <= l
    private static readonly Point Moved = new(300, 200);       // 与 P1 相距 100px > 20px 位移阈值
    private static readonly Point End1 = new(500, 320);        // 第一指的结束点
    private static readonly Point Stray2 = new(260, 560);      // 附加指位置

    private static Point EndOf(Stroke stroke)
    {
        var points = stroke.StylusPoints;
        return new Point(points[^1].X, points[^1].Y);
    }

    [Fact]
    public void Draw_SecondFinger_CommitsShapeAndTurnsMultiDraw( )
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

            // 第二指落下即无条件升级 MultiDraw：在途形状按预览提交，两指转入多指画笔
            Assert.Equal(TouchState.MultiDraw, host.Canvas.State);

            finger2.Move(host.Target, Stray2);
            finger2.Up(host.Target, Stray2);
            finger1.Move(host.Target, End1);
            finger1.Up(host.Target, End1);

            // 形状笔画（P1 -> Moved）先提交；f2 抬起提交其多指笔画并迁回 Draw，
            // EndMultiTouch 把 f1 未续画的多指笔画按现有点位一并收尾，f1 后续移动不再产生笔画
            Assert.Equal(3, host.Canvas.Strokes.Count);
            Assert.Equal(P1, host.Canvas.Strokes[0].StylusPoints[0].ToPoint( ));
            Assert.Equal(Moved, EndOf(host.Canvas.Strokes[0]));
            Assert.Equal(Close2, host.Canvas.Strokes[1].StylusPoints[0].ToPoint( ));
            Assert.Equal(Stray2, EndOf(host.Canvas.Strokes[1]));
            Assert.Equal(Moved, EndOf(host.Canvas.Strokes[2]));
            Assert.Equal(TouchState.Idle, host.Canvas.State);
        });
    }

    [Fact]
    public void Draw_MoreFingers_AllTurnMultiDraw( )
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

            // 形状按预览提交（P1 -> Moved）；f1 的多指笔画续接到 End1；f2/f3 仍在途不提交
            Assert.Equal(2, host.Canvas.Strokes.Count);
            Assert.Equal(P1, host.Canvas.Strokes[0].StylusPoints[0].ToPoint( ));
            Assert.Equal(End1, EndOf(host.Canvas.Strokes[1]));
        });
    }

    [Fact]
    public void Draw_SecondFinger_CommitsCircle( )
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
            finger2.Up(host.Target, Stray2);

            // 圆形笔画（CircleSegments + 1 点）按预览提交，另有两指的多指笔画
            Assert.Equal(3, host.Canvas.Strokes.Count);
            Assert.Contains(host.Canvas.Strokes, stroke => stroke.StylusPoints.Count == InkCanvasNext.CircleSegments + 1);
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
