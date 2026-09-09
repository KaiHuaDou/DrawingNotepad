using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;

namespace InkCanvasNext;

/// <summary>
/// 历史记录项抽象：Apply 为重做方向，Revert 为撤销方向。
/// 参数 owner 用于访问 InkCanvas 以执行增删（StrokeChanges）或纯变换（TransformChanges）。
/// </summary>
internal interface IHistoryChange
{
    void Apply(InkCanvasNext owner);
    void Revert(InkCanvasNext owner);
}

internal sealed class StrokeChanges(StrokeCollection added, StrokeCollection removed) : IHistoryChange
{
    internal StrokeCollection Added { get; } = added;
    internal StrokeCollection Removed { get; } = removed;

    void IHistoryChange.Apply(InkCanvasNext owner)
    {
        owner.InnerCanvas.Strokes.Remove(Removed);
        owner.InnerCanvas.Strokes.Add(Added);
    }

    void IHistoryChange.Revert(InkCanvasNext owner)
    {
        owner.InnerCanvas.Strokes.Remove(Added);
        owner.InnerCanvas.Strokes.Add(Removed);
    }
}

/// <summary>
/// 一次手势（移动/缩放/旋转）的整段绝对变换，作为单个撤销单元。
/// </summary>
internal sealed class TransformChanges(StrokeCollection target, Matrix delta) : IHistoryChange
{
    void IHistoryChange.Apply(InkCanvasNext owner)
    {
        target.Transform(delta, false);
    }

    void IHistoryChange.Revert(InkCanvasNext owner)
    {
        var inverse = delta;
        inverse.Invert( );
        target.Transform(inverse, false);
    }
}

/// <summary>
/// 结构性变化携带 Added/Removed；选区整体变换（移动/缩放/旋转）无增删，
/// </summary>
public sealed class InkCanvasStrokesChangedEventArgs : EventArgs
{
    internal InkCanvasStrokesChangedEventArgs(StrokeCollection added, StrokeCollection removed, bool transformOnly)
    {
        Added = added;
        Removed = removed;
        TransformOnly = transformOnly;
    }

    public StrokeCollection Added { get; }

    public StrokeCollection Removed { get; }

    public bool TransformOnly { get; }
}

public sealed class HistorySnapshot
{
    internal HistorySnapshot(IHistoryChange[] changes, int position)
    {
        Changes = changes;
        Position = position;
    }

    internal IReadOnlyList<IHistoryChange> Changes { get; }

    public int Position { get; }
}

public partial class InkCanvasNext
{
    private const int MaxHistoryCount = 200;
    private readonly RingBuffer<IHistoryChange> history = new(MaxHistoryCount);
    private int position;
    private bool applyingUndoRedo;

    private void PushChange(IHistoryChange change)
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
        StrokesChanged?.Invoke(
            this,
            new InkCanvasStrokesChangedEventArgs(e.Added, e.Removed, transformOnly: false));

        if (e.Removed.Count > 0)
        {
            selection.Prune(InnerCanvas.Strokes);
        }

        if (applyingUndoRedo || eraser.Active || (e.Added.Count == 0 && e.Removed.Count == 0))
        {
            return;
        }

        PushChange(new StrokeChanges(e.Added, e.Removed));
    }

    /// <summary>
    /// 撤销上一步操作。
    /// </summary>
    public void Undo( )
    {
        if (position == 0)
        {
            return;
        }

        applyingUndoRedo = true;
        try
        {
            position--;
            history[position].Revert(this);
        }
        finally
        {
            // 无论 Revert 成败都复位，否则后续变更会永远不再入栈（静默失去撤销能力）
            applyingUndoRedo = false;
        }

        UndoRedoRefresh( );
    }

    /// <summary>
    /// 重做已撤销的操作。
    /// </summary>
    public void Redo( )
    {
        if (position >= history.Count)
        {
            return;
        }

        applyingUndoRedo = true;
        try
        {
            var change = history[position];
            position++;
            change.Apply(this);
        }
        finally
        {
            applyingUndoRedo = false;
        }

        UndoRedoRefresh( );
    }
    private void UndoRedoRefresh( )
    {
        selection.RecomputeBounds( );
        selection.Invalidate( );
        UpdateCanUndoRedo( );
        RaiseViewOrSelectionChanged( );
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

    /// <summary>
    /// 导出当前历史快照到 <paramref name="old"/>，并用 <paramref name="new"/> 替换现有历史。
    /// </summary>
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
