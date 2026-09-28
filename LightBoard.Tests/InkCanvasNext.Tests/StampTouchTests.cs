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
    private static readonly Point End1 = new(500, 320);        // 第一指的结束点

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
            finger.Move(host.Target, TestPoints.BeyondDisplacement(host, P1));
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
            finger2.Down(host.Target, TestPoints.CloseTo(host, P1));
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
            var farPoint = TestPoints.FarFrom(host, P1);

            finger1.Down(host.Target, P1);
            finger2.Down(host.Target, farPoint);
            Assert.Equal(TouchState.MultiDraw, host.Canvas.State);

            finger1.Move(host.Target, TestPoints.BeyondDisplacement(host, P1));
            finger2.Move(host.Target, new Point(farPoint.X, farPoint.Y + 500));
            finger1.Up(host.Target, End1);
            finger2.Up(host.Target, new Point(farPoint.X + 100, farPoint.Y + 600));

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

    /// <summary>武装随手势解除、随下一单指序列重新武装：捏合手势不落章，其后的单击照常落章。</summary>
    [Fact]
    public void PinchGesture_Disarms_NextTapRestamps( )
    {
        StaTest.Run(( ) =>
        {
            using var host = CreateStampHost( );

            var secondPoint = TestPoints.CloseTo(host, P1);
            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, P1);
            b.Down(host.Target, secondPoint);
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);

            a.Up(host.Target, P1);
            b.Up(host.Target, secondPoint);
            Assert.Equal(TouchState.Idle, host.Canvas.State);
            Assert.Single(host.Canvas.Strokes);

            var finger = host.Device( );
            finger.Down(host.Target, End1);
            finger.Up(host.Target, TestPoints.BeyondDisplacement(host, P1));
            Assert.Equal(2, host.Canvas.Strokes.Count);
            AssertStampCenter(host.Canvas.Strokes[1], TestPoints.BeyondDisplacement(host, P1));
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
