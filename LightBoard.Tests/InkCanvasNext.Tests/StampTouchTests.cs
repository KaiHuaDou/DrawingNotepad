using System.Windows;
using System.Windows.Ink;

using LightBoard.Tests.Toolbar;

namespace InkCanvasNext.Tests;

/// <summary>
/// 盖章模式的手势路由：多指手势照常（MultiDraw 被排除、不出笔画），
/// 未升级为手势的单指序列在抬手位置落章，按下时不落章。
/// 所有测试体在 STA 线程上执行（WPF 控件与触摸输入管线要求）。
/// </summary>
public class StampTouchTests
{
    private static readonly Point P1 = new(100, 100);
    private static readonly Point Close2 = new(180, 140);      // 与 P1 相距 ~89px，恒满足 d <= l
    private static readonly Point Moved = new(300, 200);       // 与 P1 相距 100px > 20px 位移阈值
    private static readonly Point End1 = new(500, 320);        // 第一指的结束点
    private static readonly Point Far2 = new(100, 20_000);     // 与 P1 相距 ~19900px，恒满足 d > l

    [Fact]
    public void SingleFingerTap_StampsAtLiftPoint( )
    {
        StaTest.Run(( ) =>
        {
            using var host = CreateStampHost( );
            Assert.Equal(System.Windows.Controls.InkCanvasEditingMode.None, host.Canvas.InnerCanvasElement.EditingMode);

            var finger = host.Device( );
            finger.Down(host.Target, P1);
            finger.Up(host.Target, End1);

            Assert.Equal(2, host.Canvas.Strokes.Count);
            AssertStampCenter(host.Canvas.Strokes[1], End1);
            Assert.Equal(TouchState.Idle, host.Canvas.State);
        });
    }

    [Fact]
    public void SingleFingerDrag_StampsAtLiftPoint( )
    {
        StaTest.Run(( ) =>
        {
            using var host = CreateStampHost( );
            var finger = host.Device( );

            finger.Down(host.Target, P1);
            finger.Move(host.Target, Moved);
            Assert.Equal(TouchState.Draw, host.Canvas.State);
            finger.Up(host.Target, End1);

            Assert.Equal(2, host.Canvas.Strokes.Count);
            AssertStampCenter(host.Canvas.Strokes[1], End1);
        });
    }

    [Fact]
    public void TwoFingerPinch_GestureWorksAndNoStamp( )
    {
        StaTest.Run(( ) =>
        {
            using var host = CreateStampHost( );
            var finger1 = host.Device( );
            var finger2 = host.Device( );

            finger1.Down(host.Target, P1);
            finger2.Down(host.Target, Close2);
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);

            finger1.Move(host.Target, new Point(120, 80));
            finger2.Move(host.Target, new Point(220, 160));
            finger1.Up(host.Target, new Point(120, 80));
            finger2.Up(host.Target, new Point(220, 160));

            // 双指捏合缩放照常可用，全程不落章
            Assert.Single(host.Canvas.Strokes);
            Assert.Equal(TouchState.Idle, host.Canvas.State);
        });
    }

    [Fact]
    public void MultiDraw_CreatesNoStrokeAndNoStamp( )
    {
        StaTest.Run(( ) =>
        {
            using var host = CreateStampHost( );
            var finger1 = host.Device( );
            var finger2 = host.Device( );

            finger1.Down(host.Target, P1);
            finger2.Down(host.Target, Far2);
            Assert.Equal(TouchState.MultiDraw, host.Canvas.State);

            finger1.Move(host.Target, Moved);
            finger2.Move(host.Target, new Point(200, 19_000));
            finger1.Up(host.Target, End1);
            finger2.Up(host.Target, new Point(300, 19_500));

            // MultiDraw 在盖章期间被排除：不创建笔画，抬手也不残留落章
            Assert.Single(host.Canvas.Strokes);
            Assert.Equal(TouchState.Idle, host.Canvas.State);
        });
    }

    [Fact]
    public void StampExitDuringGesture_DisarmsStamp( )
    {
        StaTest.Run(( ) =>
        {
            using var host = CreateStampHost( );
            var finger = host.Device( );

            finger.Down(host.Target, P1);
            host.Canvas.StampAction = StampAction.None;
            finger.Up(host.Target, End1);

            // 序列中途退出盖章：抬手不落章
            Assert.Single(host.Canvas.Strokes);
            Assert.Equal(TouchState.Idle, host.Canvas.State);
        });
    }

    private static TouchHost CreateStampHost( )
    {
        var host = new TouchHost( );
        host.Canvas.Strokes.Add(TestStrokes.MakeStroke( ));
        TestSelection.ForceSelection(host.Canvas, host.Canvas.Strokes);
        host.Canvas.StampAction = StampAction.Clone;
        return host;
    }

    private static void AssertStampCenter(Stroke stroke, Point expected)
    {
        var bounds = stroke.GetBounds( );
        Assert.Equal(expected.X, bounds.Left + bounds.Width / 2, 6);
        Assert.Equal(expected.Y, bounds.Top + bounds.Height / 2, 6);
    }
}
