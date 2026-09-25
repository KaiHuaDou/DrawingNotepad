using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;

namespace InkCanvasNext;

internal sealed partial class SelectionVisual
{
    private readonly SelectionVisualHost host;

    private Rect bounds = Rect.Empty;
    private IReadOnlyCollection<Stroke> selected = [];
    private IReadOnlyList<Point>? lasso;
    private bool drawHalo;
    private double zoom = 1.0;
    private Point? rotateHandlePosition;

    internal SelectionVisual(Canvas layer, Size canvasSize)
    {
        // 覆盖层必须盖住整个墨迹画布，否则画布边缘的选区会被裁掉。
        host = new SelectionVisualHost(Draw)
        {
            Width = canvasSize.Width,
            Height = canvasSize.Height
        };
        layer.Children.Add(host);
    }

    /// <summary>
    /// 画布扩展后同步覆盖层尺寸，维持覆盖整个墨迹画布。
    /// </summary>
    internal void Resize(Size canvasSize)
    {
        host.Width = canvasSize.Width;
        host.Height = canvasSize.Height;
    }

    internal void Invalidate(Rect bounds, IReadOnlyCollection<Stroke> selected, IReadOnlyList<Point>? lasso, bool halo, double zoom, Point? rotateHandlePosition)
    {
        this.bounds = bounds;
        this.selected = selected;
        this.lasso = lasso;
        drawHalo = halo;
        this.zoom = Math.Max(zoom, 1e-6);
        this.rotateHandlePosition = rotateHandlePosition;
        host.InvalidateVisual( );
    }

    /// <summary>旋转手柄中心（内容坐标）：浮于选区正上方（顶部居中），拖拽绕选区中心旋转。</summary>
    internal static Point RotateHandleCenter(Rect b, double zoom)
    {
        var z = Math.Max(zoom, 1e-6);
        return new Point(
            b.Left + b.Width / 2,
            b.Top - (RotateGapAboveSelection + RotateScreenRadius) / z);
    }

    private void Draw(DrawingContext dc)
    {
        if (drawHalo)
        {
            var haloBrush = new SolidColorBrush(Color.FromArgb(HaloAlpha, AccentColor.R, AccentColor.G, AccentColor.B));
            foreach (var s in selected)
            {
                var halo = s.Clone( );
                var attrs = halo.DrawingAttributes.Clone( );
                attrs.Color = haloBrush.Color;
                attrs.Width *= HaloWidthFactor;
                attrs.Height *= HaloWidthFactor;
                halo.DrawingAttributes = attrs;
                halo.Draw(dc);
            }
        }

        if (!bounds.IsEmpty)
        {
            var accentBrush = new SolidColorBrush(Color.FromArgb(BorderAlpha, AccentColor.R, AccentColor.G, AccentColor.B));
            var pen = new Pen(accentBrush, BorderWidth);
            dc.DrawRectangle(null, pen, bounds);
            DrawHandles(dc, bounds, pen, zoom, rotateHandlePosition);
        }

        if (lasso is { Count: >= 2 })
        {
            var lassoPen = new Pen(new SolidColorBrush(AccentColor), LassoWidth)
            {
                DashStyle = new DashStyle(LassoDashPattern, 0)
            };
            var figure = new PathFigure { StartPoint = lasso[0], IsClosed = true, IsFilled = false };
            for (var i = 1; i < lasso.Count; i++)
            {
                figure.Segments.Add(new LineSegment(lasso[i], true));
            }

            var geometry = new PathGeometry( );
            geometry.Figures.Add(figure);
            dc.DrawGeometry(null, lassoPen, geometry);
        }
    }

    private static void DrawHandles(DrawingContext dc, Rect b, Pen pen, double zoom, Point? rotateHandlePosition)
    {
        var white = Brushes.White;
        var pts = new[]
        {
            b.TopLeft, b.TopRight, b.BottomLeft, b.BottomRight,
            new Point(b.Left + b.Width / 2, b.Top),
            new Point(b.Left + b.Width / 2, b.Bottom),
            new Point(b.Left, b.Top + b.Height / 2),
            new Point(b.Right, b.Top + b.Height / 2)
        };

        foreach (var p in pts)
        {
            dc.DrawRectangle(white, pen, new Rect(p.X - HandleRadius, p.Y - HandleRadius, HandleRadius * 2, HandleRadius * 2));
        }

        // 旋转手柄：默认浮于选区正上方；旋转手势中沿鼠标角度绕选区中心等半径跟随（rotateHandlePosition）
        var rotateCenter = rotateHandlePosition ?? RotateHandleCenter(b, zoom);
        var rr = RotateScreenRadius / zoom;
        dc.DrawEllipse(Brushes.White, pen, rotateCenter, rr, rr);
    }
}

internal sealed class SelectionVisualHost(Action<DrawingContext> render) : FrameworkElement
{
    protected override void OnRender(DrawingContext drawingContext)
    {
        render(drawingContext);
    }
}
