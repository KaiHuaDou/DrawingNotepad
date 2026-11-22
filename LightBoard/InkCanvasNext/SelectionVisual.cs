using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;

namespace InkCanvasNext;

/// <summary>
/// 选区覆盖层：边框、手柄、套索与 halo 各占独立 visual
/// 单独成 visual 是为了让每帧的脏区落在细窄的边条与小方块上，而不是整块选区
/// </summary>
internal sealed partial class SelectionVisual
{
    private readonly VisualCanvas host;
    private readonly DrawingVisual haloVisual = new( );
    private readonly DrawingVisual topVisual = new( );
    private readonly DrawingVisual bottomVisual = new( );
    private readonly DrawingVisual leftVisual = new( );
    private readonly DrawingVisual rightVisual = new( );
    private readonly DrawingVisual rotateVisual = new( );
    private readonly DrawingVisual lassoVisual = new( );

    private Rect bounds = Rect.Empty;
    private IReadOnlyCollection<Stroke> selected = [];
    private IReadOnlyList<Point>? lasso;
    private bool drawHalo;
    private double zoom = 1.0;
    private Point? rotateHandlePosition;
    private Pen? borderPen;
    private Pen? lassoPen;
    private double penZoom = double.NaN;
    private bool haloDrawn;
    private bool frameDrawn;
    private bool lassoDrawn;

