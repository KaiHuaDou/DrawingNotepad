using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
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
        if (state == TouchState.Eraser)
        {
            return true;
        }

        return Mode == InkCanvasNextMode.EraseArea
            && state is TouchState.EvalDraw or TouchState.Draw or TouchState.MultiDraw;
    }

    private void UpdateAreaEraser( )
    {
        if (!IsAreaEraserActive(State))
        {
            EndEraserCycle( );
            return;
        }

        eraser.Scale = currentScale;

        if (State == TouchState.Eraser)
        {
            (var screenCenter, var radius) = Eraser.GetCircle(touches);
            eraser.Diameter = radius * 2 + 8;

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

    private Point GetCanvasCenter(OrderedDictionary<int, (TouchDevice Device, Point Position)> touches)
    {
        if (touches.Count == 0)
        {
            return default;
        }

        double sumX = 0, sumY = 0;
        foreach (var kv in touches)
        {
            var pos = kv.Value.Device.GetTouchPoint(InnerCanvas).Position;
            sumX += pos.X;
            sumY += pos.Y;
        }

        return new Point(sumX / touches.Count, sumY / touches.Count);
    }
}

internal sealed partial class Eraser(InkCanvas canvas, Ellipse feedback)
{
    internal StrokeChanges StrokeChanges { get; } = new([], []);

    internal double Diameter { get; set; } = EraserDefaultDiameter;

    internal double Scale { get; set; } = 1.0;

    internal bool Active { get; private set; }

    private double LogicalDiameter => Math.Max(Diameter / Scale, 1.0);

    private IncrementalStrokeHitTester? hitTester;

    private double hitTesterDiameter;
    private const double RebuildThreshold = 0.5;

    internal void Show(Point screenPosition)
    {
        feedback.Width = Diameter;
        feedback.Height = Diameter;
        Canvas.SetLeft(feedback, screenPosition.X - Diameter / 2);
        Canvas.SetTop(feedback, screenPosition.Y - Diameter / 2);
        feedback.Visibility = Visibility.Visible;
    }

    internal void Start(Point canvasPosition)
    {
        if (Active)
        {
            return;
        }

        Active = true;
        StrokeChanges.Added.Clear( );
        StrokeChanges.Removed.Clear( );
        CreateHitTester(canvasPosition);
    }

    internal void Move(Point canvasPosition)
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

        if (Math.Abs(LogicalDiameter - hitTesterDiameter) < RebuildThreshold)
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
        if (!Active)
        {
            return new StrokeChanges([], []);
        }

        Active = false;
        hitTester?.EndHitTesting( );
        hitTester = null;
        feedback.Visibility = Visibility.Collapsed;

        var change = new StrokeChanges([.. StrokeChanges.Added], [.. StrokeChanges.Removed]);
        StrokeChanges.Added.Clear( );
        StrokeChanges.Removed.Clear( );
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
        if (!StrokeChanges.Removed.Remove(stroke))
        {
            StrokeChanges.Added.Add(stroke);
        }
    }

    private void TrackRemoved(Stroke stroke)
    {
        if (!StrokeChanges.Added.Remove(stroke))
        {
            StrokeChanges.Removed.Add(stroke);
        }
    }

    internal static (Point center, double radius) GetCircle(OrderedDictionary<int, (TouchDevice Device, Point Position)> touches)
    {
        var count = touches.Count;
        if (count == 0)
        {
            return (default, 1);
        }

        double sumX = 0, sumY = 0;
        foreach (var kv in touches)
        {
            var pos = kv.Value.Position;
            sumX += pos.X;
            sumY += pos.Y;
        }

        var center = new Point(sumX / count, sumY / count);

        double max = 0;
        foreach (var kv in touches)
        {
            var distance2 = Geometry.Distance2(kv.Value.Position, center);
            if (distance2 > max)
            {
                max = distance2;
            }
        }

        return (center, Math.Sqrt(max));
    }
}
