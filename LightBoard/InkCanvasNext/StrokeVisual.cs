using System.Collections.Generic;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasNext;

// 多指书写与选区覆盖共用的自绘层：在 UI 线程按帧重建活动段几何
/// <summary>
/// 自绘图层容器：按添加顺序承载 DrawingVisual，不做布局、裁剪与命中测试
/// 多指笔画与选区覆盖层共用
/// </summary>
internal sealed class VisualCanvas : FrameworkElement
{
    private readonly List<DrawingVisual> visuals = [];

    internal int VisualCount => visuals.Count;

    internal void AddVisual(DrawingVisual visual)
    {
        visuals.Add(visual);
        AddVisualChild(visual);
    }

    internal void RemoveVisual(DrawingVisual visual)
    {
        visuals.Remove(visual);
        RemoveVisualChild(visual);
    }

    protected override Visual GetVisualChild(int index)
    {
        return visuals[index];
    }

    protected override int VisualChildrenCount => visuals.Count;
}

internal sealed partial class StrokeVisual(DrawingAttributes drawingAttributes, StylusPoint initialPoint, TaperParameters? taper)
{
    // 活动段每帧整段重建并重新拟合，窗口越短，已画部分的形状变化越小；
    // 冻结窗口越大，段间接缝越少（64 点约等于 4 个触摸报文）
    private const int SegmentPoints = 64;

    private readonly DrawingAttributes drawingAttributes = drawingAttributes;

    // 高亮笔的笔迹半透明，段间共享的接头点会被两段各画一次而叠色，因此整笔重绘
    private readonly bool segmentable = !drawingAttributes.IsHighlighter;

    private readonly List<DrawingVisual> segments = [];

    private DrawingVisual? activeVisual;
    private SolidColorBrush? brush;

    /// <summary>
    /// 已画进冻结段的点数
    /// </summary>
    private int frozenCount;

    // 触点描述由设备决定，点集沿用首点描述才能继续收点
    internal Stroke Stroke { get; } = new Stroke(
        [with(initialPoint.Description), initialPoint],
        drawingAttributes
    );

    /// <summary>
    /// 自上次重绘以来有新触点
    /// </summary>
    internal bool NeedsRedraw { get; private set; }

    /// <summary>
    /// 活动段的承载层
    /// </summary>
    internal VisualCanvas? VisualCanvas { get; set; }

    internal void Add(StylusPoint point)
    {
        Stroke.StylusPoints.Add(point);
        NeedsRedraw = true;

        var count = Stroke.StylusPoints.Count;
        if (segmentable && count - Taper.Tail(count, taper) - frozenCount >= SegmentPoints)
        {
            FreezeSegment( );
        }
    }

    internal void Redraw( )
    {
        NeedsRedraw = false;

        if (VisualCanvas == null || Stroke.StylusPoints.Count == 0)
        {
            return;
        }

        activeVisual ??= CreateAndAttachVisual( );

        // 只重建活动段
        using var context = activeVisual.RenderOpen( );
        if (SegmentGeometry(frozenCount, Stroke.StylusPoints.Count) is { } geometry)
        {
            context.DrawGeometry(brush ??= CreateBrush( ), null, geometry);
        }
    }

    internal Stroke? BuildStroke( )
    {
        if (Stroke.StylusPoints.Count == 0)
        {
            return null;
        }

        Taper.Apply(Stroke, taper);

        return Stroke;
    }

    internal void Cleanup( )
    {
        foreach (var segment in segments)
        {
            VisualCanvas?.RemoveVisual(segment);
        }

        segments.Clear( );

        if (activeVisual is null || VisualCanvas is null)
        {
            return;
        }

        VisualCanvas.RemoveVisual(activeVisual);
        activeVisual = null;
    }

    private void FreezeSegment( )
    {
        var count = Stroke.StylusPoints.Count;
        var end = count - Taper.Tail(count, taper);
        if (VisualCanvas is null || end <= frozenCount || SegmentGeometry(frozenCount, end) is not { } geometry)
        {
            return;
        }

        var visual = new DrawingVisual( );
        using (var context = visual.RenderOpen( ))
        {
            context.DrawGeometry(brush ??= CreateBrush( ), null, geometry);
        }

        VisualCanvas.AddVisual(visual);
        segments.Add(visual);

        // 同一点留在下一段起点，避免接头处出现缺口；两段的端帽重合，不透明笔刷下看不出接缝
        frozenCount = end - 1;
    }

    private System.Windows.Media.Geometry? SegmentGeometry(int start, int end)
    {
        if (end <= start)
        {
            return null;
        }

        return new Stroke(Taper.Copy(Stroke.StylusPoints, start, end, taper), drawingAttributes).GetGeometry(drawingAttributes);
    }

    private DrawingVisual CreateAndAttachVisual( )
    {
        var visual = new DrawingVisual( );
        VisualCanvas!.AddVisual(visual);
        return visual;
    }

    private SolidColorBrush CreateBrush( )
    {
        var brush = new SolidColorBrush(drawingAttributes.Color);

        if (drawingAttributes.IsHighlighter)
        {
            brush.Opacity = HighlighterOpacity;
        }

        brush.Freeze( );
        return brush;
    }
}
