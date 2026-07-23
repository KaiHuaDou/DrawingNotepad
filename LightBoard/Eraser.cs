using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Shapes;

namespace LightBoard;

public sealed class StrokeChange(StrokeCollection added, StrokeCollection removed)
{
    public StrokeCollection Added { get; } = added;
    public StrokeCollection Removed { get; } = removed;
}

public sealed class Eraser(InkCanvas canvas, Ellipse feedback)
{
    private readonly StrokeCollection added = [];
    private readonly StrokeCollection removed = [];
    private IncrementalStrokeHitTester? hitTester;

    public double Diameter { get; set; } = 50.0;

    public bool IsActive { get; private set; }

    public bool IsVisible => feedback.Visibility == Visibility.Visible;

    public void Show(Point position)
    {
        feedback.Width = Diameter;
        feedback.Height = Diameter;
        Canvas.SetLeft(feedback, position.X - Diameter / 2);
        Canvas.SetTop(feedback, position.Y - Diameter / 2);
        feedback.Visibility = Visibility.Visible;
    }

    public void Hide( )
    {
        feedback.Visibility = Visibility.Collapsed;
    }

    public void Start(Point position)
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        added.Clear( );
        removed.Clear( );
        CreateHitTester(position);
    }

    public void Restart(Point position)
    {
        if (IsActive)
        {
            hitTester?.EndHitTesting( );
        }

        IsActive = true;
        CreateHitTester(position);
    }

    public void Move(Point position)
    {
        if (!IsActive)
        {
            return;
        }

        hitTester?.AddPoint(position);
        Show(position);
    }

    public void End( )
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        hitTester?.EndHitTesting( );
        hitTester = null;
        Hide( );
    }

    private void CreateHitTester(Point position)
    {
        var shape = new EllipseStylusShape(Diameter, Diameter);
        hitTester = canvas.Strokes.GetIncrementalStrokeHitTester(shape);
        hitTester.StrokeHit += OnStrokeHit;
        hitTester.AddPoint(position);
        Show(position);
    }

    public StrokeChange CollectChanges( )
    {
        var change = new StrokeChange(new StrokeCollection(added), new StrokeCollection(removed));
        added.Clear( );
        removed.Clear( );
        return change;
    }

    private void OnStrokeHit(object sender, StrokeHitEventArgs e)
    {
        StrokeCollection result = e.GetPointEraseResults( );
        Stroke hitStroke = e.HitStroke;

        if (!canvas.Strokes.Contains(hitStroke))
        {
            return;
        }

        canvas.Strokes.Replace([hitStroke], result);

        TrackRemoved(hitStroke);
        foreach (Stroke stroke in result)
        {
            TrackAdded(stroke);
        }
    }

    private void TrackAdded(Stroke stroke)
    {
        if (!removed.Remove(stroke))
        {
            added.Add(stroke);
        }
    }

    private void TrackRemoved(Stroke stroke)
    {
        if (!added.Remove(stroke))
        {
            removed.Add(stroke);
        }
    }
}

public partial class MainWindow
{
    private readonly Stack<StrokeChange> undoStack = new( );
    private readonly Stack<StrokeChange> redoStack = new( );
    private bool applyingUndoRedo;
    private bool areaEraserSelected;

    private void OnStrokesChanged(object sender, StrokeCollectionChangedEventArgs e)
    {
        dirty = true;

        if (applyingUndoRedo || eraser.IsActive || (e.Added.Count == 0 && e.Removed.Count == 0))
        {
            return;
        }

        undoStack.Push(new StrokeChange(e.Added, e.Removed));
        redoStack.Clear( );
        UpdateUndoRedoButtons( );
    }

    private void UndoButtonClick(object o, RoutedEventArgs e)
    {
        if (undoStack.Count == 0)
        {
            return;
        }

        applyingUndoRedo = true;
        StrokeChange change = undoStack.Pop( );
        MainCanvas.Strokes.Remove(change.Added);
        MainCanvas.Strokes.Add(change.Removed);
        applyingUndoRedo = false;

        redoStack.Push(change);
        UpdateUndoRedoButtons( );
    }

    private void RedoButtonClick(object o, RoutedEventArgs e)
    {
        if (redoStack.Count == 0)
        {
            return;
        }

        applyingUndoRedo = true;
        StrokeChange change = redoStack.Pop( );
        MainCanvas.Strokes.Remove(change.Removed);
        MainCanvas.Strokes.Add(change.Added);
        applyingUndoRedo = false;

        undoStack.Push(change);
        UpdateUndoRedoButtons( );
    }

    private void UpdateUndoRedoButtons( )
    {
        UndoButton.IsEnabled = undoStack.Count > 0;
        RedoButton.IsEnabled = redoStack.Count > 0;
    }

    private void EndEraserCycle( )
    {
        if (!eraser.IsActive)
        {
            return;
        }

        eraser.End( );

        StrokeChange change = eraser.CollectChanges( );
        if (change.Added.Count > 0 || change.Removed.Count > 0)
        {
            undoStack.Push(change);
            redoStack.Clear( );
            UpdateUndoRedoButtons( );
        }
    }

    private void MainCanvasPreviewMouseDown(object o, MouseButtonEventArgs e)
    {
        if (e.StylusDevice != null || !areaEraserSelected)
        {
            return;
        }

        Point position = e.GetPosition(MainCanvas);
        eraser.Diameter = 50;
        eraser.Start(position);
        e.Handled = true;
    }

    private void MainCanvasPreviewMouseMove(object o, MouseEventArgs e)
    {
        if (e.StylusDevice != null || !eraser.IsActive)
        {
            return;
        }

        eraser.Move(e.GetPosition(MainCanvas));
        e.Handled = true;
    }

    private void MainCanvasPreviewMouseUp(object o, MouseButtonEventArgs e)
    {
        if (e.StylusDevice != null || !eraser.IsActive)
        {
            return;
        }

        EndEraserCycle( );
        e.Handled = true;
    }
}
