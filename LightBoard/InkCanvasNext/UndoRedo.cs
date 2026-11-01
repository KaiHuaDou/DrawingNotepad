using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasNext;

/// <summary>
/// 历史记录项抽象：Apply 为重做方向，Revert 为撤销方向
/// 参数 owner 用于访问 InkCanvas 以执行增删（StrokeChanges）或纯变换（TransformChanges）
/// </summary>
internal interface IHistoryChange
{
    void Apply(InkCanvasNext owner);
    void Revert(InkCanvasNext owner);
}

internal sealed class StrokeChanges(IReadOnlyList<Stroke> added, IReadOnlyList<Stroke> removed) : IHistoryChange
{
    private readonly List<Stroke> added = [.. added];
    private readonly List<Stroke> removed = [.. removed];

    /// <summary>
    /// 追加擦除产生的新笔画；同一批次内先增后删的笔画相互抵消
    /// </summary>
    internal void TrackAdded(Stroke stroke)
    {
        if (!removed.Remove(stroke))
        {
            added.Add(stroke);
        }
    }

    /// <summary>
    /// 追加擦除去掉的笔画；与同批次的新增相互抵消
    /// </summary>
    internal void TrackRemoved(Stroke stroke)
    {
        if (!added.Remove(stroke))
        {
            removed.Add(stroke);
        }
    }

    void IHistoryChange.Apply(InkCanvasNext owner)
    {
        RemovePresent(owner, removed);
        AddAbsent(owner, added);
    }

    void IHistoryChange.Revert(InkCanvasNext owner)
    {
        RemovePresent(owner, added);
        AddAbsent(owner, removed);
    }

    // StrokeCollection 的增删对缺失与重复的笔画都抛异常：重放次序出意外时按画布现状挑出能执行的项，
    // 撤销表现为漏还原而不是中断
    private static void RemovePresent(InkCanvasNext owner, List<Stroke> strokes)
    {
        if (strokes.Count == 0)
        {
            return;
        }

        var canvas = owner.InnerCanvas.Strokes;
        StrokeCollection? present = null;
        foreach (var stroke in strokes)
        {
            if (canvas.Contains(stroke))
            {
                (present ??= []).Add(stroke);
            }
        }

        if (present is not null)
        {
            canvas.Remove(present);
        }
    }

    private static void AddAbsent(InkCanvasNext owner, List<Stroke> strokes)
    {
        if (strokes.Count == 0)
        {
            return;
        }

        var canvas = owner.InnerCanvas.Strokes;
        StrokeCollection? absent = null;
        foreach (var stroke in strokes)
        {
            if (!canvas.Contains(stroke))
            {
                (absent ??= []).Add(stroke);
            }
        }

        if (absent is not null)
        {
            canvas.Add(absent);
        }
    }
}

/// <summary>
/// 一次手势（移动/缩放/旋转）的整段绝对变换，作为单个撤销单元
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
/// 结构性变化携带 Added/Removed；选区整体变换（移动/缩放/旋转）无增删，仅 TransformOnly 为 true
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

    /// <summary>
    /// 擦除周期内持续追加的历史项；非空表示批次还开着
    /// </summary>
    private StrokeChanges? eraseBatch;

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

        var historyEnabled = history.Capacity > 0;
        var erasing = eraser.Recording && historyEnabled;
        var shouldPush = !erasing && historyEnabled && !applyingUndoRedo
            && (e.Added.Count > 0 || e.Removed.Count > 0);

        // 入栈先于通知：宿主在回调里改画布时，嵌套变更排在后面
        if (erasing)
        {
            TrackEraseChange(e.Added, e.Removed);
        }
        else if (shouldPush)
        {
            PushChange(new StrokeChanges(e.Added, e.Removed));
        }

        StrokesChanged?.Invoke(this, new InkCanvasStrokesChangedEventArgs([.. e.Added], [.. e.Removed], transformOnly: false));

        UpdateEdgeHints( );
    }

    /// <summary>
    /// 给开口批次封口：批次留在历史里，之后的擦除开新项
    /// </summary>
    private void SealEraseBatch( )
    {
        eraseBatch = null;
    }

    private void TrackEraseChange(IReadOnlyList<Stroke> added, IReadOnlyList<Stroke> removed)
    {
        var batch = eraseBatch ??= OpenEraseBatch( );

        foreach (var stroke in removed)
        {
            batch.TrackRemoved(stroke);
        }

        foreach (var stroke in added)
        {
            batch.TrackAdded(stroke);
        }
    }

    private StrokeChanges OpenEraseBatch( )
    {
        var batch = new StrokeChanges([], []);
        PushChange(batch);
        return batch;
    }

    /// <summary>
    /// 撤销上一步操作；擦除手势中调用时，已擦除的部分作为独立一步回退
    /// </summary>
    public void Undo( )
    {
        if (Position == 0)
        {
            return;
        }

        SealEraseBatch( );

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

        SealEraseBatch( );

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

        if (CanUndo == canUndo && CanRedo == canRedo)
        {
            return;
        }

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

        // 命令的 CanExecute 只在重查时重算，撤销与重做按钮要立刻跟着状态变
        CommandManager.InvalidateRequerySuggested( );
    }

    /// <summary>
    /// 导出当前历史快照到 <paramref name="old"/>，并用 <paramref name="new"/> 替换现有历史
    /// </summary>
    public void SwapHistory(out HistorySnapshot? old, HistorySnapshot? @new)
    {
        SealEraseBatch( );

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
