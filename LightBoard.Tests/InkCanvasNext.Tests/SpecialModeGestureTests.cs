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

    /// <summary>双指几何断言用笔画：中间两个触点恰好落在两指基线上。</summary>
    private static readonly Point[] PinchStrokePoints = [new(180, 96), new(200, 100), new(300, 100), new(320, 104)];

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
    public void SelectMode_Stamp_SingleFingerSkipsSelection( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.Select;
            host.Canvas.StampAction = StampAction.Clone;

            var finger = host.Device( );
            finger.Down(host.Target, P1);
            // 盖章期间选区入口被跳过：单指进入单指绘制上下文，抬手落章（此处无选区，落章为空操作）
            Assert.Equal(TouchState.EvalDraw, host.Canvas.State);
            finger.Up(host.Target, P1);

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

    /// <summary>
    /// 双指相似变换：基线两指 (200,100)/(300,100) 移到 (280,10)/(280,210)，
    /// 即绕基线中点 (250,100) 旋转 90 度、间距放大 2 倍、中点平移到 (280,110)。
    /// 基线瞬间位于第一指处的笔画触点必须落在第一指终点，其余触点按同一相似变换映射。
    /// </summary>
    [Fact]
    public void SelectMode_Pinch_RotateScaleTranslate_SelectionTracksFingers( )
    {
        StaTest.Run(( ) =>
        {
            using var host = CreateSelectHost(PinchStrokePoints);

            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, new Point(200, 100));
            b.Down(host.Target, new Point(300, 100));

            a.Move(host.Target, new Point(280, 10));
            b.Move(host.Target, new Point(280, 210));
            a.Up(host.Target, new Point(280, 10));
            b.Up(host.Target, new Point(280, 210));

            var points = host.Canvas.Strokes[0].StylusPoints;
            AssertPoint(points[0], 288, -30);
            AssertPoint(points[1], 280, 10);
            AssertPoint(points[2], 280, 210);
            AssertPoint(points[3], 272, 250);
        });
    }

    /// <summary>捏合中抬起一指后，剩余指继续拖动，位移从抬指瞬间开始累计（不跳变、不吞掉这一帧）。</summary>
    [Fact]
    public void SelectMode_PinchThenLiftOneFinger_DragContinuesFromRemainingFinger( )
    {
        StaTest.Run(( ) =>
        {
            using var host = CreateSelectHost(PinchStrokePoints);

            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, new Point(200, 100));
            b.Down(host.Target, new Point(300, 100));

            b.Up(host.Target, new Point(300, 100));
            Assert.Equal(TouchState.Selection, host.Canvas.State);

            a.Move(host.Target, new Point(210, 110));
            a.Up(host.Target, new Point(210, 110));

            AssertPoints(host.Canvas.Strokes[0].StylusPoints, new Point(190, 106), new Point(210, 110), new Point(310, 110), new Point(330, 114));
        });
    }

    /// <summary>手柄缩放手势中落下第二指即升级为双指相似变换：随后两指纯平移，选区位移等于平移量。</summary>
    [Fact]
    public void SelectMode_HandleGestureThenSecondFinger_BecomesPinch( )
    {
        StaTest.Run(( ) =>
        {
            using var host = CreateSelectHost(PinchStrokePoints);

            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, new Point(320, 100));
            b.Down(host.Target, new Point(200, 100));

            a.Move(host.Target, new Point(335, 125));
            b.Move(host.Target, new Point(215, 125));
            a.Up(host.Target, new Point(335, 125));
            b.Up(host.Target, new Point(215, 125));

            AssertPoints(host.Canvas.Strokes[0].StylusPoints, new Point(195, 121), new Point(215, 125), new Point(315, 125), new Point(335, 129));
        });
    }

    /// <summary>一次"双指捏合 -> 抬一指 -> 拖动"的手势只产生一条撤销记录。</summary>
    [Fact]
    public void SelectMode_PinchWithRebase_UndoRestoresWholeGesture( )
    {
        StaTest.Run(( ) =>
        {
            using var host = CreateSelectHost(PinchStrokePoints);

            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, new Point(200, 100));
            b.Down(host.Target, new Point(300, 100));
            a.Move(host.Target, new Point(280, 10));
            b.Move(host.Target, new Point(280, 210));
            b.Up(host.Target, new Point(280, 210));
            a.Move(host.Target, new Point(290, 20));
            a.Up(host.Target, new Point(290, 20));

            Assert.True(host.Canvas.CanUndo);
            host.Canvas.Undo( );

            AssertPoints(host.Canvas.Strokes[0].StylusPoints, PinchStrokePoints);
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
    public void PanZoom_TwoFingers_SymmetricPinchChangesScale( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, P1);           // (100, 100)
            b.Down(host.Target, Close2);       // (180, 140)，两指中点 (140, 120)
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);
            Assert.Equal(1.0, host.Canvas.CurrentScale);

            // 两指围绕初始中点对称张开，中点位移为 0，不触发位移锁死
            a.Move(host.Target, new Point(80, 80));
            b.Move(host.Target, new Point(200, 160));
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);
            Assert.True(host.Canvas.CurrentScale > 1.0, "对称张开应放大画布");
        });
    }

    [Fact]
    public void PanZoom_TranslateBeyondThreshold_LocksZoom( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, new Point(100, 100));
            b.Down(host.Target, new Point(200, 100)); // 两指中点 (150, 100)，间距 100
            Assert.Equal(1.0, host.Canvas.CurrentScale);

            // 两指同向上移 40px：中点位移 40 > 30，视为纯平移，缩放锁死
            a.Move(host.Target, new Point(100, 60));
            b.Move(host.Target, new Point(200, 60));
            Assert.Equal(1.0, host.Canvas.CurrentScale);

            // 大幅张开（间距 300，未锁定时缩放 3 倍）仍不更新缩放
            a.Move(host.Target, new Point(0, 60));
            b.Move(host.Target, new Point(300, 60));
            Assert.Equal(1.0, host.Canvas.CurrentScale);
        });
    }

    [Fact]
    public void PanZoom_FingersBelowMinDistance_LocksZoom( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, new Point(100, 100));
            b.Down(host.Target, new Point(200, 100)); // 两指中点 (150, 100)，间距 100
            Assert.Equal(1.0, host.Canvas.CurrentScale);

            // 收拢一帧（中点位移 22.5，间距 55，未触发锁死）：先产生缩放
            a.Move(host.Target, new Point(145, 100));
            Assert.NotEqual(1.0, host.Canvas.CurrentScale);

            // 间距 10 < 24 → 距离锁死
            b.Move(host.Target, new Point(155, 100));
            var frozen = host.Canvas.CurrentScale;

            // 大范围张开（间距 300）仍不更新缩放
            a.Move(host.Target, new Point(0, 100));
            b.Move(host.Target, new Point(300, 100));
            Assert.Equal(frozen, host.Canvas.CurrentScale);
        });
    }

    /// <summary>在 Select 模式下建好宿主，并用单指套索选中一条经过指定触点的笔画。</summary>
    private static TouchHost CreateSelectHost(params Point[] strokePoints)
    {
        var host = new TouchHost( );
        host.Canvas.Mode = InkCanvasNextMode.Select;
        host.Canvas.Strokes.Add(MakeStroke(strokePoints));

        var lasso = host.Device( );
        lasso.Down(host.Target, new Point(150, 50));
        lasso.Move(host.Target, new Point(360, 50));
        lasso.Move(host.Target, new Point(360, 160));
        lasso.Move(host.Target, new Point(150, 160));
        lasso.Move(host.Target, new Point(150, 50));
        lasso.Up(host.Target, new Point(150, 50));

        Assert.Single(host.Canvas.SelectedStrokes);
        return host;
    }

    private static void AssertPoints(StylusPointCollection points, params Point[] expected)
    {
        Assert.Equal(expected.Length, points.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            AssertPoint(points[i], expected[i].X, expected[i].Y);
        }
    }

    private static void AssertPoint(StylusPoint point, double x, double y)
    {
        Assert.Equal(x, point.X, 6);
        Assert.Equal(y, point.Y, 6);
    }

    private static Stroke MakeStroke(params Point[] points)
    {
        return new Stroke(
            [.. points.Select(p => new StylusPoint(p.X, p.Y, 0.5f))],
            new DrawingAttributes { Color = Colors.Black, Width = 3, Height = 3 });
    }
}
