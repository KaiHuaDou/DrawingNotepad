using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    private readonly Canvas shapePreviewLayer = new( ) { IsHitTestVisible = false };
    private readonly Line shapePreviewLine = new( ) { Visibility = Visibility.Collapsed };
    private readonly Ellipse shapePreviewEllipse = new( ) { Visibility = Visibility.Collapsed };

    private Point shapeStart;
    private Point shapeEnd;
    private bool shapeActive;

    private bool IsShapeMode => Mode is InkCanvasNextMode.Line or InkCanvasNextMode.Circle;
    private bool IsCircle => Mode == InkCanvasNextMode.Circle;

    private void SetupShapePreview( )
    {
        shapePreviewLine.Stroke = new SolidColorBrush(Colors.White);
        shapePreviewLine.StrokeThickness = 3;
        shapePreviewEllipse.Stroke = new SolidColorBrush(Colors.White);
        shapePreviewEllipse.StrokeThickness = 3;
        shapePreviewEllipse.Fill = Brushes.Transparent;
        shapePreviewLayer.Children.Add(shapePreviewLine);
        shapePreviewLayer.Children.Add(shapePreviewEllipse);
        Canvas.Children.Add(shapePreviewLayer);
    }

    private void StartShape(Point point)
    {
        shapeStart = point;
        shapeEnd = point;
        shapeActive = true;

        if (IsCircle)
        {
            System.Windows.Controls.Canvas.SetLeft(shapePreviewEllipse, point.X);
            System.Windows.Controls.Canvas.SetTop(shapePreviewEllipse, point.Y);
            shapePreviewEllipse.Width = 0;
            shapePreviewEllipse.Height = 0;
            shapePreviewEllipse.Visibility = Visibility.Visible;
            shapePreviewLine.Visibility = Visibility.Collapsed;
        }
        else
        {
            shapePreviewLine.X1 = point.X;
            shapePreviewLine.Y1 = point.Y;
            shapePreviewLine.X2 = point.X;
            shapePreviewLine.Y2 = point.Y;
            shapePreviewLine.Visibility = Visibility.Visible;
            shapePreviewEllipse.Visibility = Visibility.Collapsed;
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
        var width = Math.Max(attrs.Width, 1.0);

        if (IsCircle)
        {
            var r = Geometry.Distance(shapeStart, end);
            System.Windows.Controls.Canvas.SetLeft(shapePreviewEllipse, shapeStart.X - r);
            System.Windows.Controls.Canvas.SetTop(shapePreviewEllipse, shapeStart.Y - r);
            shapePreviewEllipse.Width = 2 * r;
            shapePreviewEllipse.Height = 2 * r;
            shapePreviewEllipse.Stroke = brush;
            shapePreviewEllipse.StrokeThickness = width;
        }
        else
        {
            shapePreviewLine.X1 = shapeStart.X;
            shapePreviewLine.Y1 = shapeStart.Y;
            shapePreviewLine.X2 = end.X;
            shapePreviewLine.Y2 = end.Y;
            shapePreviewLine.Stroke = brush;
            shapePreviewLine.StrokeThickness = width;
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
        shapePreviewLine.Visibility = Visibility.Collapsed;
        shapePreviewEllipse.Visibility = Visibility.Collapsed;

        if (Geometry.Distance(shapeStart, shapeEnd) < 4)
        {
            return;
        }

        var stroke = BuildShapeStroke( );
        if (stroke is not null)
        {
            Canvas.Strokes.Add(stroke);
        }
    }

    private void CancelShape( )
    {
        shapeActive = false;
        shapePreviewLine.Visibility = Visibility.Collapsed;
        shapePreviewEllipse.Visibility = Visibility.Collapsed;
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
            var r = Geometry.Distance(shapeStart, shapeEnd);

            if (r < 1)
            {
                return null;
            }

            const int Segments = 64;
            var points = new StylusPointCollection(Segments + 1);
            for (var i = 0; i <= Segments; i++)
            {
                var a = 2 * Math.PI * i / Segments;
                points.Add(new StylusPoint(cx + r * Math.Cos(a), cy + r * Math.Sin(a), 0.5f));
            }

            return new Stroke(points, attrs);
        }

        return new Stroke(
            [
                new StylusPoint(shapeStart.X, shapeStart.Y, 0.5f),
                new StylusPoint(shapeEnd.X, shapeEnd.Y, 0.5f)
            ],
            attrs);
    }
}
