using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    private readonly Dictionary<int, StrokeVisual> multiTouchStrokes = [];
    private readonly VisualCanvas multiTouchCanvas = new( );

    /// <summary>测试接缝：多指笔画是否在等下一帧重绘（等价于帧回调已订阅）。</summary>
    internal bool AwaitsMultiTouchFrame { get; private set; }

    /// <summary>测试接缝：多指笔画挂载的绘制视觉数量（冻结段 + 活动段）。</summary>
    internal int MultiTouchVisualCount => multiTouchCanvas.VisualCount;

    private void StartMultiTouchStroke(int touchId, Point canvasPoint)
    {
        // 盖章期间 MultiDraw 被排除：多指落笔不创建笔画
        if (Tool.IsStamp)
        {
            return;
        }

        var attributes = InnerCanvas.DefaultDrawingAttributes.Clone( );
        var initialPoint = new StylusPoint(canvasPoint.X, canvasPoint.Y, DefaultPressure);
        var strokeVisual = new StrokeVisual(attributes, initialPoint);
        strokeVisual.SetVisualCanvas(multiTouchCanvas);

        multiTouchStrokes[touchId] = strokeVisual;
        strokeVisual.Redraw( );
    }

    /// <summary>
    /// 迁入 MultiDraw 前保住原生在绘笔画：InkCanvas 对中途切换编辑模式的在绘笔画只做丢弃
    /// （InkCollectionBehavior 对非 Ink 目标一律 Commit(commit: false)），对捕获丢失则整笔提交，
    /// 故先释放捕获触发提交，多指笔画随后从当前触点续接。
    /// </summary>
    private void HandOffDrawingStroke( )
    {
        if (State is TouchState.EvalDraw or TouchState.Draw
            && InnerCanvas.EditingMode == InkCanvasEditingMode.Ink
            && InnerCanvas.IsStylusCaptured)
        {
            InnerCanvas.ReleaseStylusCapture( );
        }
    }

    private void StartMultiTouch( )
    {
        foreach (var (id, (device, _)) in touches)
        {
            if (multiTouchStrokes.ContainsKey(id))
            {
                continue;
            }

            StartMultiTouchStroke(id, device.GetTouchPoint(InnerCanvas).Position);
        }
    }

    private void ContinueMultiTouchStroke(int touchId, Point canvasPoint)
    {
        if (!multiTouchStrokes.TryGetValue(touchId, out var strokeVisual))
        {
            return;
        }

        strokeVisual.Add(new StylusPoint(canvasPoint.X, canvasPoint.Y, DefaultPressure));
        HookMultiTouchFrame( );
    }

    private void EndMultiTouchStroke(int touchId)
    {
        if (!multiTouchStrokes.TryGetValue(touchId, out var strokeVisual))
        {
            return;
        }

        var stroke = strokeVisual.BuildStroke( );
        if (stroke is not null)
        {
            InnerCanvas.Strokes.Add(stroke);
        }

        strokeVisual.Cleanup( );

        multiTouchStrokes.Remove(touchId);

        if (multiTouchStrokes.Count == 0)
        {
            UnhookMultiTouchFrame( );
        }
    }

    private void EndMultiTouch( )
    {
        foreach (var id in multiTouchStrokes.Keys.ToArray( ))
        {
            EndMultiTouchStroke(id);
        }
    }

    /// <summary>
    /// 清空所有进行中的多指笔画视觉（已提交 + 未提交段）。
    /// </summary>
    public void ClearMultiTouchVisuals( )
    {
        foreach (var strokeVisual in multiTouchStrokes.Values)
        {
            strokeVisual.Cleanup( );
        }

        multiTouchStrokes.Clear( );
        UnhookMultiTouchFrame( );
    }

    private void HookMultiTouchFrame( )
    {
        if (AwaitsMultiTouchFrame)
        {
            return;
        }

        AwaitsMultiTouchFrame = true;
        CompositionTarget.Rendering += MultiTouchFrame;
    }

    private void UnhookMultiTouchFrame( )
    {
        if (!AwaitsMultiTouchFrame)
        {
            return;
        }

        AwaitsMultiTouchFrame = false;
        CompositionTarget.Rendering -= MultiTouchFrame;
    }

    /// <summary>
    /// 帧节拍：一帧内到达的触点合并成一次几何重建。没有脏笔画时退订：订阅本身会持续排帧，
    /// 手指按住不动期间不该让 WPF 一直逐帧渲染。
    /// </summary>
    private void MultiTouchFrame(object? o, EventArgs e)
    {
        var redrawn = false;

        foreach (var strokeVisual in multiTouchStrokes.Values)
        {
            if (!strokeVisual.NeedsRedraw)
            {
                continue;
            }

            strokeVisual.Redraw( );
            redrawn = true;
        }

        if (!redrawn)
        {
            UnhookMultiTouchFrame( );
        }
    }
}

internal sealed class VisualCanvas : FrameworkElement
{
    private readonly List<DrawingVisual> visuals = [];

    internal VisualCanvas( )
    {
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
    }

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

internal sealed partial class StrokeVisual(DrawingAttributes drawingAttributes, StylusPoint initialPoint)
{
    private const int SegmentPoints = 64;

    private readonly DrawingAttributes drawingAttributes = drawingAttributes;

    // 高亮笔的笔迹半透明，段间共享的接头点会被两段各画一次而叠色，因此整笔重绘
    private readonly bool segmentable = !drawingAttributes.IsHighlighter;

    private readonly List<DrawingVisual> segments = [];

    private VisualCanvas? visualCanvas;
    private DrawingVisual? activeVisual;
    private SolidColorBrush? brush;

    /// <summary>已画进冻结段的点数；段与段共享接头点，下一段从这里开始。</summary>
    private int frozenCount;

    internal Stroke Stroke { get; } = new Stroke([initialPoint], drawingAttributes);

    /// <summary>自上次重绘以来有新触点。</summary>
    internal bool NeedsRedraw { get; private set; }

    internal void SetVisualCanvas(VisualCanvas visualCanvas)
    {
        this.visualCanvas = visualCanvas;
    }

    internal void Add(StylusPoint point)
    {
        Stroke.StylusPoints.Add(point);
        NeedsRedraw = true;

        if (segmentable && Stroke.StylusPoints.Count - frozenCount >= SegmentPoints)
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

        // 只重建活动段（点数不超过 SegmentPoints）；冻结段的几何各自缓存在独立视觉里
        using var context = activeVisual.RenderOpen( );
        if (SegmentGeometry(frozenCount, Stroke.StylusPoints.Count) is { } geometry)
        {
            context.DrawGeometry(brush ??= CreateBrush( ), null, geometry);
        }
    }

    internal Stroke? BuildStroke( )
    {
        return Stroke.StylusPoints.Count > 0 ? Stroke : null;
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
        var end = Stroke.StylusPoints.Count;
        if (visualCanvas is null || SegmentGeometry(frozenCount, end) is not { } geometry)
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

    // 返回值必须写全名：InkCanvasNext 命名空间内有同名的静态工具类 Geometry
    private System.Windows.Media.Geometry? SegmentGeometry(int start, int end)
    {
        if (end <= start)
        {
            return null;
        }

        var points = new StylusPointCollection(end - start);
        for (var i = start; i < end; i++)
        {
            points.Add(Stroke.StylusPoints[i]);
        }

        return new Stroke(points, drawingAttributes).GetGeometry(drawingAttributes);
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
