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

            host.Device( ).Down(host.Target, TestPoints.CloseTo(host, P1));
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
            var cluster = TestPoints.CloseCluster(host, P1, 3);

            host.Device( ).Down(host.Target, cluster[0]);
            host.Device( ).Down(host.Target, cluster[1]);
            host.Device( ).Down(host.Target, cluster[2]);
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
            var cluster = TestPoints.CloseCluster(host, P1, 5);

            foreach (var point in cluster)
            {
                host.Device( ).Down(host.Target, point);
            }

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
            finger.Move(host.Target, TestPoints.BeyondDisplacement(host, P1));
            finger.Up(host.Target, TestPoints.BeyondDisplacement(host, P1));
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

    /// <summary>盖章期间双指并拢：选区入口被压制，双指照常迁入 PanZoom，全程不落章、不产生选区。</summary>
    [Fact]
    public void SelectMode_Stamp_TwoCloseFingers_EntersPanZoom( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.Select;
            host.Canvas.StampAction = StampAction.Clone;

            var secondPoint = TestPoints.CloseTo(host, P1);
            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, P1);
            Assert.Equal(TouchState.EvalDraw, host.Canvas.State);

            b.Down(host.Target, secondPoint);
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);

            a.Up(host.Target, P1);
            b.Up(host.Target, secondPoint);
            Assert.Equal(TouchState.Idle, host.Canvas.State);
            Assert.Empty(host.Canvas.Strokes);
            Assert.Null(host.Canvas.GetSelectionScreenBounds(host.Canvas));
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

    /// <summary>套索在途时落第三指：迁入 Pan 前结算套索（选区保留），随后三指平移画布。</summary>
    [Fact]
    public void SelectMode_LassoThenThirdFinger_EntersPan_KeepsLassoSelection( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.Select;
            host.Canvas.Strokes.Add(MakeStroke(new Point(200, 150), new Point(250, 350)));

            var lassoPoint = new Point(150, 100);
            var lasso = host.Device( );
            lasso.Down(host.Target, lassoPoint);
            lasso.Move(host.Target, new Point(300, 100));
            lasso.Move(host.Target, new Point(300, 400));
            lasso.Move(host.Target, new Point(150, 400));
            lasso.Move(host.Target, lassoPoint);
            Assert.Equal(TouchState.Selection, host.Canvas.State);

            // 两根附加指彼此与套索指的间距均不超过 l/4：三指并拢迁入 Pan
            var secondPoint = TestPoints.CloseTo(host, lassoPoint);
            var thirdPoint = TestPoints.CloseTo(host, secondPoint);
            var second = host.Device( );
            var third = host.Device( );
            second.Down(host.Target, secondPoint);
            third.Down(host.Target, thirdPoint);
            Assert.Equal(TouchState.Pan, host.Canvas.State);

            lasso.Up(host.Target, lassoPoint);
            second.Up(host.Target, secondPoint);
            third.Up(host.Target, thirdPoint);
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

    /// <summary>缩放手柄拖过锚点（对侧中点）不再翻转选区：单轴因子钳在 MinSelectionScale，
    /// 笔画停在被压缩的一侧而非镜像到锚点另一侧（x 不越过锚点、y 不变）。
    /// 手柄与锚点由包围盒推导，GetBounds 含笔画半宽外扩，期望值在手势前按真实包围盒捕获。</summary>
    [Fact]
    public void SelectMode_ScaleHandlePastAnchor_ClampsToMinScale( )
    {
        StaTest.Run(( ) =>
        {
            using var host = CreateSelectHost(
                new Point(180, 60), new Point(200, 100), new Point(300, 100), new Point(320, 140));

            // R 手柄在右缘中点、锚点在左中；宽 3 的笔画使包围盒左缘在 178.5 而非 180
            var left = host.Canvas.Strokes[0].GetBounds( ).Left;
            var xs = host.Canvas.Strokes[0].StylusPoints.Select(p => p.X).ToArray( );
            var ys = host.Canvas.Strokes[0].StylusPoints.Select(p => p.Y).ToArray( );
            const double minScale = InkCanvasNext.MinSelectionScale;

            var a = host.Device( );
            a.Down(host.Target, new Point(320, 100));
            // 拖到锚点左侧：原始因子 -78.5/141.5 ≈ -0.55，钳制后为 MinSelectionScale
            a.Move(host.Target, new Point(100, 100));
            a.Up(host.Target, new Point(100, 100));

            var points = host.Canvas.Strokes[0].StylusPoints;
            Assert.Equal(xs.Length, points.Count);
            for (var i = 0; i < xs.Length; i++)
            {
                Assert.Equal(left + (xs[i] - left) * minScale, points[i].X, 9);
                Assert.Equal(ys[i], points[i].Y, 9);
            }
        });
    }

    /// <summary>旋转手柄拖拽：手柄命中（选区正上方 40）、角度换算（弧度差 × RadToDeg）、
    /// 绕选区中心整体旋转 +90°（手从正上方移到右方同半径处），旋转手柄沿鼠标角度等半径跟随、抬手清空。</summary>
    [Fact]
    public void SelectMode_RotateHandle_NinetyDegrees_RotatesStrokeAroundCenter( )
    {
        StaTest.Run(( ) =>
        {
            using var host = CreateSelectHost(PinchStrokePoints);

            var a = host.Device( );
            // 旋转手柄悬于选区正上方（zoom = 1 时间距 40，bounds.Top = 94.5 含笔画半宽外扩）
            a.Down(host.Target, new Point(250, 54.5));
            // 移到选区右中（与起始点同在半径 45.5 的圆上）：+90°
            a.Move(host.Target, new Point(295.5, 100));
            var live = host.Canvas.RotateHandleLive;
            a.Up(host.Target, new Point(295.5, 100));

            // 绕选区中心 (250, 100) 旋转 +90°：相对坐标 (x, y) → (−y, x)
            var points = host.Canvas.Strokes[0].StylusPoints;
            AssertPoint(points[0], 254, 30);
            AssertPoint(points[1], 250, 50);
            AssertPoint(points[2], 250, 150);
            AssertPoint(points[3], 246, 170);

            Assert.NotNull(live);
            Assert.Equal(295.5, live!.Value.X, 9);
            Assert.Equal(100, live.Value.Y, 9);
            Assert.Null(host.Canvas.RotateHandleLive);
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

            var radius = host.Canvas.EraserDiameter / 2;
            var startPoint = new Point(165, 100);
            var drawPoint = TestPoints.BeyondDisplacement(host, startPoint);
            host.Canvas.Strokes.Add(MakeStroke(
                new Point(drawPoint.X - radius * 0.6, 100),
                new Point(drawPoint.X + radius * 0.6, 100)));

            var finger = host.Device( );
            finger.Down(host.Target, startPoint);
            Assert.Equal(TouchState.EvalDraw, host.Canvas.State);
            Assert.Single(host.Canvas.Strokes); // EvalDraw 仅显示橡皮，不擦除

            finger.Move(host.Target, drawPoint); // 移过位移阈值进入 Draw 才开始擦除
            Assert.Equal(TouchState.Draw, host.Canvas.State);
            Assert.Empty(host.Canvas.Strokes);
        });
    }

    /// <summary>擦除模式单指（EvalDraw）落第二指（并拢）：与普通模式同款迁入 PanZoom。</summary>
    [Theory]
    [InlineData(InkCanvasNextMode.EraseArea)]
    [InlineData(InkCanvasNextMode.EraseStroke)]
    public void EraseMode_TwoCloseFingers_EntersPanZoom(InkCanvasNextMode mode)
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = mode;

            host.Device( ).Down(host.Target, P1);
            host.Device( ).Down(host.Target, TestPoints.CloseTo(host, P1));
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);
        });
    }

    /// <summary>面积擦单指（EvalDraw）落第二指（张开）：不迁多指画笔，保持单指擦除上下文。</summary>
    [Fact]
    public void EraseAreaMode_TwoFarFingers_StaysEvalDraw( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.EraseArea;

            var farPoint = TestPoints.FarFrom(host, P1);
            host.Device( ).Down(host.Target, P1);
            host.Device( ).Down(host.Target, farPoint);
            Assert.Equal(TouchState.EvalDraw, host.Canvas.State);
            Assert.Empty(host.Canvas.Strokes);

            host.Device( ).Up(host.Target, farPoint);
            Assert.Equal(TouchState.EvalDraw, host.Canvas.State);
        });
    }

    /// <summary>面积擦 5 指并拢：与普通模式同款迁入掌擦，橡皮直径 = 触点包络直径 + 掌擦外扩。</summary>
    [Fact]
    public void EraseAreaMode_FiveCloseFingers_EntersEraser( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.EraseArea;

            var cluster = TestPoints.CloseCluster(host, P1, 5);
            foreach (var point in cluster)
            {
                host.Device( ).Down(host.Target, point);
            }

            Assert.Equal(TouchState.Eraser, host.Canvas.State);

            // 5 指横向均布：包络直径 = 4 倍指间距（Spacing/8 × 4）
            var expected = TestPoints.Spacing(host) / 2 + Eraser.EraserPalmExtra;
            Assert.Equal(expected, host.Canvas.EraserFeedback.Width, 9);
        });
    }

    /// <summary>面积擦 5 指张开：仍迁入掌擦（普通模式此处迁入 MultiDraw 起笔画）。</summary>
    [Fact]
    public void EraseAreaMode_FiveSpreadFingers_EntersEraser( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.EraseArea;

            var step = TestPoints.Spacing(host) + 1; // 相邻间距超过 l，两两最大距离必大于 l
            for (var i = 0; i < 5; i++)
            {
                host.Device( ).Down(host.Target, new Point(P1.X + i * step, P1.Y));
            }

            Assert.Equal(TouchState.Eraser, host.Canvas.State);
        });
    }

    /// <summary>笔画擦 5 指张开：同样迁入掌擦而非多指画笔。</summary>
    [Fact]
    public void EraseStrokeMode_FiveSpreadFingers_EntersEraser( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.EraseStroke;

            var step = TestPoints.Spacing(host) + 1;
            for (var i = 0; i < 5; i++)
            {
                host.Device( ).Down(host.Target, new Point(P1.X + i * step, P1.Y));
            }

            Assert.Equal(TouchState.Eraser, host.Canvas.State);
        });
    }

    /// <summary>擦除模式单指确认（Draw）后落第二指：拒绝迁入多指画笔，保持原地擦除上下文；
    /// 附加指抬起后仍保持 Draw，首指抬起才回 Idle。</summary>
    [Theory]
    [InlineData(InkCanvasNextMode.EraseArea)]
    [InlineData(InkCanvasNextMode.EraseStroke)]
    public void EraseMode_SecondFingerOnDraw_StaysDraw(InkCanvasNextMode mode)
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = mode;

            var startPoint = new Point(165, 100);
            var drawPoint = TestPoints.BeyondDisplacement(host, startPoint);
            var secondPoint = TestPoints.CloseTo(host, startPoint);

            var finger = host.Device( );
            finger.Down(host.Target, startPoint);
            finger.Move(host.Target, drawPoint); // 超过单指位移阈值，EvalDraw -> Draw
            Assert.Equal(TouchState.Draw, host.Canvas.State);

            var second = host.Device( );
            second.Down(host.Target, secondPoint);
            Assert.Equal(TouchState.Draw, host.Canvas.State);

            second.Up(host.Target, secondPoint);
            Assert.Equal(TouchState.Draw, host.Canvas.State);

            finger.Up(host.Target, drawPoint);
            Assert.Equal(TouchState.Idle, host.Canvas.State);
        });
    }

    [Fact]
    public void PalmEraser_FiveFingers_MoveErasesStroke( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var cluster = TestPoints.CloseCluster(host, P1, 5);
            var center = cluster[2];
            var halfLength = TestPoints.Spacing(host) / 16;
            host.Canvas.Strokes.Add(MakeStroke(
                new Point(center.X - halfLength, center.Y),
                new Point(center.X + halfLength, center.Y)));

            foreach (var point in cluster)
            {
                host.Device( ).Down(host.Target, point);
            }

            Assert.Equal(TouchState.Eraser, host.Canvas.State);
            Assert.Empty(host.Canvas.Strokes);
        });
    }

    // ---------- 盖章模式 ----------

    /// <summary>盖章期间迁入 MultiDraw：多指不创建笔画、抬手不落章。</summary>
    [Fact]
    public void StampMode_FarFingers_EnterMultiDraw_WithoutStrokesOrStamp( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.StampAction = StampAction.Clone;

            var a = host.Device( );
            var b = host.Device( );
            var farPoint = TestPoints.FarFrom(host, P1);
            a.Down(host.Target, P1);
            b.Down(host.Target, farPoint);
            Assert.Equal(TouchState.MultiDraw, host.Canvas.State);

            a.Up(host.Target, P1);
            b.Up(host.Target, farPoint);
            Assert.Equal(TouchState.Idle, host.Canvas.State);
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
            finger.Move(host.Target, TestPoints.BeyondDisplacement(host, P1));
            finger.Up(host.Target, TestPoints.BeyondDisplacement(host, P1));

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
            host.Device( ).Down(host.Target, TestPoints.CloseTo(host, P1));
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

            var spacing = TestPoints.PinchSpacing(host);
            var baseB = new Point(P1.X + spacing, P1.Y);

            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, P1);           // (100, 100)
            b.Down(host.Target, baseB);
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);
            Assert.Equal(1.0, host.Canvas.CurrentScale);

            // 两指围绕初始中点对称张开，中点位移为 0，不触发位移锁死
            a.Move(host.Target, new Point(P1.X - 20, P1.Y - 20));
            b.Move(host.Target, new Point(baseB.X + 20, baseB.Y + 20));
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

            // 逐指上报时中间帧只有一指到位：初始间距需保证中间帧跨度落在 Smooth 死区
            // （不产生缩放）、中间帧中点位移低于平移锁死阈值，末帧中点位移才触发锁死
            var displace = Math.Sqrt(host.Canvas.PanZoomDisplaceThreshold2);
            var dy = displace * 1.05 + 1;
            var spacing = Math.Max(TestPoints.PinchSpacing(host), dy * 1.7);
            var baseA = P1;
            var baseB = new Point(P1.X + spacing, P1.Y);

            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, baseA);
            b.Down(host.Target, baseB);
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);
            Assert.Equal(1.0, host.Canvas.CurrentScale);

            // 两指同向上移超过平移锁死阈值：视为纯平移，缩放锁死
            a.Move(host.Target, new Point(baseA.X, baseA.Y - dy));
            b.Move(host.Target, new Point(baseB.X, baseB.Y - dy));
            Assert.Equal(1.0, host.Canvas.CurrentScale);

            // 大幅张开（远超初始间距）仍不更新缩放
            var spread = TestPoints.Spacing(host);
            a.Move(host.Target, new Point(baseA.X - spread, baseA.Y - dy));
            b.Move(host.Target, new Point(baseB.X + spread, baseB.Y - dy));
            Assert.Equal(1.0, host.Canvas.CurrentScale);
        });
    }

    [Fact]
    public void PanZoom_FingersBelowMinDistance_LocksZoom( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            var spacing = TestPoints.PinchSpacing(host);
            var pinch = Math.Sqrt(host.Canvas.PinchLockDistance2);
            var baseA = P1;
            var baseB = new Point(P1.X + spacing, P1.Y);

            var a = host.Device( );
            var b = host.Device( );
            a.Down(host.Target, baseA);
            b.Down(host.Target, baseB);
            Assert.Equal(TouchState.PanZoom, host.Canvas.State);
            Assert.Equal(1.0, host.Canvas.CurrentScale);

            // 收拢一帧：间距降至初始与锁死阈值之间，且中点位移低于平移锁死阈值 → 先产生缩放
            var midSpacing = (spacing + pinch) / 2;
            a.Move(host.Target, new Point(baseA.X + spacing - midSpacing, baseA.Y));
            Assert.NotEqual(1.0, host.Canvas.CurrentScale);

            // 间距降到锁死阈值以下 → 距离锁死
            b.Move(host.Target, new Point(baseA.X + pinch / 4, baseA.Y));
            var frozen = host.Canvas.CurrentScale;

            // 大幅张开仍不更新缩放
            var spread = TestPoints.Spacing(host);
            a.Move(host.Target, new Point(baseA.X - spread, baseA.Y));
            b.Move(host.Target, new Point(baseA.X + pinch / 4 + spread, baseA.Y));
            Assert.Equal(frozen, host.Canvas.CurrentScale);
        });
    }

    /// <summary>手柄命中半径为视口 DIP：缩放 2x 后内容半径减半，距角柄 HandleHitRadius/2 之外、
    /// 选区之内的落点不再命中手柄，单指拖动表现为整体移动（纯平移）而非缩放。</summary>
    [Fact]
    public void SelectMode_HandleHitRadius_IsScreenAbsolute( )
    {
        StaTest.Run(( ) =>
        {
            using var host = CreateSelectHost(PinchStrokePoints);
            host.Canvas.CurrentScale = 2;

            var bounds = host.Canvas.Strokes[0].GetBounds( );
            const double contentRadius = InkCanvasNext.HandleHitRadius / 2;
            var start = new Point(bounds.Left + contentRadius + 4, bounds.Top);

            var finger = host.Device( );
            finger.Down(host.Target, start);
            finger.Move(host.Target, new Point(start.X + 30, start.Y));
            finger.Up(host.Target, new Point(start.X + 30, start.Y));

            // 未命中手柄 → 移动手势：四个触点整体平移 +30
            var points = host.Canvas.Strokes[0].StylusPoints;
            AssertPoint(points[0], PinchStrokePoints[0].X + 30, PinchStrokePoints[0].Y);
            AssertPoint(points[1], PinchStrokePoints[1].X + 30, PinchStrokePoints[1].Y);
            AssertPoint(points[2], PinchStrokePoints[2].X + 30, PinchStrokePoints[2].Y);
            AssertPoint(points[3], PinchStrokePoints[3].X + 30, PinchStrokePoints[3].Y);
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
