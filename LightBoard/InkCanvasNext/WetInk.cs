using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasNext;

// 湿墨自绘层：笔锋开启时替代 DynamicRenderer 显示在绘笔画
// 湿墨与干墨都由 Stroke 的几何生成，抬手后形状一致
internal sealed class WetInk(TaperParameters taper)
{
    private StrokeVisual? stroke;
    private int deviceId;
    private bool awaitingFrame;

    internal int PointCount => stroke?.Stroke.StylusPoints.Count ?? 0;

    internal int VisualCount => Canvas.VisualCount;

    /// <summary>
    /// 是否有湿墨笔画在途
    /// </summary>
    internal bool Active => stroke is not null;

    internal VisualCanvas Canvas { get; } = new( );

    internal void Begin(int stylusId, DrawingAttributes attributes, StylusPointCollection points)
    {
        if (stroke is not null || points.Count == 0)
        {
            return;
        }

        deviceId = stylusId;
        stroke = new StrokeVisual(attributes.Clone( ), points[0], Taper.Effective(taper, attributes));
        stroke.SetVisualCanvas(Canvas);

        for (var i = 1; i < points.Count; i++)
        {
            stroke.Add(points[i]);
        }

        stroke.Redraw( );
    }

    internal void Add(int stylusId, StylusPointCollection points)
    {
        if (stroke is null || stylusId != deviceId)
        {
            return;
        }

        foreach (var point in points)
        {
            stroke.Add(point);
        }

        HookFrame( );
    }

    /// <summary>
    /// 结束指定设备的湿墨显示；设备不符时不变
    /// </summary>
    internal void Clear(int stylusId)
    {
        if (stroke is not null && stylusId != deviceId)
        {
            return;
        }

        Abort( );
    }

    /// <summary>
    /// 清空湿墨显示与帧订阅
    /// </summary>
    internal void Abort( )
    {
        Clear( );
    }

    private void Clear( )
    {
        UnhookFrame( );
        stroke?.Cleanup( );
        stroke = null;
    }

    private void HookFrame( )
    {
        if (awaitingFrame)
        {
            return;
        }

        awaitingFrame = true;
        CompositionTarget.Rendering += Frame;
    }

    private void UnhookFrame( )
    {
        if (!awaitingFrame)
        {
            return;
        }

        awaitingFrame = false;
        CompositionTarget.Rendering -= Frame;
    }

    private void Frame(object? o, EventArgs e)
    {
        if (stroke?.NeedsRedraw != true)
        {
            UnhookFrame( );
            return;
        }

        stroke.Redraw( );
    }
}

/// <summary>
/// 笔画预览层
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
    // 活动段每帧整段重建并重新拟合，窗口越短，已画部分的形状变化越小
    private const int SegmentPoints = 16;

    private readonly DrawingAttributes drawingAttributes = drawingAttributes;

    // 高亮笔的笔迹半透明，段间共享的接头点会被两段各画一次而叠色，因此整笔重绘
    private readonly bool segmentable = !drawingAttributes.IsHighlighter;

    private readonly List<DrawingVisual> segments = [];

    private VisualCanvas? visualCanvas;
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

    internal void SetVisualCanvas(VisualCanvas visualCanvas)
    {
        this.visualCanvas = visualCanvas;
    }

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

        if (visualCanvas == null || Stroke.StylusPoints.Count == 0)
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
            visualCanvas?.RemoveVisual(segment);
        }

        segments.Clear( );

        if (activeVisual is null || visualCanvas is null)
        {
            return;
        }

        visualCanvas.RemoveVisual(activeVisual);
        activeVisual = null;
    }

    private void FreezeSegment( )
    {
        var count = Stroke.StylusPoints.Count;
        var end = count - Taper.Tail(count, taper);
        if (visualCanvas is null || end <= frozenCount || SegmentGeometry(frozenCount, end) is not { } geometry)
        {
            return;
        }

        var visual = new DrawingVisual( );
        using (var context = visual.RenderOpen( ))
        {
            context.DrawGeometry(brush ??= CreateBrush( ), null, geometry);
        }

        visualCanvas.AddVisual(visual);
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
        visualCanvas!.AddVisual(visual);
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
