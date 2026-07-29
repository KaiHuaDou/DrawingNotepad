using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    private readonly Dictionary<int, StrokeVisual> multiTouchStrokes = [];
    private readonly VisualCanvas multiTouchCanvas = new( );

    private void StartMultiTouchStroke(int touchId, Point canvasPoint)
    {
        var attributes = Canvas.DefaultDrawingAttributes.Clone( );
        var initialPoint = new StylusPoint(canvasPoint.X, canvasPoint.Y, 0.5f);
        var strokeVisual = new StrokeVisual(attributes, initialPoint);
        strokeVisual.SetVisualCanvas(multiTouchCanvas);

        multiTouchStrokes[touchId] = strokeVisual;
        strokeVisual.Redraw( );
    }

    private void StartMultiTouch( )
    {
        foreach (var (id, (device, _)) in touches)
        {
            if (multiTouchStrokes.ContainsKey(id))
            {
                continue;
            }

            StartMultiTouchStroke(id, device.GetTouchPoint(Canvas).Position);
        }
    }

    private void ContinueMultiTouchStroke(int touchId, Point canvasPoint)
    {
        if (!multiTouchStrokes.TryGetValue(touchId, out var strokeVisual))
        {
            return;
        }

        strokeVisual.Add(new StylusPoint(canvasPoint.X, canvasPoint.Y, 0.5f));
        strokeVisual.Redraw( );
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
            Canvas.Strokes.Add(stroke);
        }

        strokeVisual.Cleanup( );

        multiTouchStrokes.Remove(touchId);
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
    }
}

internal sealed class VisualCanvas : FrameworkElement
{
    private readonly List<DrawingVisual> visuals = [];

    public VisualCanvas( )
    {
        CacheMode = new BitmapCache( );
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
        RenderOptions.SetCachingHint(this, CachingHint.Cache);
    }

    public void AddVisual(DrawingVisual visual)
    {
        visuals.Add(visual);
        AddVisualChild(visual);
    }

    public void RemoveVisual(DrawingVisual visual)
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

internal sealed class StrokeVisual(DrawingAttributes drawingAttributes, StylusPoint initialPoint)
{
    private readonly DrawingAttributes drawingAttributes = drawingAttributes;
    private VisualCanvas? visualCanvas;
    private DrawingVisual? activeVisual;

    public Stroke Stroke { get; } = new Stroke([initialPoint], drawingAttributes);

    public void SetVisualCanvas(VisualCanvas visualCanvas)
    {
        this.visualCanvas = visualCanvas;
    }

    public void Add(StylusPoint point)
    {
        Stroke.StylusPoints.Add(point);
    }

    public void Redraw( )
    {
        if (visualCanvas == null || Stroke.StylusPoints.Count == 0)
        {
            return;
        }

        activeVisual ??= CreateAndAttachVisual( );

        using var context = activeVisual.RenderOpen( );
        var geometry = Stroke.GetGeometry(drawingAttributes);
        context.DrawGeometry(CreateBrush( ), null, geometry);
    }

    public Stroke? BuildStroke( )
    {
        return Stroke.StylusPoints.Count > 0 ? Stroke : null;
    }

    public void Cleanup( )
    {
        if (activeVisual is null || visualCanvas is null)
        {
            return;
        }

        visualCanvas.RemoveVisual(activeVisual);
        activeVisual = null;
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
            brush.Opacity = 0.5;
        }

        brush.Freeze( );
        return brush;
    }
}
