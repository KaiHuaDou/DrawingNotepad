using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Ink;

namespace InkCanvasNext;

internal sealed class StrokeChanges(StrokeCollection added, StrokeCollection removed)
{
    public StrokeCollection Added { get; } = added;
    public StrokeCollection Removed { get; } = removed;
}

public partial class InkCanvasNext
{
    private readonly Stack<StrokeChanges> undoStack = new( );
    private readonly Stack<StrokeChanges> redoStack = new( );
    private bool applyingUndoRedo;

    private void OnStrokesChanged(object sender, StrokeCollectionChangedEventArgs e)
    {
        StrokesChanged?.Invoke(this, EventArgs.Empty);

        if (applyingUndoRedo || eraser.Active || (e.Added.Count == 0 && e.Removed.Count == 0))
        {
            return;
        }

        undoStack.Push(new StrokeChanges(e.Added, e.Removed));
        redoStack.Clear( );
        UpdateCanUndoRedo( );
    }

    public void Undo( )
    {
        if (undoStack.Count == 0)
        {
            return;
        }

        applyingUndoRedo = true;
        var change = undoStack.Pop( );
        Canvas.Strokes.Remove(change.Added);
        Canvas.Strokes.Add(change.Removed);
        applyingUndoRedo = false;

        redoStack.Push(change);
        UpdateCanUndoRedo( );
    }

    public void Redo( )
    {
        if (redoStack.Count == 0)
        {
            return;
        }

        applyingUndoRedo = true;
        var change = redoStack.Pop( );
        Canvas.Strokes.Remove(change.Removed);
        Canvas.Strokes.Add(change.Added);
        applyingUndoRedo = false;

        undoStack.Push(change);
        UpdateCanUndoRedo( );
    }

    private void ClearHistory( )
    {
        undoStack.Clear( );
        redoStack.Clear( );
        UpdateCanUndoRedo( );
    }

    private void UpdateCanUndoRedo( )
    {
        var canUndo = undoStack.Count > 0;
        var canRedo = redoStack.Count > 0;

        if (CanUndo != canUndo)
        {
            CanUndo = canUndo;
            CanUndoChanged?.Invoke(this, new DependencyPropertyChangedEventArgs(CanUndoProperty, !canUndo, canUndo));
        }

        if (CanRedo != canRedo)
        {
            CanRedo = canRedo;
            CanRedoChanged?.Invoke(this, new DependencyPropertyChangedEventArgs(CanRedoProperty, !canRedo, canRedo));
        }
    }
}
