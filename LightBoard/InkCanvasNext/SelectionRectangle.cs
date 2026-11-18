using System.Collections.Generic;
using System.Windows;

using static InkCanvasNext.Geometry;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    private Point rectangleCorner;
    private bool rectangleDragged;

    /// <summary>
    /// 空白处按下（鼠标/触摸共用）：以按下点为对角起点开始矩形框选，原选型留到矩形真正拉开时再清
    /// </summary>
    private void BeginRectangle(Point p)
    {
        selectionGesture = SelectionGesture.Rectangle;
        rectangleCorner = p;
        rectangleDragged = false;
        selection.InvalidateLasso(RectangleCorners(p, p));
    }

    /// <summary>
    /// 框选拖动追加（鼠标/触摸共用）：与套索同间距去抖，更新对角点与矩形虚线视觉
    /// </summary>
    private void UpdateRectangleTrack(Point p)
    {
        var minDistance2 = LassoPointDistance2 / (CurrentScale * CurrentScale);
        if (Distance2(p, rectangleCorner) < minDistance2)
        {
            return;
        }

        // 起手不清选型，第二根手指落在选区内时可以切回拖动；矩形已经拉开才算新的一次选择
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
            // 无拖拽视为点选：命中单笔则选中，否则清除
            HandlePointSelect(selectionStartPoint, halo: false);
        }
    }

    private static IReadOnlyList<Point> RectangleCorners(Point a, Point b)
    {
        var rect = new Rect(a, b);
        return [rect.TopLeft, rect.TopRight, rect.BottomRight, rect.BottomLeft];
    }
}
