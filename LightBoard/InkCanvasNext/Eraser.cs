using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Shapes;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    private readonly Eraser eraser;

    private void EndEraserCycle( )
    {
        var change = eraser.End( );

        if (change.Added.Count > 0 || change.Removed.Count > 0)
        {
            PushChange(change);
        }
    }

    private bool IsAreaEraserActive(TouchState state)
    {
        var traits = Traits(state);
        return traits.PalmErase || (Tool.IsAreaErase && traits.AreaEraseHost);
    }

    private void UpdateAreaEraser( )
    {
        if (!IsAreaEraserActive(State))
        {
            EndEraserCycle( );
            return;
        }

        if (State == TouchState.Eraser)
        {
            (var screenCenter, var radius) = GetEnvelopeCircle(touches);
            eraser.Diameter = radius * 2 + eraserPalmExtra;

            var canvasCenter = GetCanvasCenter(touches);
            eraser.Show(screenCenter);
            eraser.Update(canvasCenter);
        }
        else
        {
            eraser.Diameter = EraserDiameter;

            (var Device, var Position) = touches.First( ).Value;
            var screenPosition = Position;
            var canvasPosition = Device.GetTouchPoint(InnerCanvas).Position;

            eraser.Show(screenPosition);

            if (State != TouchState.EvalDraw)
            {
                eraser.Update(canvasPosition);
            }
        }
    }
}

internal sealed partial class Eraser(InkCanvas canvas, Ellipse feedback, EraserParameters parameters)
{
    private readonly double minLogicalDiameter = parameters.MinLogicalDiameter;
    private readonly double rebuildThreshold = parameters.RebuildThreshold;
    private readonly StrokeChanges strokeChanges = new([], []);

    private IncrementalStrokeHitTester? hitTester;

    private double hitTesterDiameter;

    internal double Diameter { get; set; } = parameters.Diameter;

    /// <summary>
    /// 画布缩放，由 CurrentScale 写入，用于视口直径与内容直径换算
    /// </summary>
    internal double Scale { get; set; } = 1.0;

    internal bool Active { get; private set; }

    private double LogicalDiameter => Math.Max(Diameter / Scale, minLogicalDiameter);

    internal void Show(Point screenPosition)
    {
        feedback.Width = Diameter;
        feedback.Height = Diameter;
        Canvas.SetLeft(feedback, screenPosition.X - Diameter / 2);
        Canvas.SetTop(feedback, screenPosition.Y - Diameter / 2);
        feedback.Visibility = Visibility.Visible;
    }

    private void Start(Point canvasPosition)
    {
        if (Active)
        {
            return;
        }

        Active = true;
        strokeChanges.Added.Clear( );
        strokeChanges.Removed.Clear( );
        CreateHitTester(canvasPosition);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void Move(Point canvasPosition)
    {
        if (!Active)
        {
            return;
        }

        hitTester?.AddPoint(canvasPosition);
    }

    internal void Update(Point canvasPosition)
    {
        if (!Active)
        {
            Start(canvasPosition);
        }
        else
        {
            SyncHitTester(canvasPosition);
            Move(canvasPosition);
        }
    }

    private void SyncHitTester(Point canvasPosition)
    {
        if (hitTester is null)
        {
            return;
        }

        if (Math.Abs(LogicalDiameter - hitTesterDiameter) < rebuildThreshold)
        {
            return;
        }

        hitTester.StrokeHit -= OnStrokeHit;
        hitTester.EndHitTesting( );
        hitTester = null;
        CreateHitTester(canvasPosition);
    }

    internal StrokeChanges End( )
    {
        // EvalDraw 只显示橡皮不开擦除周期：隐藏不依赖 Active，否则圈内残留到下一次擦除态
        feedback.Visibility = Visibility.Collapsed;

        if (!Active)
        {
            return new StrokeChanges([], []);
        }

        Active = false;
        hitTester?.EndHitTesting( );
        hitTester = null;

        var change = new StrokeChanges([.. strokeChanges.Added], [.. strokeChanges.Removed]);
        strokeChanges.Added.Clear( );
        strokeChanges.Removed.Clear( );
        return change;
    }

    private void CreateHitTester(Point position)
    {
        var diameter = LogicalDiameter;
        var shape = new EllipseStylusShape(diameter, diameter);
        hitTester = canvas.Strokes.GetIncrementalStrokeHitTester(shape);
        hitTester.StrokeHit += OnStrokeHit;
        hitTesterDiameter = diameter;
        hitTester.AddPoint(position);
    }

    private void OnStrokeHit(object sender, StrokeHitEventArgs e)
    {
        var result = e.GetPointEraseResults( );
        var hitStroke = e.HitStroke;

        if (!canvas.Strokes.Contains(hitStroke))
        {
            return;
        }

        canvas.Strokes.Replace([hitStroke], result);

        TrackRemoved(hitStroke);
        foreach (var stroke in result)
        {
            TrackAdded(stroke);
        }
    }

    private void TrackAdded(Stroke stroke)
    {
        if (!strokeChanges.Removed.Remove(stroke))
        {
            strokeChanges.Added.Add(stroke);
        }
    }

    private void TrackRemoved(Stroke stroke)
    {
        if (!strokeChanges.Added.Remove(stroke))
        {
            strokeChanges.Removed.Add(stroke);
        }
    }
}
