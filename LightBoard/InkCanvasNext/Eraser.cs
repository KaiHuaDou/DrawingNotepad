using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    private readonly Eraser eraser;

    internal bool EraserActive => eraser.Active;

    /// <summary>
    /// 橡皮反馈圆上显示的文本；null 或空串时不显示
    /// </summary>
    public string? EraserLabel
    {
        get;
        set
        {
            field = value;
            eraser.Label = value;
        }
    }

    private void EndEraserCycle( )
    {
        var active = eraser.Active;

        SealEraseBatch( );
        eraser.End( );
        EndAreaEraseCycle( );

        if (active)
        {
            RaiseOperationCompleted(InkCanvasOperation.Erase);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsAreaEraserActive(TouchState state)
    {
        var traits = TraitsOf(state);
        return traits.PalmErase || (Snapshot.IsAreaErase && traits.AreaEraseHost);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void UpdateAreaEraser( )
    {
        if (!IsAreaEraserActive(State))
        {
            EndEraserCycle( );
            return;
        }

        if (State == TouchState.Eraser)
        {
            var (fingerCount, palmCount) = CountTouches(touches);

            if (palmCount > 0)
            {
                var geometry = GetPalmGeometry( );
                var restart = !areaEraseCycle || geometry.ClusterSignature != areaClusterSignature;

                // 手掌簇组成变化（新掌落下、种子掌抬起、碎片合并）意味着擦除主体换了：
                // 命中测试器不能沿旧中心到新中心的线段扫过，必须在新中心原地重建；
                // 同时重开调整窗口，让新主体重新决定直径
                if (restart)
                {
                    BeginAreaEraseCycle( );
                    areaClusterSignature = geometry.ClusterSignature;
                }

                eraser.Diameter = ResolvePalmDiameter(geometry);
                eraser.Show(geometry.Center);

                if (restart && eraser.Active)
                {
                    eraser.Rebase(geometry.CanvasCenter);
                }

                eraser.Update(geometry.CanvasCenter);
                return;
            }

            // 面积周期里手掌抬走后剩余手指停止擦除；手指也空时同样结束，避免空集合落到原点
            if (areaEraseUsed || fingerCount == 0)
            {
                EndEraserCycle( );
                return;
            }

            var (screenCenter, radius) = GetEnvelopeCircle(touches, options.PalmEraseFingers);
            eraser.Diameter = radius * 2 + options.Eraser.PalmExtra;
            eraser.Show(screenCenter);
            eraser.Update(GetCanvasCenter(touches, options.PalmEraseFingers, InnerCanvas));
        }
        else
        {
            if (!TryGetFirstFinger(touches, out var contact))
            {
                EndEraserCycle( );
                return;
            }

            eraser.Diameter = EraserDiameter;
            eraser.Show(contact.Position);
            eraser.Update(contact.Device.GetTouchPoint(InnerCanvas).Position);
        }
    }
}

internal sealed class Eraser(InkCanvas canvas, FrameworkElement feedback, TextBlock label, EraserParameters parameters)
{
    private readonly double rebuildThreshold = parameters.RebuildThreshold;

    private IncrementalStrokeHitTester? hitTester;

    private double hitTesterDiameter;

    internal double Diameter { get; set; } = parameters.Diameter;

    /// <summary>
    /// 反馈圆上显示的文本；null 或空串时只显示圆
    /// </summary>
    internal string? Label
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            label.Text = value;
            label.Visibility = Active && !string.IsNullOrEmpty(value) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>
    /// 画布缩放，由 CurrentScale 写入，用于视口直径与内容直径换算
    /// </summary>
    internal double Scale { get; set; } = 1.0;

    /// <summary>
    /// 擦除周期是否开着：从本周期第一次推进命中测试到周期结束
    /// </summary>
    internal bool Active { get; private set; }

    /// <summary>
    /// 命中测试进行中：这段窗口里的画布增删都由擦除器产生
    /// </summary>
    internal bool Recording { get; private set; }

    private double LogicalDiameter { get => Math.Max(Diameter / Scale, field); } = parameters.MinLogicalDiameter;

    internal void Show(Point screenPosition)
    {
        feedback.Width = Diameter;
        feedback.Height = Diameter;
        Canvas.SetLeft(feedback, screenPosition.X - Diameter / 2);
        Canvas.SetTop(feedback, screenPosition.Y - Diameter / 2);
        feedback.Visibility = Visibility.Visible;
        label.Visibility = string.IsNullOrEmpty(Label) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Start(Point canvasPosition)
    {
        if (Active)
        {
            return;
        }

        Active = true;
        CreateHitTester(canvasPosition);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void Move(Point canvasPosition)
    {
        if (!Active)
        {
            return;
        }

        hitTester?.AddPoint(canvasPosition);
    }

    internal void Update(Point canvasPosition)
    {
        // 命中回调里的画布增删属于当前批次，窗口要盖住整段命中测试
        Recording = true;
        try
        {
            if (!Active)
            {
                Start(canvasPosition);
            }
            else
            {
                SyncHitTester(canvasPosition);
                Move(canvasPosition);
            }
        }
        finally
        {
            Recording = false;
        }
    }

    /// <summary>
    /// 擦除主体切换时在新位置原地重建命中测试器：
    /// AddPoint 会沿上一采样点到新点的线段扫过，中心跳变必须断开这条路径
    /// </summary>
    internal void Rebase(Point canvasPosition)
    {
        if (!Active)
        {
            return;
        }

        // 结算旧测试器时可能补发命中，画布增删仍属于当前擦除批次
        Recording = true;
        try
        {
            DropHitTester( );
            CreateHitTester(canvasPosition);
        }
        finally
        {
            Recording = false;
        }
    }

    private void SyncHitTester(Point canvasPosition)
    {
        if (hitTester is null)
        {
            return;
        }

        if (Math.Abs(LogicalDiameter - hitTesterDiameter) < rebuildThreshold)
        {
            return;
        }

        DropHitTester( );
        CreateHitTester(canvasPosition);
    }

    private void DropHitTester( )
    {
        if (hitTester is null)
        {
            return;
        }

        hitTester.StrokeHit -= OnStrokeHit;
        hitTester.EndHitTesting( );
        hitTester = null;
    }

    internal void End( )
    {
        // 未开擦除周期时也会调用：隐藏先于 Active 判定，否则橡皮圈残留到下一次擦除
        feedback.Visibility = Visibility.Collapsed;
        label.Visibility = Visibility.Collapsed;

        if (!Active)
        {
            return;
        }

        Active = false;
        DropHitTester( );
    }

    private void CreateHitTester(Point position)
    {
        var diameter = LogicalDiameter;
        var shape = new EllipseStylusShape(diameter, diameter);
        hitTester = canvas.Strokes.GetIncrementalStrokeHitTester(shape);
        hitTester.StrokeHit += OnStrokeHit;
        hitTesterDiameter = diameter;
        hitTester.AddPoint(position);
    }

    private void OnStrokeHit(object sender, StrokeHitEventArgs e)
    {
        var hitStroke = e.HitStroke;

        if (!canvas.Strokes.Contains(hitStroke))
        {
            return;
        }

        canvas.Strokes.Replace([hitStroke], e.GetPointEraseResults( ));
    }
}
