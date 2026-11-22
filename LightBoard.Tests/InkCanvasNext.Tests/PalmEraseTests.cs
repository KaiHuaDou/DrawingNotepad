using System.Linq;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasNext.Tests;

/// <summary>
/// 手掌面积通道：尺寸分流、笔迹提交、包络与直径
/// </summary>
public class PalmEraseTests
{
    private static readonly Point P1 = new(160, 300);
    private static readonly Point P2 = new(420, 360);
    private static readonly Size Palm = new(320, 220);
    private static readonly Size Finger = new(45, 60);

    [Fact]
    public void PalmDown_EntersEraser( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var palm = host.Device( );

            palm.Down(host.Target, P1, Palm);

            Assert.Equal(TouchState.Eraser, host.Canvas.State);
            Assert.Equal(1, host.Canvas.PalmDebug.PalmCount);
            Assert.True(host.Canvas.PalmDebug.HasContactSize);
            Assert.True(host.Canvas.EraserActive);
        });
    }

    [Fact]
    public void FingerDown_StaysEvalDraw( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var finger = host.Device( );

            finger.Down(host.Target, P1, Finger);

            Assert.Equal(TouchState.EvalDraw, host.Canvas.State);
            Assert.Equal(0, host.Canvas.PalmDebug.PalmCount);
        });
    }

    [Fact]
    public void PalmAtThreshold_DoesNotErase( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var palm = host.Device( );

            palm.Down(host.Target, P1, new Size(300, 200));

            Assert.Equal(TouchState.EvalDraw, host.Canvas.State);
            Assert.Equal(0, host.Canvas.PalmDebug.PalmCount);
        });
    }

    [Fact]
    public void PalmAboveThreshold_Erases( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var palm = host.Device( );

            palm.Down(host.Target, P1, new Size(301, 201));

            Assert.Equal(TouchState.Eraser, host.Canvas.State);
            Assert.Equal(1, host.Canvas.PalmDebug.PalmCount);
        });
    }

    [Fact]
    public void PalmThenFinger_KeepsEraser( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Device( ).Down(host.Target, P1, Palm);
            host.Device( ).Down(host.Target, P2, Finger);

            Assert.Equal(TouchState.Eraser, host.Canvas.State);
            Assert.Equal(1, host.Canvas.PalmDebug.PalmCount);
            Assert.Equal(2, host.Canvas.ActiveTouchCount);
        });
    }

    [Fact]
    public void FingerThenPalm_EntersEraser( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Device( ).Down(host.Target, P1, Finger);
            host.Device( ).Down(host.Target, P2, Palm);

            Assert.Equal(TouchState.Eraser, host.Canvas.State);
            Assert.Equal(1, host.Canvas.PalmDebug.PalmCount);
        });
    }

    [Fact]
    public void ThreeFragments_EnterEraser( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var size = new Size(150, 150);

            host.Device( ).Down(host.Target, P1, size);
            host.Device( ).Down(host.Target, new Point(P1.X + 80, P1.Y), size);
            host.Device( ).Down(host.Target, new Point(P1.X + 40, P1.Y + 100), size);

            Assert.Equal(TouchState.Eraser, host.Canvas.State);
            Assert.Equal(3, host.Canvas.PalmDebug.PalmCount);
            Assert.Equal(3, host.Canvas.ActiveTouchCount);
        });
    }

    [Fact]
    public void PalmUpWithFingerRemaining_StopsErase( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var palm = host.Device( );
            var finger = host.Device( );
            palm.Down(host.Target, P1, Palm);
            finger.Down(host.Target, P2, Finger);

            palm.Up(host.Target, P1);

            Assert.Equal(TouchState.Eraser, host.Canvas.State);
            Assert.Equal(0, host.Canvas.PalmDebug.PalmCount);
            Assert.False(host.Canvas.EraserActive);

            finger.Up(host.Target, P2);
            Assert.Equal(TouchState.Idle, host.Canvas.State);
        });
    }

    [Fact]
    public void PalmUp_ReturnsIdle( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var palm = host.Device( );
            palm.Down(host.Target, P1, Palm);

            palm.Up(host.Target, P1);

            Assert.Equal(TouchState.Idle, host.Canvas.State);
            Assert.Equal(0, host.Canvas.PalmDebug.PalmCount);
        });
    }

    [Fact]
    public void EraseAreaPalm_UsesAreaDiameter( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Mode = InkCanvasNextMode.EraseArea;
            var palm = host.Device( );

            palm.Down(host.Target, P1, new Size(320, 240));

            Assert.Equal(TouchState.Eraser, host.Canvas.State);
            Assert.Equal(165, host.Canvas.PalmDebug.FinalDiameter, 0.5);
        });
    }

    [Fact]
    public void SecondPalm_RestartsAdjustWindow( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var palm = host.Device( );
            var finger = host.Device( );
            palm.Down(host.Target, P1, Palm);
            finger.Down(host.Target, P2, Finger);

            palm.Up(host.Target, P1);
            Assert.Equal(TouchState.Eraser, host.Canvas.State);

            var second = new Size(700, 500);
            host.Device( ).Down(host.Target, new Point(900, 300), second);

            Assert.Equal(213.75, host.Canvas.PalmDebug.FinalDiameter, 0.5);
        });
    }

    [Fact]
    public void PalmDuringDraw_ErasesAfterFingerLifts( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var finger = host.Device( );
            finger.Down(host.Target, P1, Finger);
            finger.Move(host.Target, TestPoints.BeyondDisplacement(host, P1), Finger);
            Assert.Equal(TouchState.Draw, host.Canvas.State);

            var palm = host.Device( );
            palm.Down(host.Target, P2, Palm);
            Assert.Equal(TouchState.Draw, host.Canvas.State);

            finger.Up(host.Target, TestPoints.BeyondDisplacement(host, P1));

            Assert.Equal(TouchState.Eraser, host.Canvas.State);
            Assert.True(host.Canvas.EraserActive);
        });
    }

    [Fact]
    public void PalmInsideSelection_DoesNotErase( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Mode = InkCanvasNextMode.Select;
            host.Canvas.Strokes.Add(ShortStroke( ));
            Lasso(host, new Point(150, 100), new Point(300, 400));
            Assert.True(host.Canvas.HasSelection);

            host.Device( ).Down(host.Target, new Point(225, 250), Finger);
            Assert.Equal(TouchState.Selection, host.Canvas.State);

            host.Device( ).Down(host.Target, new Point(225, 250), Palm);

            Assert.Equal(TouchState.Selection, host.Canvas.State);
            Assert.Equal(1, host.Canvas.PalmDebug.PalmCount);
            Assert.False(host.Canvas.EraserActive);
        });
    }

    [Fact]
    public void TwoDistantPalms_UseNewestCluster( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Device( ).Down(host.Target, P1, Palm);
            host.Device( ).Down(host.Target, new Point(900, 300), new Size(700, 500));

            Assert.Equal(2, host.Canvas.PalmDebug.PalmCount);
            Assert.Equal(700, host.Canvas.PalmDebug.ClusterWidth, 1);
            Assert.Equal(500, host.Canvas.PalmDebug.ClusterHeight, 1);
            Assert.Equal(213.75, host.Canvas.PalmDebug.AreaDiameter, 0.5);
        });
    }

    [Fact]
    public void TwoDistantPalms_DoNotEraseBetweenThem( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost(options: new( )
            {
                Eraser = new( ) { PalmSizeAdjustDuration = 0 }
            });
            var second = new Point(900, P1.Y);
            var bridge = BetweenPalms(host, P1, second);
            host.Canvas.Strokes.Add(bridge);

            host.Device( ).Down(host.Target, P1, Palm);
            host.Device( ).Down(host.Target, second, Palm);

            Assert.Contains(bridge, host.Canvas.Strokes);
        });
    }

    [Fact]
    public void LiftingSeedPalm_DoesNotEraseBetweenPalms( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost(options: new( )
            {
                Eraser = new( ) { PalmSizeAdjustDuration = 0 }
            });
            var second = new Point(900, P1.Y);
            var bridge = BetweenPalms(host, P1, second);
            host.Canvas.Strokes.Add(bridge);

            host.Device( ).Down(host.Target, P1, Palm);
            var seed = host.Device( );
            seed.Down(host.Target, second, Palm);
            seed.Up(host.Target, second);

            Assert.True(host.Canvas.EraserActive);
            Assert.Contains(bridge, host.Canvas.Strokes);
        });
    }

    [Theory]
    [InlineData(300.0, 250.0)]
    [InlineData(500.0, 150.0)]
    public void SingleContactBelowLongShortSideThreshold_DoesNotErase(double width, double height)
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Device( ).Down(host.Target, P1, new Size(width, height));

            Assert.Equal(TouchState.EvalDraw, host.Canvas.State);
            Assert.Equal(0, host.Canvas.PalmDebug.PalmCount);
        });
    }

    [Fact]
    public void PalmDown_ErasesStrokeUnderPalm( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var center = host.Canvas.TranslatePoint(P1, host.Target);
            var stroke = HorizontalLine(center.Y, center.X);
            host.Canvas.Strokes.Add(stroke);

            var palm = host.Device( );
            palm.Down(host.Target, P1, Palm);
            palm.Move(host.Target, new Point(P1.X + 10, P1.Y), Palm);

            Assert.DoesNotContain(stroke, host.Canvas.Strokes);
        });
    }

    [Fact]
    public void FiveFingerEraser_PalmJoins_DoesNotEraseAtOrigin( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var originStroke = HorizontalLine(0, 0);
            host.Canvas.Strokes.Add(originStroke);

            // 与原点保持距离，避免五指通道自身的包络覆盖原点笔画
            var start = new Point(700, 600);
            var cluster = TestPoints.CloseCluster(host, start, 5);
            var fingers = cluster.Select(_ => host.Device( )).ToArray( );
            for (var i = 0; i < fingers.Length; i++)
            {
                fingers[i].Down(host.Target, cluster[i]);
            }

            Assert.Equal(TouchState.Eraser, host.Canvas.State);

            var palmPoint = new Point(900, 200);
            var palm = host.Device( );
            palm.Down(host.Target, palmPoint, Palm);

            for (var i = 0; i < fingers.Length; i++)
            {
                fingers[i].Up(host.Target, cluster[i]);
            }

            palm.Move(host.Target, new Point(palmPoint.X + 10, palmPoint.Y), Palm);

            Assert.Equal(TouchState.Eraser, host.Canvas.State);
            Assert.True(host.Canvas.PalmDebug.IsAreaPalm);
            Assert.Contains(originStroke, host.Canvas.Strokes);
        });
    }

    [Fact]
    public void PalmDiameter_FreezesAfterAdjustWindow( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost(options: new( )
            {
                Eraser = new( ) { PalmSizeAdjustDuration = 0 }
            });
            var palm = host.Device( );

            palm.Down(host.Target, P1, new Size(320, 240));
            var first = host.Canvas.PalmDebug.FinalDiameter;

            palm.Move(host.Target, P1, new Size(700, 500));

            Assert.Equal(165, first, 0.5);
            Assert.Equal(first, host.Canvas.PalmDebug.FinalDiameter, 0.5);
        });
    }

    [Fact]
    public void SelectionPalmOutside_EntersEraser( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Mode = InkCanvasNextMode.Select;
            host.Device( ).Down(host.Target, P1, Finger);
            Assert.Equal(TouchState.Selection, host.Canvas.State);

            host.Device( ).Down(host.Target, P2, Palm);

            Assert.Equal(TouchState.Eraser, host.Canvas.State);
        });
    }

    [Fact]
    public void ReadOnlyPalm_DoesNotErase( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.IsReadOnly = true;
            var palm = host.Device( );

            palm.Down(host.Target, P1, Palm);

            Assert.Equal(TouchState.EvalDraw, host.Canvas.State);
            Assert.Equal(0, host.Canvas.PalmDebug.PalmCount);
            Assert.False(host.Canvas.EraserActive);
        });
    }

    [Fact]
    public void TwoPalmErases_ProduceTwoEraseBatches( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var erases = 0;
            host.Canvas.OperationCompleted += (_, e) =>
            {
                if (e.Operation == InkCanvasOperation.Erase)
                {
                    erases++;
                }
            };

            host.Canvas.Strokes.Add(HorizontalLine(P1.Y, P1.X));

            var first = host.Device( );
            first.Down(host.Target, P1, Palm);
            first.Up(host.Target, P1);

            host.Canvas.Strokes.Add(HorizontalLine(P2.Y, P2.X));
            var second = host.Device( );
            second.Down(host.Target, P2, Palm);
            second.Up(host.Target, P2);

            Assert.Equal(2, erases);
        });
    }

    /// <summary>
    /// 两只手掌内容坐标之间的一条横线：两端远离两个擦除中心，只可能被中心跳变的扫掠擦到
    /// </summary>
    private static Stroke BetweenPalms(TouchHost host, Point firstViewport, Point secondViewport)
    {
        var first = host.Canvas.TranslatePoint(firstViewport, host.Target);
        var second = host.Canvas.TranslatePoint(secondViewport, host.Target);
        return HorizontalLine(first.Y, (first.X + second.X) / 2);
    }

    private static Stroke HorizontalLine(double y, double centerX)
    {
        const double reach = 200;
        var points = new StylusPointCollection( );
        for (var x = centerX - reach; x <= centerX + reach; x += 20)
        {
            points.Add(new StylusPoint(x, y, 0.5f));
        }

        return new Stroke(points, new DrawingAttributes { Color = Colors.Black, Width = 3, Height = 3 });
    }

    private static Stroke ShortStroke( )
    {
        return new Stroke(
            [new StylusPoint(200, 200), new StylusPoint(250, 300)],
            new DrawingAttributes { Color = Colors.Black, Width = 3, Height = 3 });
    }

    private static void Lasso(TouchHost host, Point topLeft, Point bottomRight)
    {
        var finger = host.Device( );
        finger.Down(host.Target, topLeft);
        finger.Move(host.Target, new Point(bottomRight.X, topLeft.Y));
        finger.Move(host.Target, new Point(bottomRight.X, bottomRight.Y));
        finger.Move(host.Target, new Point(topLeft.X, bottomRight.Y));
        finger.Move(host.Target, topLeft);
        finger.Up(host.Target, topLeft);
    }
}
