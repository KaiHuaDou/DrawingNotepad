using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;

namespace InkCanvasNext;

/// <summary>
/// 自绘选择控制器，替代 InkCanvas 内置选择
/// 坐标一律为内容坐标（已含 CanvasGrid 缩放），与视图变换解耦
/// </summary>
internal sealed class SelectionController(InkCanvasNext owner, SelectionVisual visual)
{
    private readonly InkCanvasNext owner = owner;
    private readonly SelectionVisual visual = visual;
    private readonly HashSet<Stroke> selectedStrokes = [];
    private readonly HashSet<Stroke> canvasStrokes = [];

    private static readonly IReadOnlyList<Stroke> EmptySnapshot = Array.AsReadOnly(Array.Empty<Stroke>( ));

    // 选区变化时一次性生成快照，读取方共用

    internal IReadOnlyCollection<Stroke> SelectedStrokes => selectedStrokes;

    internal IReadOnlyList<Stroke> SelectedStrokesSnapshot { get; private set; } = EmptySnapshot;

    internal Stroke[] SelectedStrokesArray { get; private set; } = [];

    internal bool HasSelection => selectedStrokes.Count > 0;

    internal Rect Bounds { get; private set; } = Rect.Empty;

    internal void SyncCanvasStrokes(StrokeCollection strokes)
    {
        canvasStrokes.Clear( );
        foreach (var stroke in strokes)
        {
            canvasStrokes.Add(stroke);
        }
    }

    internal void ApplyCanvasChanges(IEnumerable<Stroke> added, IEnumerable<Stroke> removed)
    {
        foreach (var stroke in added)
        {
            canvasStrokes.Add(stroke);
        }

        foreach (var stroke in removed)
        {
            canvasStrokes.Remove(stroke);
        }
    }

    internal void SetStrokes(IEnumerable<Stroke> strokes, bool halo = true)
    {
        selectedStrokes.Clear( );
        foreach (var s in strokes)
        {
            if (canvasStrokes.Contains(s))
            {
                selectedStrokes.Add(s);
            }
        }

        RebuildSnapshot( );
        RecomputeBounds( );
        Invalidate(halo);
        owner.RaiseSelectionChanged( );
    }

    internal void Clear( )
    {
        if (selectedStrokes.Count == 0)
        {
            return;
        }

        selectedStrokes.Clear( );
        RebuildSnapshot( );
        Bounds = Rect.Empty;
        Invalidate( );
        owner.RaiseSelectionChanged( );
    }

    private void RebuildSnapshot( )
    {
        SelectedStrokesArray = selectedStrokes.Count == 0 ? [] : [.. selectedStrokes];
        SelectedStrokesSnapshot = SelectedStrokesArray.Length == 0
            ? EmptySnapshot
            : Array.AsReadOnly(SelectedStrokesArray);
    }

    internal void RecomputeBounds( )
    {
        // Rect 是结构体：经由属性调用实例方法 Union 只会修改 getter 返回的临时副本（静默丢弃），
        // 必须用局部变量累计后一次性赋回属性，否则 Bounds 始终为 Rect.Empty
        var result = Rect.Empty;
        foreach (var s in selectedStrokes)
        {
            result = Rect.Union(result, s.GetBounds( ));
        }

        Bounds = result;
    }

    /// <summary>
    /// 段起始包围盒的四个角乘矩阵求 AABB，供逐帧更新；只适用于无旋转分量的变换，
    /// 有旋转时结果会大于真实包围盒，由调用方改用 <see cref="RecomputeBounds"/>
    /// </summary>
    internal void TransformBounds(Rect bounds, Matrix delta)
    {
        if (bounds.IsEmpty)
        {
            return;
        }

        var topLeft = delta.Transform(bounds.TopLeft);
        var topRight = delta.Transform(bounds.TopRight);
        var bottomRight = delta.Transform(bounds.BottomRight);
        var bottomLeft = delta.Transform(bounds.BottomLeft);

        var minX = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomRight.X, bottomLeft.X));
        var minY = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomRight.Y, bottomLeft.Y));
        var maxX = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomRight.X, bottomLeft.X));
        var maxY = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomRight.Y, bottomLeft.Y));

        Bounds = new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    internal void Prune( )
    {
        if (selectedStrokes.Count == 0)
        {
            return;
        }

        var before = selectedStrokes.Count;
        selectedStrokes.RemoveWhere(s => !canvasStrokes.Contains(s));

        if (selectedStrokes.Count == before)
        {
            return;
        }

        RebuildSnapshot( );
        RecomputeBounds( );
        Invalidate( );
        owner.RaiseSelectionChanged( );
    }

    internal void Invalidate(bool halo = true)
    {
        visual.Render(Bounds, SelectedStrokesArray, null, halo, owner.CurrentScale, owner.RotateHandleLive);
    }

    internal void InvalidateLasso(IReadOnlyList<Point>? lasso)
    {
        // 套索在途时选择未定，不画 halo
        visual.Render(Bounds, SelectedStrokesArray, lasso, halo: false, owner.CurrentScale, owner.RotateHandleLive);
    }
}
