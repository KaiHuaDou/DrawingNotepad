using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;

namespace InkCanvasNext;

internal sealed class SelectionVisual
{
    private const double HandleRadius = 5;

    private static readonly Color accentColor = Color.FromRgb(0x4C, 0x8B, 0xF5);

    private readonly SelectionVisualHost host;

    private Rect bounds = Rect.Empty;
    private IReadOnlyCollection<Stroke> selected = [];
    private IReadOnlyList<Point>? lasso;
    private bool drawHalo;

    public SelectionVisual(Canvas layer)
    {
        host = new SelectionVisualHost(Draw)
        {
            Width = 32768,
            Height = 16384
        };
        layer.Children.Add(host);
    }

    public void Invalidate(Rect bounds, IReadOnlyCollection<Stroke> selected, IReadOnlyList<Point>? lasso, bool halo)
    {
        this.bounds = bounds;
        this.selected = selected;
        this.lasso = lasso;
        drawHalo = halo;
        host.InvalidateVisual( );
    }

    private void Draw(DrawingContext dc)
    {
        // 1) 选中高亮：accent 半透明加粗描边 halo（替代 WPF hollow）。
        //    仅在选择确定（无进行中手势）时绘制：手势期间逐帧克隆选中笔画开销大，
        //    且拖动/缩放中笔画本身就在移动，选择框 + 手柄已足够指示。
        if (drawHalo)
        {
            var haloBrush = new SolidColorBrush(Color.FromArgb(120, accentColor.R, accentColor.G, accentColor.B));
            foreach (var s in selected)
            {
                var halo = s.Clone( );
                var attrs = halo.DrawingAttributes.Clone( );
                attrs.Color = haloBrush.Color;
                attrs.Width *= 2.4;
                attrs.Height *= 2.4;
                halo.DrawingAttributes = attrs;
                halo.Draw(dc);
            }
        }

        // 2) 选择框 + 手柄
        if (!bounds.IsEmpty)
        {
            var accentBrush = new SolidColorBrush(Color.FromArgb(235, accentColor.R, accentColor.G, accentColor.B));
            var pen = new Pen(accentBrush, 2.0);
            dc.DrawRectangle(null, pen, bounds);
            DrawHandles(dc, bounds, pen);
        }

        // 3) 套索轨迹（accent 虚线）
        if (lasso is { Count: >= 2 })
        {
            var lassoPen = new Pen(new SolidColorBrush(accentColor), 2.0)
            {
                DashStyle = new DashStyle([4, 3], 0)
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

    private static void DrawHandles(DrawingContext dc, Rect b, Pen pen)
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
    }

    private sealed class SelectionVisualHost(Action<DrawingContext> render) : FrameworkElement
    {
        protected override void OnRender(DrawingContext drawingContext)
        {
            render(drawingContext);
        }
    }
}
