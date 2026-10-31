using System;
using System.Collections.Generic;
using System.Linq;
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

internal sealed class StrokeChanges(Stroke[] added, Stroke[] removed) : IHistoryChange
{
    internal Stroke[] Added { get; } = added;
    internal Stroke[] Removed { get; } = removed;

    void IHistoryChange.Apply(InkCanvasNext owner)
    {
        Remove(owner, Removed);
        Add(owner, Added);
    }

    void IHistoryChange.Revert(InkCanvasNext owner)
    {
        Remove(owner, Added);
        Add(owner, Removed);
    }

    private static void Remove(InkCanvasNext owner, Stroke[] strokes)
    {
        if (strokes.Length > 0)
        {
            owner.InnerCanvas.Strokes.Remove([with(strokes)]);
        }
    }

    private static void Add(InkCanvasNext owner, Stroke[] strokes)
    {
        if (strokes.Length > 0)
        {
            owner.InnerCanvas.Strokes.Add([with(strokes)]);
        }
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
/// 结构性变化携带 Added/Removed；选区整体变换（移动/缩放/旋转）无增删，仅 TransformOnly 为 true。
/// </summary>
public sealed class InkCanvasStrokesChangedEventArgs : EventArgs
{
    private static new readonly IReadOnlyList<Stroke> Empty = Array.AsReadOnly(Array.Empty<Stroke>( ));

    internal InkCanvasStrokesChangedEventArgs(Stroke[] added, Stroke[] removed, bool transformOnly)
    {
        Added = added.Length == 0 ? Empty : Array.AsReadOnly(added);
        Removed = removed.Length == 0 ? Empty : Array.AsReadOnly(removed);
        TransformOnly = transformOnly;
    }

    public IReadOnlyList<Stroke> Added { get; }

    public IReadOnlyList<Stroke> Removed { get; }

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
    private readonly RingBuffer<IHistoryChange> history;
    private bool applyingUndoRedo;

    internal int Position { get; private set; }

    internal int RedoDepth => history.Count - Position;

    private void PushChange(IHistoryChange change)
    {
        if (Position < history.Count)
        {
            history.Truncate(Position);
        }

        history.Enqueue(change);
        Position = history.Count;
        UpdateCanUndoRedo( );
    }

    private void OnStrokesChanged(object sender, StrokeCollectionChangedEventArgs e)
    {
        selection.ApplyCanvasChanges(e.Added, e.Removed);

        if (e.Removed.Count > 0)
        {
            selection.Prune( );
        }

        var shouldPush = !applyingUndoRedo && !eraser.Active && history.Capacity > 0
            && (e.Added.Count > 0 || e.Removed.Count > 0);

        if (StrokesChanged is not null || shouldPush)
        {
            var added = e.Added.ToArray( );
            var removed = e.Removed.ToArray( );
            StrokesChanged?.Invoke(this, new InkCanvasStrokesChangedEventArgs(added, removed, transformOnly: false));

            if (shouldPush)
            {
                PushChange(new StrokeChanges(added, removed));
            }
        }

        UpdateEdgeHints( );
    }

    /// <summary>
    /// 撤销上一步操作
    /// </summary>
    public void Undo( )
    {
        if (Position == 0)
        {
            return;
        }

        applyingUndoRedo = true;
        try
        {
            Position--;
            history[Position].Revert(this);
        }
        finally
        {
            // 无论 Revert 成败都复位，否则后续变更不会入栈（静默失去撤销能力）
            applyingUndoRedo = false;
        }

        UndoRedoRefresh( );
    }

    /// <summary>
    /// 重做已撤销的操作
    /// </summary>
    public void Redo( )
    {
        if (Position >= history.Count)
        {
            return;
        }

        applyingUndoRedo = true;
        try
        {
            var change = history[Position];
            Position++;
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
        Position = 0;
        UpdateCanUndoRedo( );
    }

    private void UpdateCanUndoRedo( )
    {
        var canUndo = Position > 0;
        var canRedo = Position < history.Count;

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
    /// 导出当前历史快照到 <paramref name="old"/>，并用 <paramref name="new"/> 替换现有历史
    /// </summary>
    public void SwapHistory(out HistorySnapshot? old, HistorySnapshot? @new)
    {
        old = history.Count > 0 ? new HistorySnapshot(history.ToArray( ), Position) : null;
        history.Clear( );
        Position = 0;

        if (@new is not null)
        {
            for (var i = 0; i < @new.Changes.Count; i++)
            {
                history.Enqueue(@new.Changes[i]);
            }

            // 快照条目超出当前容量时头部被丢弃，指针按丢弃条数前移
            Position = @new.Position - (@new.Changes.Count - history.Count);
        }

        Position = Math.Clamp(Position, 0, history.Count);
        UpdateCanUndoRedo( );
    }
}
