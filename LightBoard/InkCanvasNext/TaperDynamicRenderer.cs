using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Input.StylusPlugIns;
using System.Windows.Media;
using System.Windows.Threading;

using MediaGeometry = System.Windows.Media.Geometry;

namespace InkCanvasNext;

/// <summary>
/// 自绘实时渲染器：与原生 DynamicRenderer 走同一条插件通道，按报文增量绘制湿墨
/// 报文视觉只画已离开尾部的笔身；末尾 Taper.Points 点放进可替换的尾部视觉，保证末端渐细
/// </summary>
internal sealed class TaperDynamicRenderer : DynamicRenderer
{
    // 两个报文之间超过此距离视为换笔，避免把两次落笔连成一条线
    private const double JoinResetDistance = 128;

    private readonly TaperParameters taper;
    private readonly Lock sync = new( );
    private readonly List<StylusPoint> points = [];

    private sealed record TailUpdate(MediaGeometry? Geometry, Brush? Brush, int Generation);

    private DrawingAttributes? attributes;
    private ContainerVisual? root;
    private ContainerVisual? tailRoot;
    private DrawingVisual? tailVisual;
    private Dispatcher? applicationDispatcher;

    private TailUpdate? pendingTail;
    private int tailQueued;

    private int bodyDrawn;
    private int generation;
    private int resetRequested;

    internal TaperDynamicRenderer(TaperParameters taper)
    {
        this.taper = taper;
        Volatile.Write(ref attributes, Snapshot(DrawingAttributes));
    }

    /// <summary>
    /// 尾部视觉是否已挂到 DynamicRenderer.RootVisual；用于集成测试
    /// </summary>
    internal bool HasTailLayer => Volatile.Read(ref tailVisual) is not null;

    protected override void OnEnabledChanged( )
    {
        base.OnEnabledChanged( );

        if (Enabled)
        {
            EnsureTailLayer( );
            return;
        }

        // 手势接管、切工具或失活时编辑模式会把渲染器关掉，湿墨尾部必须同步清掉
        Interlocked.Increment(ref generation);
        Interlocked.Exchange(ref pendingTail, null);

        lock (sync)
        {
            points.Clear( );
            bodyDrawn = 0;
        }

        DrawTail(null, null);
        RemoveTailLayer( );
    }

    protected override void OnAdded( )
    {
        applicationDispatcher = Element?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        base.OnAdded( );
        EnsureTailLayer( );
    }

    protected override void OnRemoved( )
    {
        base.OnRemoved( );
        Interlocked.Increment(ref generation);

        lock (sync)
        {
            points.Clear( );
            bodyDrawn = 0;
        }

        RemoveTailLayer( );
    }

    protected override void OnIsActiveForInputChanged( )
    {
        base.OnIsActiveForInputChanged( );

        if (IsActiveForInput)
        {
            EnsureTailLayer( );
        }
    }

    protected override void OnDrawingAttributesReplaced( )
    {
        base.OnDrawingAttributesReplaced( );
        Volatile.Write(ref attributes, Snapshot(DrawingAttributes));

        if (!Supports(Volatile.Read(ref attributes)))
        {
            PostTail(null, null, Volatile.Read(ref generation));
        }
    }

    protected override void OnStylusDown(RawStylusInput rawStylusInput)
    {
        Interlocked.Increment(ref generation);
        Interlocked.Exchange(ref resetRequested, 1);
        EnsureTailLayer( );
        base.OnStylusDown(rawStylusInput);
    }

