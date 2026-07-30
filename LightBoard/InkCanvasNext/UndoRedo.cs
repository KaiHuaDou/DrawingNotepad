using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Ink;

namespace InkCanvasNext;

public sealed class StrokeChanges(StrokeCollection added, StrokeCollection removed)
{
    public StrokeCollection Added { get; } = added;
    public StrokeCollection Removed { get; } = removed;
}

public sealed class HistorySnapshot(StrokeChanges[] changes, int position)
{
    public IReadOnlyList<StrokeChanges> Changes { get; } = changes;
    public int Position { get; } = position;
}

public partial class InkCanvasNext
{
    private const int MaxHistoryCount = 200;
    private readonly RingBuffer<StrokeChanges> history = new(MaxHistoryCount);
    private int position;
    private bool applyingUndoRedo;

    private void PushChange(StrokeChanges change)
    {
        if (position < history.Count)
        {
            history.Truncate(position);
        }

        history.Enqueue(change);
        position = history.Count;
        UpdateCanUndoRedo( );
    }

    private void OnStrokesChanged(object sender, StrokeCollectionChangedEventArgs e)
    {
        StrokesChanged?.Invoke(this, EventArgs.Empty);

        if (applyingUndoRedo || eraser.Active || (e.Added.Count == 0 && e.Removed.Count == 0))
        {
            return;
        }

        PushChange(new StrokeChanges(e.Added, e.Removed));
    }

    public void Undo( )
    {
        if (position == 0)
        {
            return;
        }

        applyingUndoRedo = true;
        position--;
        var change = history[position];
        Canvas.Strokes.Remove(change.Added);
        Canvas.Strokes.Add(change.Removed);
        applyingUndoRedo = false;

        UpdateCanUndoRedo( );
    }

    public void Redo( )
    {
        if (position >= history.Count)
        {
            return;
        }

        applyingUndoRedo = true;
        var change = history[position];
        position++;
        Canvas.Strokes.Remove(change.Removed);
        Canvas.Strokes.Add(change.Added);
        applyingUndoRedo = false;

        UpdateCanUndoRedo( );
    }

    private void ClearHistory( )
    {
        history.Clear( );
        position = 0;
        UpdateCanUndoRedo( );
    }

    private void UpdateCanUndoRedo( )
    {
        var canUndo = position > 0;
        var canRedo = position < history.Count;

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

    public void SwapHistory(out HistorySnapshot? old, HistorySnapshot? @new)
    {
        old = history.Count > 0 ? new HistorySnapshot(history.ToArray( ), position) : null;
        history.Clear( );
        position = 0;

        if (@new is not null)
        {
            for (var i = 0; i < @new.Changes.Count; i++)
            {
                history.Enqueue(@new.Changes[i]);
            }

            position = @new.Position;
        }

        UpdateCanUndoRedo( );
    }
}
