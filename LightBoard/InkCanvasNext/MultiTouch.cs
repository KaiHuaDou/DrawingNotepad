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
    private readonly Dictionary<int, VisualCanvas> multiTouchVisuals = [];

    private void StartMultiTouchStroke(int touchId, Point canvasPoint)
    {
        var attributes = Canvas.DefaultDrawingAttributes.Clone( );
        var strokeVisual = new StrokeVisual(attributes);
        var visualCanvas = new VisualCanvas( );
        strokeVisual.SetVisualCanvas(visualCanvas);

        multiTouchStrokes[touchId] = strokeVisual;
        multiTouchVisuals[touchId] = visualCanvas;
        Canvas.Children.Add(visualCanvas);

        strokeVisual.Add(new StylusPoint(canvasPoint.X, canvasPoint.Y, 0.5f));
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

        var stroke = strokeVisual.Stroke;
        if (stroke?.StylusPoints.Count > 0)
        {
            Canvas.Strokes.Add(stroke);
        }

        if (multiTouchVisuals.TryGetValue(touchId, out var visualCanvas))
        {
            Canvas.Children.Remove(visualCanvas);
        }

        multiTouchStrokes.Remove(touchId);
        multiTouchVisuals.Remove(touchId);
    }

    private void EndMultiTouch( )
    {
        foreach (var id in multiTouchStrokes.Keys.ToList( ))
        {
            EndMultiTouchStroke(id);
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

    public void Clear( )
    {
        foreach (var visual in visuals)
        {
            RemoveVisualChild(visual);
        }

        visuals.Clear( );
    }

    protected override Visual GetVisualChild(int index)
    {
        return visuals[index];
    }

    protected override int VisualChildrenCount => visuals.Count;
}

internal sealed class StrokeVisual(DrawingAttributes drawingAttributes)
{
    private readonly DrawingAttributes drawingAttributes = drawingAttributes;
    private VisualCanvas? visualCanvas;
    private DrawingVisual? activeVisual;
    private int lastCommittedPointCount;
    private const int CommitPointThreshold = 24;

    public Stroke? Stroke { get; private set; }

    public void SetVisualCanvas(VisualCanvas visualCanvas)
    {
        this.visualCanvas = visualCanvas;
    }

    public void Add(StylusPoint point)
    {
        if (Stroke == null)
        {
            var collection = new StylusPointCollection { point };
            Stroke = new Stroke(collection) { DrawingAttributes = drawingAttributes };
        }
        else
        {
            Stroke.StylusPoints.Add(point);
        }
    }

    public void Redraw( )
    {
        if (Stroke == null || visualCanvas == null)
        {
            return;
        }

        var currentCount = Stroke.StylusPoints.Count;
        if (currentCount == 0)
        {
            return;
        }

        if (activeVisual == null)
        {
            activeVisual = new DrawingVisual( );
            visualCanvas.AddVisual(activeVisual);
        }

        var startIndex = lastCommittedPointCount == 0 ? 0 : lastCommittedPointCount - 1;
        using var context = activeVisual.RenderOpen( );
        DrawSegment(context, startIndex, currentCount);

        if (currentCount - lastCommittedPointCount >= CommitPointThreshold)
        {
            lastCommittedPointCount = currentCount;
            activeVisual = null;
        }
    }

    public void ForceRedraw( )
    {
        if (Stroke == null || visualCanvas == null)
        {
            return;
        }

        if (Stroke.StylusPoints.Count < lastCommittedPointCount)
        {
            visualCanvas.Clear( );
            activeVisual = null;
            lastCommittedPointCount = 0;
        }

        Redraw( );
    }

    private void DrawSegment(DrawingContext context, int startIndex, int endIndex)
    {
        if (startIndex >= endIndex || Stroke == null)
        {
            return;
        }

        var count = endIndex - startIndex;
        var points = new StylusPointCollection(count);
        for (var i = startIndex; i < endIndex; i++)
        {
            points.Add(Stroke.StylusPoints[i]);
        }

        var segment = new Stroke(points) { DrawingAttributes = drawingAttributes };
        segment.Draw(context);
    }
}
