using System;
using System.Collections.Generic;
using System.Threading;
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
/// 段几何按原始采样点成形，整笔拟合只在收笔后由干墨执行
/// </summary>
internal sealed partial class TaperDynamicRenderer : DynamicRenderer
{
    // 两个报文之间超过此距离视为换笔，避免把两次落笔连成一条线
    private const double JoinResetDistance = 128;

    private readonly TaperParameters taper;
    private readonly InputArbiter arbiter;
    private readonly Lock sync = new( );
    private readonly List<StylusPoint> points = [];

    private DrawingAttributes? attributes;
    private DrawingAttributes? noFitAttributes;
    private Stroke? segment;
    private Dispatcher? applicationDispatcher;

    private int bodyDrawn;
    private int generation;
    private int resetRequested;
    private volatile bool penStroke;

    internal TaperDynamicRenderer(TaperParameters taper, InputArbiter arbiter)
    {
        this.taper = taper;
        this.arbiter = arbiter;
        Volatile.Write(ref attributes, Snapshot(DrawingAttributes));
        Volatile.Write(ref noFitAttributes, NoFitSnapshot(DrawingAttributes));
    }

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
            segment = null;
        }

        penStroke = false;
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
            segment = null;
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
        Volatile.Write(ref noFitAttributes, NoFitSnapshot(DrawingAttributes));

        if (!Supports(Volatile.Read(ref attributes)))
        {
            PostTail(null, null, Volatile.Read(ref generation));
        }
    }

    protected override void OnStylusDown(RawStylusInput rawStylusInput)
    {
        var kind = arbiter.KindOf(rawStylusInput);

        if (!arbiter.Allows(kind))
        {
            return;
        }

        penStroke = kind == InputKind.Stylus;
        Interlocked.Increment(ref generation);
        Interlocked.Exchange(ref resetRequested, 1);
        EnsureTailLayer( );
        base.OnStylusDown(rawStylusInput);
    }

    protected override void OnStylusMove(RawStylusInput rawStylusInput)
    {
        if (!arbiter.Allows(arbiter.KindOf(rawStylusInput)))
        {
            return;
        }

        base.OnStylusMove(rawStylusInput);
    }

    protected override void OnStylusUp(RawStylusInput rawStylusInput)
    {
        var allowed = arbiter.Allows(arbiter.KindOf(rawStylusInput));
        base.OnStylusUp(rawStylusInput);

        if (!allowed)
        {
            return;
        }

        penStroke = false;

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
        if (!arbiter.Allows(InputArbiter.KindOf(stylusDevice)))
        {
            return;
        }

        penStroke = false;
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
        var currentNoFit = Volatile.Read(ref noFitAttributes);

        if (stylusPoints.Count == 0)
        {
            return;
        }

        if (currentAttributes is not { } current
            || currentNoFit is not { } noFit
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
            var effectiveTaper = penStroke ? null : taper;
            var tail = Taper.Tail(total, effectiveTaper);
            var bodyEnd = total - tail;

            // 笔身：只画本次离开尾部的点，报文视觉由原生 DynamicRenderer 追加
            if (bodyEnd > bodyDrawn)
            {
                var start = Math.Max(0, bodyDrawn - 1);
                var bodyGeometry = BuildGeometry(points, start, bodyEnd, noFit, effectiveTaper);

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
            var tailGeometry = BuildGeometry(points, tailStart, total, noFit, effectiveTaper);

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
        segment = null;
        PostTail(null, null, Volatile.Read(ref generation));
    }

    private MediaGeometry? BuildGeometry(
        IReadOnlyList<StylusPoint> points,
        int start,
        int end,
        DrawingAttributes attributes,
        TaperParameters? taper)
    {
        if (end <= start)
        {
            return null;
        }

        var copy = Taper.CopyRange(points, start, end, taper);
        if (segment is null)
        {
            segment = new Stroke(copy, attributes);
        }
        else
        {
            segment.StylusPoints = copy;
        }

        return segment.GetGeometry(attributes);
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
        return value?.Clone( );
    }

    // 湿墨段按原始采样点成形，整笔拟合留给收笔后的干墨
    private static DrawingAttributes? NoFitSnapshot(DrawingAttributes? value)
    {
        if (value is null)
        {
            return null;
        }

        var copy = value.Clone( );
        copy.FitToCurve = false;
        return copy;
    }

    private static double Distance2(StylusPoint a, StylusPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }
}