    protected override void OnStylusUp(RawStylusInput rawStylusInput)
    {
        base.OnStylusUp(rawStylusInput);

        // 干墨由 InkCanvas 在本轮输入里提交，尾部视觉延后到后台优先级再清，避免闪空
        var current = Volatile.Read(ref generation);
        if (applicationDispatcher is null)
        {
            PostTail(null, null, current);
            return;
        }

        applicationDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(( ) =>
        {
            if (Volatile.Read(ref generation) == current)
            {
                PostTail(null, null, current);
            }
        }));
    }

    public override void Reset(StylusDevice stylusDevice, StylusPointCollection stylusPoints)
    {
        Interlocked.Increment(ref generation);
        Interlocked.Exchange(ref resetRequested, 1);
        base.Reset(stylusDevice, stylusPoints);
    }

    protected override void OnDraw(
        DrawingContext drawingContext,
        StylusPointCollection stylusPoints,
        MediaGeometry geometry,
        Brush fillBrush)
    {
        var currentAttributes = Volatile.Read(ref attributes);

        if (stylusPoints.Count == 0)
        {
            return;
        }

        if (currentAttributes is not { } current
            || !Supports(current)
            || fillBrush is null)
        {
            PostTail(null, null, Volatile.Read(ref generation));
            base.OnDraw(drawingContext, stylusPoints, geometry, fillBrush);
            return;
        }

        if (Interlocked.Exchange(ref resetRequested, 0) == 1)
        {
            ResetPoints( );
        }

        var currentGeneration = Volatile.Read(ref generation);

        lock (sync)
        {
            var first = stylusPoints[0];
            if (points.Count > 0
                && Distance2(points[^1], first) > JoinResetDistance * JoinResetDistance)
            {
                ResetPoints( );
            }

            foreach (var point in stylusPoints)
            {
                if (points.Count == 0 || Distance2(points[^1], point) > 0.0001)
                {
                    points.Add(point);
                }
            }

            var total = points.Count;
            var tail = Taper.Tail(total, taper);
            var bodyEnd = total - tail;

            // 笔身：只画本次离开尾部的点，报文视觉由原生 DynamicRenderer 追加
            if (bodyEnd > bodyDrawn)
            {
                var start = Math.Max(0, bodyDrawn - 1);
                var bodyGeometry = TaperOutline.Build(
                    points, start, bodyEnd, current.Width, taper, bodyEnd);

                if (bodyGeometry is not null)
                {
                    drawingContext.DrawGeometry(fillBrush, null, bodyGeometry);
                }

                bodyDrawn = bodyEnd;
            }

            if (tail <= 0)
            {
                PostTail(null, null, currentGeneration);
                return;
            }

            // 尾部：每个报文重建，替换上一帧的尾部视觉，末端按 Taper 收细
            var tailStart = Math.Max(0, bodyEnd - 1);
            var tailGeometry = TaperOutline.Build(
                points, tailStart, total, current.Width, taper, bodyEnd);

            if (Volatile.Read(ref tailVisual) is null)
            {
                // 尾部视觉尚未挂上（例如 Unloaded 后首个报文）：先画进报文视觉保证有墨
                if (tailGeometry is not null)
                {
                    drawingContext.DrawGeometry(fillBrush, null, tailGeometry);
                }

                return;
            }

            PostTail(tailGeometry, fillBrush, currentGeneration);
        }
    }

    private void ResetPoints( )
    {
        points.Clear( );
        bodyDrawn = 0;
        PostTail(null, null, Volatile.Read(ref generation));
    }

    private static bool Supports(DrawingAttributes? value)
    {
        // 半透明笔画在笔身/尾部的重叠处会叠色，交给原生渲染器
        return value is
        {
            IgnorePressure: false,
            IsHighlighter: false,
            StylusTip: StylusTip.Ellipse,
            Color.A: byte.MaxValue
        };
    }

    private static DrawingAttributes? Snapshot(DrawingAttributes? value)
    {
        if (value is null)
        {
            return null;
        }

        return value.Clone( );
    }

    private static double Distance2(StylusPoint a, StylusPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    private void EnsureTailLayer( )
    {
        if (tailVisual is not null)
        {
            return;
        }

        if (applicationDispatcher is null)
        {
            return;
        }

        if (!applicationDispatcher.CheckAccess( ))
        {
            applicationDispatcher.BeginInvoke(DispatcherPriority.Send, new Action(EnsureTailLayer));
            return;
        }

        if (RootVisual is not ContainerVisual container)
        {
            return;
        }

        root = container;
        tailRoot = new ContainerVisual( );
        tailVisual = new DrawingVisual( );
        tailRoot.Children.Add(tailVisual);
        container.Children.Add(tailRoot);
    }

    private void RemoveTailLayer( )
    {
        if (applicationDispatcher?.CheckAccess( ) == false)
        {
            applicationDispatcher.BeginInvoke(DispatcherPriority.Send, new Action(RemoveTailLayer));
            return;
        }

        if (root is not null && tailRoot is not null)
        {
            root.Children.Remove(tailRoot);
        }

        root = null;
        tailRoot = null;
        tailVisual = null;
    }

    private void PostTail(MediaGeometry? geometry, Brush? brush, int expectedGeneration)
    {
        var visual = Volatile.Read(ref tailVisual);
        if (visual is null)
        {
            return;
        }

        if (visual.Dispatcher.CheckAccess( ))
        {
            if (Volatile.Read(ref generation) == expectedGeneration)
            {
                DrawTail(geometry, brush);
            }

            return;
        }

        // 渲染线程只保留最新一份尾部几何；UI 线程忙时不再堆积逐报文更新
        Volatile.Write(ref pendingTail, new TailUpdate(geometry, brush, expectedGeneration));
        if (Interlocked.Exchange(ref tailQueued, 1) != 0)
        {
            return;
        }

        visual.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(FlushTail));
    }

    private void FlushTail( )
    {
        Interlocked.Exchange(ref tailQueued, 0);

        var update = Interlocked.Exchange(ref pendingTail, null);
        if (update is null || Volatile.Read(ref generation) != update.Generation)
        {
            return;
        }

        DrawTail(update.Geometry, update.Brush);
    }

    /// <summary>
    /// 干墨提交后同步清尾：OnStrokeCollected 调用；不依赖 Background 优先级
    /// </summary>
    internal void ClearTailAfterStroke( )
    {
        // 让排队中的尾部更新失效
        Interlocked.Increment(ref generation);
        Interlocked.Exchange(ref pendingTail, null);

        var visual = Volatile.Read(ref tailVisual);
        if (visual is null)
        {
            return;
        }

        if (visual.Dispatcher.CheckAccess( ))
        {
            DrawTail(null, null);
            return;
        }

        visual.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(( ) => DrawTail(null, null)));
    }

    private void DrawTail(MediaGeometry? geometry, Brush? brush)
    {
        if (tailVisual is null)
        {
            return;
        }

        // 原生 DynamicRenderer 把颜色 alpha 放进容器 Opacity，尾部视觉与它保持一致
        tailVisual.Opacity = (attributes?.Color.A ?? byte.MaxValue) / 255.0;

        using var context = tailVisual.RenderOpen( );
        if (geometry is not null && brush is not null)
        {
            context.DrawGeometry(brush, null, geometry);
        }
    }
}

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
        TaperParameters taper,
        int bodyEnd)
    {
        var count = end - start;
        if (count <= 0 || width <= 0 || end > points.Count)
        {
            return null;
        }

        var tail = Taper.Tail(points.Count, taper);

        double WidthAt(int index)
        {
            var pressure = (double) points[index].PressureFactor;
            var factor = 1.5 * pressure + 0.25;
            var ratio = tail >= 2 && index >= bodyEnd
                ? Taper.Ratio(index - bodyEnd, tail)
                : 1.0;

            return Math.Max(MinWidth, width * factor * ratio);
        }

        if (count == 1)
        {
            var single = points[start];
            var radius = WidthAt(start) / 2;
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

            var radius = WidthAt(index) / 2;
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
        var firstRadius = WidthAt(start) / 2;
        group.Children.Add(new EllipseGeometry(
            new Point(firstPoint.X, firstPoint.Y), firstRadius, firstRadius));

        var lastPoint = points[end - 1];
        var lastRadius = WidthAt(end - 1) / 2;
        group.Children.Add(new EllipseGeometry(
            new Point(lastPoint.X, lastPoint.Y), lastRadius, lastRadius));

        group.Freeze( );
        return group;
    }
}
