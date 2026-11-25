using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasNext;

/// <summary>
/// 历史记录项：Apply 为重做方向，Revert 为撤销方向
/// </summary>
internal interface IHistoryChange
{
    void Apply(InkCanvasNext owner);
    void Revert(InkCanvasNext owner);

    /// <summary>
    /// 内容坐标整体平移后校正记录；笔画自身坐标已随平移变化，只有带绝对锚点的矩阵需要改写
    /// </summary>
    void Translate(Vector shift);
}

internal sealed class StrokeChanges(IReadOnlyList<Stroke> added, IReadOnlyList<Stroke> removed) : IHistoryChange
{
    private readonly List<Stroke> added = [.. added];
    private readonly List<Stroke> removed = [.. removed];

    /// <summary>
    /// 同一批次内先增后删的笔画相互抵消
    /// </summary>
    internal void TrackAdded(Stroke stroke)
    {
        if (!removed.Remove(stroke))
        {
            added.Add(stroke);
        }
    }

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

    void IHistoryChange.Translate(Vector shift)
    {
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
internal sealed class TransformChanges(IReadOnlyList<Stroke> target, Matrix delta) : IHistoryChange
{
    private Matrix delta = delta;

    void IHistoryChange.Apply(InkCanvasNext owner)
    {
        foreach (var stroke in target)
        {
            stroke.Transform(delta, false);
        }
    }

    void IHistoryChange.Revert(InkCanvasNext owner)
    {
        var inverse = delta;
        inverse.Invert( );
        foreach (var stroke in target)
        {
            stroke.Transform(inverse, false);
        }
    }

    void IHistoryChange.Translate(Vector shift)
    {
        // 缩放与旋转矩阵带绝对锚点，整体平移后要换成新坐标系中的等效变换
        // WPF 矩阵按行向量生效，a * b 表示先 a 后 b，故先还原平移、再变换、最后重新平移
        var forward = new Matrix(1, 0, 0, 1, shift.X, shift.Y);
        var backward = new Matrix(1, 0, 0, 1, -shift.X, -shift.Y);
        delta = backward * delta * forward;
    }
}

/// <summary>
/// 结构性变化携带 Added/Removed，选区整体变换无增删且 TransformOnly 为 true
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
        // Added/Removed 只物化一次，历史项与事件参数共用同一份数组
        Stroke[] added = e.Added.Count == 0 ? [] : [.. e.Added];
        Stroke[] removed = e.Removed.Count == 0 ? [] : [.. e.Removed];

        selection.ApplyCanvasChanges(added, removed);

        // 原生书写、多指书写与形状提交产生的墨迹可以越出画布四边：新增笔画越界时画布随之扩展
        if (added.Length > 0)
        {
            FitBounds(e.Added.GetBounds( ));
        }

        if (removed.Length > 0)
        {
            selection.Prune( );
        }

        var historyEnabled = history.Capacity > 0;
        var erasing = (eraser.Recording || nativeEraseActive) && historyEnabled;
        var shouldPush = !erasing && historyEnabled && !applyingUndoRedo
            && (added.Length > 0 || removed.Length > 0);

        if (nativeEraseActive && (added.Length > 0 || removed.Length > 0))
        {
            nativeEraseChanged = true;
        }

        // 入栈先于通知：宿主在回调里改画布时，嵌套变更排在后面
        if (erasing)
        {
            TrackEraseChange(added, removed);
        }
        else if (shouldPush)
        {
            PushChange(new StrokeChanges(added, removed));
        }

        StrokesChanged?.Invoke(this, new InkCanvasStrokesChangedEventArgs(added, removed, transformOnly: false));

        UpdateEdgeHints( );

        if (!eraser.Recording && !nativeEraseActive && !applyingUndoRedo && added.Length > 0)
        {
            RaiseOperationCompleted(InkCanvasOperation.Stroke);
        }
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
        ResetNativeErase( );
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

        // 命令的 CanExecute 只在重查时刷新，撤销与重做按钮要立刻跟着状态变
        CommandManager.InvalidateRequerySuggested( );
    }

    public void SwapHistory(out HistorySnapshot? old, HistorySnapshot? @new)
    {
        SealEraseBatch( );
        ResetNativeErase( );

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
