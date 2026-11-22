using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace LightBoard;

internal readonly record struct SvgPathData(string Data, string? Transform);

/// <summary>
/// 笔画轮廓转 SVG 路径
/// </summary>
internal static class SvgPath
{
    /// <summary>
    /// 取得路径数据与几何自身的变换
    /// </summary>
    public static SvgPathData From(Geometry geometry)
    {
        var path = PathGeometry.CreateFromGeometry(geometry);
        var builder = new StringBuilder( );
        foreach (var figure in path.Figures)
        {
            if (!figure.IsFilled)
            {
                continue;
            }

            AppendFigure(builder, figure);
        }

        return new(builder.ToString( ), MatrixText(path.Transform));
    }

    private static void AppendFigure(StringBuilder builder, PathFigure figure)
    {
        Append(builder, 'M', [figure.StartPoint]);
        foreach (var segment in figure.Segments)
        {
            AppendSegment(builder, segment);
        }

        if (figure.IsClosed)
        {
            builder.Append('Z');
        }
    }

    private static void AppendSegment(StringBuilder builder, PathSegment segment)
    {
        switch (segment)
        {
            case LineSegment line:
                Append(builder, 'L', [line.Point]);
                break;
            case PolyLineSegment polyLine:
                Append(builder, 'L', polyLine.Points);
                break;
            case BezierSegment bezier:
                Append(builder, 'C', [bezier.Point1, bezier.Point2, bezier.Point3]);
                break;
            case PolyBezierSegment polyBezier:
                Append(builder, 'C', polyBezier.Points);
                break;
            case QuadraticBezierSegment quadratic:
                Append(builder, 'Q', [quadratic.Point1, quadratic.Point2]);
                break;
            case PolyQuadraticBezierSegment polyQuadratic:
                Append(builder, 'Q', polyQuadratic.Points);
                break;
            case ArcSegment arc:
                AppendArc(builder, arc);
                break;
            default:
                throw new NotSupportedException($"不支持的路径图元：{segment.GetType( ).Name}");
        }
    }

    private static void Append(StringBuilder builder, char command, IList<Point> points)
    {
        builder.Append(command);
        foreach (var point in points)
        {
            builder.Append(' ');
            AppendPoint(builder, point);
        }
    }

    private static void AppendArc(StringBuilder builder, ArcSegment arc)
    {
        builder.Append('A')
            .Append(SvgFormat.Number(arc.Size.Width)).Append(' ')
            .Append(SvgFormat.Number(arc.Size.Height)).Append(' ')
            .Append(SvgFormat.Number(arc.RotationAngle)).Append(' ')
            .Append(arc.IsLargeArc ? '1' : '0').Append(' ')
            .Append(arc.SweepDirection == SweepDirection.Clockwise ? '1' : '0').Append(' ');
        AppendPoint(builder, arc.Point);
    }

    private static void AppendPoint(StringBuilder builder, Point point)
    {
        builder.Append(SvgFormat.Number(point.X)).Append(' ').Append(SvgFormat.Number(point.Y));
    }

    private static string? MatrixText(Transform? transform)
    {
        if (transform?.Value.IsIdentity != false)
        {
            return null;
        }

        var matrix = transform.Value;
        return string.Create(CultureInfo.InvariantCulture,
            $"matrix({SvgFormat.Number(matrix.M11)} {SvgFormat.Number(matrix.M12)} {SvgFormat.Number(matrix.M21)} {SvgFormat.Number(matrix.M22)} {SvgFormat.Number(matrix.OffsetX)} {SvgFormat.Number(matrix.OffsetY)})");
    }
}
