using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

using static InkCanvasNext.Geometry;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    private Point shapeStart;
    private Point shapeEnd;
    private bool shapeActive;

    private bool IsShapeMode => Mode is InkCanvasNextMode.Line or InkCanvasNextMode.Circle;
    private bool IsCircle => Mode == InkCanvasNextMode.Circle;

    /// <summary>形状终点触点（InnerCanvas 坐标系）：恒取插入序第一指（形状归属指），
    /// 其余手指的移动与抬起均不参与。</summary>
    private Point GetShapeEndPoint( )
    {
        using var enumerator = touches.Values.GetEnumerator( );
        enumerator.MoveNext( );
        return enumerator.Current.Device.GetTouchPoint(InnerCanvas).Position;
    }

    private void StartShape(Point point)
    {
        shapeStart = point;
        shapeEnd = point;
        shapeActive = true;

        if (IsCircle)
        {
            Canvas.SetLeft(ShapePreviewEllipse, point.X);
            Canvas.SetTop(ShapePreviewEllipse, point.Y);
            ShapePreviewEllipse.Width = 0;
            ShapePreviewEllipse.Height = 0;
            ShapePreviewEllipse.Visibility = Visibility.Visible;
            ShapePreviewLine.Visibility = Visibility.Collapsed;
        }
        else
        {
            ShapePreviewLine.X1 = point.X;
            ShapePreviewLine.Y1 = point.Y;
            ShapePreviewLine.X2 = point.X;
            ShapePreviewLine.Y2 = point.Y;
            ShapePreviewLine.Visibility = Visibility.Visible;
            ShapePreviewEllipse.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateShape(Point point)
    {
        if (!shapeActive)
        {
            return;
        }

        var end = point;
        if (!IsCircle && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            end = ConstrainLine(shapeStart, point);
        }

        shapeEnd = end;

        var attrs = DefaultDrawingAttributes;
        var brush = new SolidColorBrush(attrs.Color);
        var width = Math.Max(attrs.Width, ShapeMinStrokeWidth);

        if (IsCircle)
        {
            var r = Distance(shapeStart, end);
            Canvas.SetLeft(ShapePreviewEllipse, shapeStart.X - r);
            Canvas.SetTop(ShapePreviewEllipse, shapeStart.Y - r);
            ShapePreviewEllipse.Width = 2 * r;
            ShapePreviewEllipse.Height = 2 * r;
            ShapePreviewEllipse.Stroke = brush;
            ShapePreviewEllipse.StrokeThickness = width;
        }
        else
        {
            ShapePreviewLine.X1 = shapeStart.X;
            ShapePreviewLine.Y1 = shapeStart.Y;
            ShapePreviewLine.X2 = end.X;
            ShapePreviewLine.Y2 = end.Y;
            ShapePreviewLine.Stroke = brush;
            ShapePreviewLine.StrokeThickness = width;
        }
    }

    private static Point ConstrainLine(Point start, Point end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var angle = Math.Atan2(dy, dx);
        var snapped = Math.Round(angle / (Math.PI / 4)) * (Math.PI / 4);
        var length = Math.Sqrt(dx * dx + dy * dy);
        return new Point(start.X + length * Math.Cos(snapped), start.Y + length * Math.Sin(snapped));
    }

    private void CommitShape( )
    {
        if (!shapeActive)
        {
            return;
        }

        shapeActive = false;
        ShapePreviewLine.Visibility = Visibility.Collapsed;
        ShapePreviewEllipse.Visibility = Visibility.Collapsed;

        if (Distance(shapeStart, shapeEnd) < ShapeCommitMinDistance)
        {
            return;
        }

        var stroke = BuildShapeStroke( );
        if (stroke is not null)
        {
            InnerCanvas.Strokes.Add(stroke);
        }
    }

    private void CancelShape( )
    {
        shapeActive = false;
        ShapePreviewLine.Visibility = Visibility.Collapsed;
        ShapePreviewEllipse.Visibility = Visibility.Collapsed;
    }

    private Stroke? BuildShapeStroke( )
    {
        var attrs = DefaultDrawingAttributes.Clone( );
        attrs.FitToCurve = false;
        attrs.IsHighlighter = false;

        if (IsCircle)
        {
            var cx = shapeStart.X;
            var cy = shapeStart.Y;
            var r = Distance(shapeStart, shapeEnd);

            if (r < CircleMinRadius)
            {
                return null;
            }

            var points = new StylusPointCollection(CircleSegments + 1);
            for (var i = 0; i <= CircleSegments; i++)
            {
                var a = 2 * Math.PI * i / CircleSegments;
                points.Add(new StylusPoint(cx + r * Math.Cos(a), cy + r * Math.Sin(a), DefaultPressure));
            }

            return new Stroke(points, attrs);
        }

        return new Stroke(
            [
                new StylusPoint(shapeStart.X, shapeStart.Y, DefaultPressure),
                new StylusPoint(shapeEnd.X, shapeEnd.Y, DefaultPressure)
            ],
            attrs);
    }
}
