using System.Collections.Generic;
using System.Windows;
using System.Windows.Ink;

namespace InkCanvasNext;

/// <summary>
/// 自绘选择控制器：维护自有选型集合与包围盒，替代 WPF InkCanvas 内置选择。
/// 坐标一律为 InkCanvas 内容坐标（已含 CanvasGrid 缩放），与滚动/缩放解耦。
/// </summary>
internal sealed class SelectionController(InkCanvasNext owner, SelectionVisual visual)
{
    private readonly InkCanvasNext owner = owner;
    private readonly SelectionVisual visual = visual;
    private readonly HashSet<Stroke> selectedStrokes = [];

    public IReadOnlyCollection<Stroke> SelectedStrokes => selectedStrokes;

    public bool HasSelection => selectedStrokes.Count > 0;

    public Rect Bounds { get; private set; } = Rect.Empty;

    public void SetStrokes(IEnumerable<Stroke> strokes)
    {
        selectedStrokes.Clear( );
        foreach (var s in strokes)
        {
            if (owner.Canvas.Strokes.Contains(s))
            {
                selectedStrokes.Add(s);
            }
        }

        RecomputeBounds( );
        Invalidate( );
        owner.RaiseSelectionChanged( );
    }

    public void Clear( )
    {
        if (selectedStrokes.Count == 0)
        {
            return;
        }

        selectedStrokes.Clear( );
        Bounds = Rect.Empty;
        Invalidate( );
        owner.RaiseSelectionChanged( );
    }

    public void RecomputeBounds( )
    {
        // Rect 是结构体：经由属性调用实例方法 Union 只会修改 getter 返回的临时副本（静默丢弃），
        // 必须用局部变量累计后一次性赋回属性，否则 Bounds 恒为 Rect.Empty、选择框/手柄永不渲染
        var result = Rect.Empty;
        foreach (var s in selectedStrokes)
        {
            result = Rect.Union(result, s.GetBounds( ));
        }

        Bounds = result;
    }

    /// <summary>从画布移除的笔画需从选型中剔除（删除/撤销/切页等）。</summary>
    public void Prune(StrokeCollection canvasStrokes)
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

        RecomputeBounds( );
        Invalidate( );
        owner.RaiseSelectionChanged( );
    }

    /// <summary>刷新选择视觉；halo 仅在选择确定（无进行中手势）时绘制，手势期间传 false 逐帧跳过克隆开销。</summary>
    public void Invalidate(bool halo = true)
    {
        visual.Invalidate(Bounds, selectedStrokes, null, halo);
    }

    public void InvalidateLasso(IReadOnlyList<Point>? lasso)
    {
        // 套索进行中：选择尚未确定，不绘制 halo
        visual.Invalidate(Bounds, selectedStrokes, lasso, halo: false);
    }
}
