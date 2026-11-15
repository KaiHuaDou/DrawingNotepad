using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasNext.Tests;

/// <summary>
/// 工具操作完成通知：落笔与擦除各发一次，撤销重做与周期内的换笔不误发
/// </summary>
public class OperationTests
{
    private static readonly Point Start = new(160, 300);
    private static readonly Point Palm = new(100, 100);

    [Fact]
    public void StrokeAdded_RaisesStroke( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var operations = Track(host);

            host.Canvas.Strokes.Add(HorizontalLine(300, 300, 40));

            var operation = Assert.Single(operations);
            Assert.Equal(InkCanvasOperation.Stroke, operation);
        });
    }

    [Fact]
    public void UndoRedo_DoesNotRaise( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Strokes.Add(HorizontalLine(300, 300, 40));
            var operations = Track(host);

            host.Canvas.Undo( );
            host.Canvas.Redo( );

            Assert.Empty(operations);
        });
    }

    [Fact]
    public void AreaErase_Gesture_RaisesOnce( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.DrawingAttributes = host.Canvas.DrawingAttributes with { Mode = InkCanvasNextMode.EraseArea };

            var target = TestPoints.BeyondDisplacement(host, Start);
            host.Canvas.Strokes.Add(HorizontalLine(300, target.X, 150));
            host.Canvas.Strokes.Add(HorizontalLine(280, target.X, 150));
            var operations = Track(host);

            var finger = host.Device( );
            finger.Down(host.Target, Start);
            finger.Move(host.Target, target);
            finger.Up(host.Target, target);

            var operation = Assert.Single(operations);
            Assert.Equal(InkCanvasOperation.Erase, operation);
        });
    }

    [Fact]
    public void RevertDuringCycleEnd_RestoresInkEditing( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.DrawingAttributes = host.Canvas.DrawingAttributes with { Mode = InkCanvasNextMode.EraseArea };

            // 模拟宿主在周期结束的回调里回退工具
            host.Canvas.OperationCompleted += (_, e) =>
            {
                if (e.Operation == InkCanvasOperation.Erase)
                {
                    host.Canvas.DrawingAttributes = host.Canvas.DrawingAttributes with { Mode = InkCanvasNextMode.Ink };
                }
            };

            var target = TestPoints.BeyondDisplacement(host, Start);
            host.Canvas.Strokes.Add(HorizontalLine(300, target.X, 40));

            var finger = host.Device( );
            finger.Down(host.Target, Start);
            finger.Move(host.Target, target);
            finger.Up(host.Target, target);

            Assert.Equal(InkCanvasNextMode.Ink, host.Canvas.DrawingAttributes.Mode);
            Assert.Equal(InkCanvasEditingMode.Ink, host.Canvas.InnerCanvas.EditingMode);
        });
    }

    [Fact]
    public void AreaErase_BlankTap_RaisesOnce( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.DrawingAttributes = host.Canvas.DrawingAttributes with { Mode = InkCanvasNextMode.EraseArea };
            var operations = Track(host);

            var finger = host.Device( );
            finger.Down(host.Target, Start);
            finger.Up(host.Target, Start);

            var operation = Assert.Single(operations);
            Assert.Equal(InkCanvasOperation.Erase, operation);
        });
    }

    [Fact]
    public void PalmErase_RaisesErase( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var cluster = TestPoints.CloseCluster(host, Palm, 5);
            var center = cluster[2];
            host.Canvas.Strokes.Add(HorizontalLine(center.Y, center.X, TestPoints.Spacing(host) / 16));
            var operations = Track(host);

            var fingers = new List<MockTouchDevice>( );
            foreach (var point in cluster)
            {
                var finger = host.Device( );
                finger.Down(host.Target, point);
                fingers.Add(finger);
            }

            Assert.Empty(host.Canvas.Strokes);

            foreach (var finger in fingers)
            {
                finger.Up(host.Target, Palm);
            }

            var operation = Assert.Single(operations);
            Assert.Equal(InkCanvasOperation.Erase, operation);
        });
    }

    private static List<InkCanvasOperation> Track(TouchHost host)
    {
        var operations = new List<InkCanvasOperation>( );
        host.Canvas.OperationCompleted += (_, e) => operations.Add(e.Operation);
        return operations;
    }

    /// <summary>以 centerX 为中点、半长 half 的三点横线</summary>
    private static Stroke HorizontalLine(double y, double centerX, double half)
    {
        return new Stroke(
            [
                new StylusPoint(centerX - half, y, 0.5f),
                new StylusPoint(centerX, y, 0.5f),
                new StylusPoint(centerX + half, y, 0.5f)
            ],
            new DrawingAttributes { Color = Colors.Black, Width = 3, Height = 3 });
    }
}