    internal SelectionVisual(Canvas layer, Size canvasSize)
    {
        // 覆盖层与画布同尺寸；Canvas 不裁剪子元素，尺寸只为与画布扩展保持一致
        host = new VisualCanvas
        {
            Width = canvasSize.Width,
            Height = canvasSize.Height
        };

        host.AddVisual(haloVisual);
        host.AddVisual(topVisual);
        host.AddVisual(bottomVisual);
        host.AddVisual(leftVisual);
        host.AddVisual(rightVisual);
        host.AddVisual(rotateVisual);
        host.AddVisual(lassoVisual);

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

    internal void Render(Rect bounds, IReadOnlyCollection<Stroke> selected, IReadOnlyList<Point>? lasso, bool halo, double zoom, Point? rotateHandlePosition)
    {
        this.bounds = bounds;
        this.selected = selected;
        this.lasso = lasso;
        drawHalo = halo;
        this.zoom = Math.Max(zoom, 1e-6);
        this.rotateHandlePosition = rotateHandlePosition;

        Redraw( );
    }

    /// <summary>只清空套索图层，边框与 halo 保持不变</summary>
    internal void ClearLasso( )
    {
        if (!lassoDrawn)
        {
            return;
        }

        using var dc = lassoVisual.RenderOpen( );
        lassoDrawn = false;
    }

    /// <summary>旋转手柄中心（内容坐标）：浮于选区正上方（顶部居中），拖拽绕选区中心旋转</summary>
    internal static Point RotateHandleCenter(Rect b, double zoom)
    {
        var z = Math.Max(zoom, 1e-6);
        return new Point(
            b.Left + b.Width / 2,
            b.Top - (RotateGapAboveSelection + RotateScreenRadius) / z);
    }

    private void Redraw( )
    {
        DrawHalo( );
        DrawFrame( );
        DrawLasso( );
    }

    private void DrawHalo( )
    {
        if (!drawHalo)
        {
            if (haloDrawn)
            {
                using var empty = haloVisual.RenderOpen( );
                haloDrawn = false;
            }

            return;
        }

        using var dc = haloVisual.RenderOpen( );
        foreach (var s in selected)
        {
            // 只需要按加宽/改色后的属性重算轮廓，不必深拷贝笔画点集；
            // GetGeometry(attrs) 使用传入属性计算，不改写原笔画的几何缓存
            var attrs = s.DrawingAttributes.Clone( );
            attrs.Color = HaloBrush.Color;
            attrs.Width *= HaloWidthFactor;
            attrs.Height *= HaloWidthFactor;

            var geometry = s.GetGeometry(attrs);
            if (attrs.IsHighlighter)
            {
                // 与 Stroke.Draw 对高亮笔的处理保持一致：整体再乘 0.5 不透明度
                dc.PushOpacity(0.5);
                try
                {
                    dc.DrawGeometry(HaloBrush, null, geometry);
                }
                finally
                {
                    dc.Pop( );
                }
            }
            else
            {
                dc.DrawGeometry(HaloBrush, null, geometry);
            }
        }

        haloDrawn = true;
    }

    private void DrawFrame( )
    {
        if (bounds.IsEmpty)
        {
            if (frameDrawn)
            {
                ClearFrame( );
                frameDrawn = false;
            }

            return;
        }

        EnsurePens( );

        var radius = HandleRadius / zoom;
        var centerX = bounds.Left + bounds.Width / 2;
        var centerY = bounds.Top + bounds.Height / 2;

        using (var dc = topVisual.RenderOpen( ))
        {
            dc.DrawLine(borderPen!, new Point(bounds.Left, bounds.Top), new Point(bounds.Right, bounds.Top));
            DrawHandle(dc, new Point(bounds.Left, bounds.Top), radius, borderPen!);
            DrawHandle(dc, new Point(bounds.Right, bounds.Top), radius, borderPen!);
            DrawHandle(dc, new Point(centerX, bounds.Top), radius, borderPen!);
        }

        using (var dc = bottomVisual.RenderOpen( ))
        {
            dc.DrawLine(borderPen!, new Point(bounds.Left, bounds.Bottom), new Point(bounds.Right, bounds.Bottom));
            DrawHandle(dc, new Point(bounds.Left, bounds.Bottom), radius, borderPen!);
            DrawHandle(dc, new Point(bounds.Right, bounds.Bottom), radius, borderPen!);
            DrawHandle(dc, new Point(centerX, bounds.Bottom), radius, borderPen!);
        }

        using (var dc = leftVisual.RenderOpen( ))
        {
            dc.DrawLine(borderPen!, new Point(bounds.Left, bounds.Top), new Point(bounds.Left, bounds.Bottom));
            DrawHandle(dc, new Point(bounds.Left, centerY), radius, borderPen!);
        }

        using (var dc = rightVisual.RenderOpen( ))
        {
            dc.DrawLine(borderPen!, new Point(bounds.Right, bounds.Top), new Point(bounds.Right, bounds.Bottom));
            DrawHandle(dc, new Point(bounds.Right, centerY), radius, borderPen!);
        }

        // 旋转手柄单占一个 visual：手势中它绕选区中心移动，与边框同层会把脏区拉成整块
        using (var dc = rotateVisual.RenderOpen( ))
        {
            var rotateCenter = rotateHandlePosition ?? RotateHandleCenter(bounds, zoom);
            var rr = RotateScreenRadius / zoom;
            dc.DrawEllipse(Brushes.White, borderPen!, rotateCenter, rr, rr);
        }

        frameDrawn = true;
    }

    private void ClearFrame( )
    {
        using var top = topVisual.RenderOpen( );
        using var bottom = bottomVisual.RenderOpen( );
        using var left = leftVisual.RenderOpen( );
        using var right = rightVisual.RenderOpen( );
        using var rotate = rotateVisual.RenderOpen( );
    }

    private void DrawLasso( )
    {
        if (lasso is not { Count: >= 2 })
        {
            if (lassoDrawn)
            {
                using var empty = lassoVisual.RenderOpen( );
                lassoDrawn = false;
            }

            return;
        }

        EnsurePens( );

        var figure = new PathFigure { StartPoint = lasso[0], IsClosed = true, IsFilled = false };
        figure.Segments.Add(new PolyLineSegment(lasso, true));

        var geometry = new PathGeometry( );
        geometry.Figures.Add(figure);

        using var dc = lassoVisual.RenderOpen( );
        dc.DrawGeometry(null, lassoPen!, geometry);
        lassoDrawn = true;
    }

    private void EnsurePens( )
    {
        if (borderPen is not null && penZoom == zoom)
        {
            return;
        }

        // 方头线帽让四条边在角上互相盖住，避免高缩放下出现缝隙
        borderPen = new Pen(BorderBrush, BorderWidth / zoom)
        {
            StartLineCap = PenLineCap.Square,
            EndLineCap = PenLineCap.Square
        };

        borderPen.Freeze( );

        lassoPen = new Pen(LassoBrush, LassoWidth / zoom)
        {
            DashStyle = new DashStyle([.. LassoDashPattern.Select(d => d / zoom)], 0)
        };
        lassoPen.Freeze( );

        penZoom = zoom;
    }

    private static void DrawHandle(DrawingContext dc, Point point, double radius, Pen pen)
    {
        dc.DrawRectangle(
            Brushes.White,
            pen,
            new Rect(point.X - radius, point.Y - radius, radius * 2, radius * 2));
    }
}
