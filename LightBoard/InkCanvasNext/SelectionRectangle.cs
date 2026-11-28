using System.Collections.Generic;
using System.Windows;

using static InkCanvasNext.Geometry;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    private Point rectangleCorner;
    private bool rectangleDragged;

    private void BeginRectangle(Point p)
    {
        selectionGesture = SelectionGesture.Rectangle;
        rectangleCorner = p;
        rectangleDragged = false;
        selection.InvalidateLasso(RectangleCorners(p, p));
    }

    private void UpdateRectangleTrack(Point p)
    {
        // 与套索共用采样间距
        var minDistance2 = LassoPointDistance2 / (CurrentScale * CurrentScale);
        if (Distance2(p, rectangleCorner) < minDistance2)
        {
            return;
        }

        // 起手不清选型，第二根手指落在选区内时可以切回拖动；矩形拉开后才算新的一次选择
        selection.Clear( );
        rectangleCorner = p;
        rectangleDragged = true;
        selection.InvalidateLasso(RectangleCorners(selectionStartPoint, p));
    }

    private void EndRectangle( )
    {
        var dragged = rectangleDragged;
        rectangleDragged = false;
        selection.InvalidateLasso(null);

        if (dragged)
        {
            var rect = new Rect(selectionStartPoint, rectangleCorner);
            selection.SetStrokes(InnerCanvas.Strokes.HitTest(rect, LassoHitPercentage), halo: false);
        }
        else
        {
            HandlePointSelect(selectionStartPoint, halo: false);
        }
    }

    private static IReadOnlyList<Point> RectangleCorners(Point a, Point b)
    {
        var rect = new Rect(a, b);
        return [rect.TopLeft, rect.TopRight, rect.BottomRight, rect.BottomLeft];
    }
}
