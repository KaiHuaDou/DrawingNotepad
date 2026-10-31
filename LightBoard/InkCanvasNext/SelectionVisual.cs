using System;
using System.Collections.Generic;
using System.Linq;
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
    private Pen? borderPen;
    private Pen? lassoPen;
    private double penZoom = double.NaN;

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
    /// 画布扩展后同步覆盖层尺寸，维持覆盖整个墨迹画布
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
            foreach (var s in selected)
            {
                var halo = s.Clone( );
                var attrs = halo.DrawingAttributes.Clone( );
                attrs.Color = HaloBrush.Color;
                attrs.Width *= HaloWidthFactor;
                attrs.Height *= HaloWidthFactor;
                halo.DrawingAttributes = attrs;
                halo.Draw(dc);
            }
        }

        // 覆盖层位于内容坐标系：视口 DIP 的线宽、手柄与虚线尺寸一律除以缩放，保持屏幕绝对大小
        EnsurePens( );
        if (!bounds.IsEmpty)
        {
            dc.DrawRectangle(null, borderPen!, bounds);
            DrawHandles(dc, bounds, borderPen!, zoom, rotateHandlePosition);
        }

        if (lasso is { Count: >= 2 })
        {
            var figure = new PathFigure { StartPoint = lasso[0], IsClosed = true, IsFilled = false };
            figure.Segments.Add(new PolyLineSegment(lasso, true));

            var geometry = new PathGeometry( );
            geometry.Figures.Add(figure);
            dc.DrawGeometry(null, lassoPen!, geometry);
        }
    }

    private void EnsurePens( )
    {
        if (borderPen is not null && penZoom == zoom)
        {
            return;
        }

        borderPen = new Pen(BorderBrush, BorderWidth / zoom);
        borderPen.Freeze( );

        lassoPen = new Pen(LassoBrush, LassoWidth / zoom)
        {
            DashStyle = new DashStyle([.. LassoDashPattern.Select(d => d / zoom)], 0)
        };
        lassoPen.Freeze( );

        penZoom = zoom;
    }

    private static void DrawHandles(DrawingContext dc, Rect b, Pen pen, double zoom, Point? rotateHandlePosition)
    {
        var handleRadius = HandleRadius / zoom;

        DrawHandle(dc, b.TopLeft, handleRadius, pen);
        DrawHandle(dc, b.TopRight, handleRadius, pen);
        DrawHandle(dc, b.BottomLeft, handleRadius, pen);
        DrawHandle(dc, b.BottomRight, handleRadius, pen);
        DrawHandle(dc, new Point(b.Left + b.Width / 2, b.Top), handleRadius, pen);
        DrawHandle(dc, new Point(b.Left + b.Width / 2, b.Bottom), handleRadius, pen);
        DrawHandle(dc, new Point(b.Left, b.Top + b.Height / 2), handleRadius, pen);
        DrawHandle(dc, new Point(b.Right, b.Top + b.Height / 2), handleRadius, pen);

        // 旋转手柄：默认浮于选区正上方；旋转手势中沿鼠标角度绕选区中心等半径跟随（rotateHandlePosition）
        var rotateCenter = rotateHandlePosition ?? RotateHandleCenter(b, zoom);
        var rr = RotateScreenRadius / zoom;
        dc.DrawEllipse(Brushes.White, pen, rotateCenter, rr, rr);
    }

    private static void DrawHandle(DrawingContext dc, Point point, double radius, Pen pen)
    {
        dc.DrawRectangle(
            Brushes.White,
            pen,
            new Rect(point.X - radius, point.Y - radius, radius * 2, radius * 2));
    }
}

internal sealed class SelectionVisualHost(Action<DrawingContext> render) : FrameworkElement
{
    protected override void OnRender(DrawingContext drawingContext)
    {
        render(drawingContext);
    }
}
