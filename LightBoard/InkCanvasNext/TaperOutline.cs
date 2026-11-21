using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

using MediaGeometry = System.Windows.Media.Geometry;

namespace InkCanvasNext;

/// <summary>
/// 湿墨轮廓：按采样点压力与尾部比例生成左右包络，末端加圆帽
/// </summary>
internal static class TaperOutline
{
    private const double MinWidth = 0.1;

    /// <summary>
    /// 生成 [start, end) 区间的填充轮廓；bodyEnd 之后视为尾部，按 Taper.Ratio 收细
    /// </summary>
    internal static MediaGeometry? Build(
        List<StylusPoint> points,
        int start,
        int end,
        double width,
        TaperParameters? taper,
        int bodyEnd)
    {
        var count = end - start;
        if (count <= 0 || width <= 0 || end > points.Count)
        {
            return null;
        }

        var tail = Taper.Tail(points.Count, taper);

        if (count == 1)
        {
            var single = points[start];
            var radius = WidthAt(start, points, width, tail, bodyEnd) / 2;
            var circle = new EllipseGeometry(new Point(single.X, single.Y), radius, radius);
            circle.Freeze( );
            return circle;
        }

        var left = new Point[count];
        var right = new Point[count];
        var previousNormal = new Vector(0, 1);

        for (var i = 0; i < count; i++)
        {
            var index = start + i;
            var previous = index > start ? points[index - 1] : points[index];
            var next = index < end - 1 ? points[index + 1] : points[index];
            var dx = next.X - previous.X;
            var dy = next.Y - previous.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            var normal = length > 0.001
                ? new Vector(-dy / length, dx / length)
                : previousNormal;

            previousNormal = normal;

            var radius = WidthAt(index, points, width, tail, bodyEnd) / 2;
            var point = points[index];
            left[i] = new Point(point.X + normal.X * radius, point.Y + normal.Y * radius);
            right[i] = new Point(point.X - normal.X * radius, point.Y - normal.Y * radius);
        }

        var geometry = new StreamGeometry { FillRule = FillRule.Nonzero };
        using (var context = geometry.Open( ))
        {
            context.BeginFigure(left[0], true, true);
            for (var i = 1; i < count; i++)
            {
                context.LineTo(left[i], true, false);
            }

            for (var i = count - 1; i >= 0; i--)
            {
                context.LineTo(right[i], true, false);
            }
        }

        geometry.Freeze( );

        var group = new GeometryGroup { FillRule = FillRule.Nonzero };
        group.Children.Add(geometry);

        var firstPoint = points[start];
        var firstRadius = WidthAt(start, points, width, tail, bodyEnd) / 2;
        group.Children.Add(new EllipseGeometry(
            new Point(firstPoint.X, firstPoint.Y), firstRadius, firstRadius));

        var lastPoint = points[end - 1];
        var lastRadius = WidthAt(end - 1, points, width, tail, bodyEnd) / 2;
        group.Children.Add(new EllipseGeometry(
            new Point(lastPoint.X, lastPoint.Y), lastRadius, lastRadius));

        group.Freeze( );
        return group;
    }

    private static double WidthAt(int index, List<StylusPoint> points, double width, int tail, int bodyEnd)
    {
        var pressure = (double) points[index].PressureFactor;
        var factor = 1.5 * pressure + 0.25;
        var ratio = tail >= 2 && index >= bodyEnd
            ? Taper.Ratio(index - bodyEnd, tail)
            : 1.0;

        return Math.Max(MinWidth, width * factor * ratio);
    }
}
