using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    private enum SelectionHandle
    {
        None,
        TL,
        TR,
        BL,
        BR,
        T,
        B,
        L,
        R,
        Rotate
    }

    private enum SelectionGesture
    {
        None,
        Move,
        Scale,
        Rotate,
        Lasso,
        Pinch
    }

    private const double HandleHitRadius = 16;
    private const double LassoPointDistance2 = 16;

    private SelectionGesture selectionGesture = SelectionGesture.None;
    private SelectionHandle selectionHandle = SelectionHandle.None;
    private Point selectionStartPoint;
    private Point selectionLastPoint;
    private Point selectionAnchor;
    private Point selectionCenter;
    private Matrix selectionAbs = Matrix.Identity;
    private StrokeCollection? selectionTarget;

    private readonly List<Point> lassoPath = [];
    private readonly HashSet<Stroke> lassoSelected = [];
    private IncrementalLassoHitTester? lassoTester;
    private bool lassoDragged;

    private bool pinchInit;
    private Point pinchStartCenter;
    private double pinchStartDist;
    private double pinchStartAngle;

    // ---------- 鼠标选择 ----------

    private void BeginMouseSelection(Point p)
    {
        ResetSelectionGesture( );
        selectionStartPoint = p;
        selectionLastPoint = p;

        if (selection.HasSelection)
        {
            var handle = HitTestHandle(p);
            if (handle != SelectionHandle.None)
            {
                selectionHandle = handle;
                selectionGesture = handle == SelectionHandle.Rotate ? SelectionGesture.Rotate : SelectionGesture.Scale;
                var b = selection.Bounds;
                selectionAnchor = GetAnchorFor(handle, b);
                selectionCenter = new Point(b.Left + b.Width / 2, b.Top + b.Height / 2);
                selectionTarget = new StrokeCollection(selection.SelectedStrokes);
                return;
            }

            if (selection.Bounds.Contains(p))
            {
                selectionGesture = SelectionGesture.Move;
                selectionTarget = new StrokeCollection(selection.SelectedStrokes);
                return;
            }
        }

        // 空白处按下：清空选型并开始套索
        BeginLasso(p);
    }

    private void UpdateMouseSelection(Point p)
    {
        switch (selectionGesture)
        {
            case SelectionGesture.Move:
            case SelectionGesture.Scale:
            case SelectionGesture.Rotate:
                UpdateSelectionTransform(p);
                break;

            case SelectionGesture.Lasso:
                UpdateLassoTrack(p);
                break;
        }
    }

    private void EndMouseSelection( )
    {
        switch (selectionGesture)
        {
            case SelectionGesture.Move:
            case SelectionGesture.Scale:
            case SelectionGesture.Rotate:
                CommitTransform( );
                break;

            case SelectionGesture.Lasso:
                EndLasso( );
                break;
        }

        selectionGesture = SelectionGesture.None;
        selectionTarget = null;
        Canvas.ReleaseMouseCapture( );
        selection.Invalidate( );
        RaiseViewOrSelectionChanged( );
    }

    // ---------- 触屏选择（配合 TouchState.Selection） ----------

    private void BeginSelectionTouch( )
    {
        ResetSelectionGesture( );
        if (touches.Count == 0)
        {
            return;
        }

        var canvasPos = touches.First( ).Value.Device.GetTouchPoint(Canvas).Position;
        selectionStartPoint = canvasPos;
        selectionLastPoint = canvasPos;

        if (!selection.HasSelection)
        {
            BeginLasso(canvasPos);
            return;
        }

        selectionTarget = new StrokeCollection(selection.SelectedStrokes);

        if (touches.Count >= 2)
        {
            // 双指：直接进入缩放/旋转（连同平移），无需命中判定
            selectionGesture = SelectionGesture.Pinch;
            pinchInit = false;
            return;
        }

        // 单指：命中手柄 → 缩放/旋转；选区内 → 移动；空白 → 触屏套索/点选
        var handle = HitTestHandle(canvasPos);
        if (handle != SelectionHandle.None)
        {
            selectionHandle = handle;
            selectionGesture = handle == SelectionHandle.Rotate ? SelectionGesture.Rotate : SelectionGesture.Scale;
            var bounds = selection.Bounds;
            selectionAnchor = GetAnchorFor(handle, bounds);
            selectionCenter = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
            return;
        }

        if (selection.Bounds.Contains(canvasPos))
        {
            selectionGesture = SelectionGesture.Move;
            return;
        }

        BeginLasso(canvasPos);
    }

    private void UpdateSelectionTouch( )
    {
        if (touches.Count == 0)
        {
            return;
        }

        if (selectionGesture == SelectionGesture.Move && touches.Count >= 2)
        {
            // 第二指落下：先记录此前的移动段，再切换为双指缩放/旋转
            CommitTransform( );
            selectionGesture = SelectionGesture.Pinch;
            pinchInit = false;
            selectionAbs = Matrix.Identity;
            selectionTarget = new StrokeCollection(selection.SelectedStrokes);
            return;
        }

        if (selectionGesture == SelectionGesture.Pinch)
        {
            UpdatePinch( );
            return;
        }

        if (selectionGesture == SelectionGesture.Lasso)
        {
            UpdateLassoTrack(touches.First( ).Value.Device.GetTouchPoint(Canvas).Position);
            return;
        }

        if (selectionGesture is SelectionGesture.Move or SelectionGesture.Scale or SelectionGesture.Rotate)
        {
            UpdateSelectionTransform(touches.First( ).Value.Device.GetTouchPoint(Canvas).Position);
        }
    }

    private void EndSelectionTouch( )
    {
        if (selectionGesture is SelectionGesture.Move or SelectionGesture.Scale or SelectionGesture.Rotate or SelectionGesture.Pinch)
        {
            CommitTransform( );
        }
        else if (selectionGesture == SelectionGesture.Lasso)
        {
            EndLasso( );
        }

        selectionGesture = SelectionGesture.None;
        selectionTarget = null;
        selection.Invalidate( );
    }

    private void UpdatePinch( )
    {
        if (touches.Count < 2 || selectionTarget is null)
        {
            return;
        }

        using var enumerator = touches.Values.GetEnumerator( );
        enumerator.MoveNext( );
        var (Device, Position) = enumerator.Current;
        enumerator.MoveNext( );
        var t2 = enumerator.Current;

        var c1 = Device.GetTouchPoint(Canvas).Position;
        var c2 = t2.Device.GetTouchPoint(Canvas).Position;
        var center = new Point((c1.X + c2.X) / 2, (c1.Y + c2.Y) / 2);
        var dist = Math.Sqrt(Geometry.Distance2(c1, c2));
        var angle = Math.Atan2(c2.Y - c1.Y, c2.X - c1.X);

        if (!pinchInit)
        {
            pinchInit = true;
            pinchStartCenter = center;
            pinchStartDist = Math.Max(dist, 1e-6);
            pinchStartAngle = angle;
            return;
        }

        var m = new Matrix( );
        m.Translate(center.X - pinchStartCenter.X, center.Y - pinchStartCenter.Y);
        m.RotateAt(angle - pinchStartAngle, pinchStartCenter.X, pinchStartCenter.Y);
        m.ScaleAt(dist / pinchStartDist, dist / pinchStartDist, pinchStartCenter.X, pinchStartCenter.Y);

        ApplyTransformDelta(m);
    }

    // ---------- 变换 ----------

    private void UpdateSelectionTransform(Point p)
    {
        if (selectionTarget is null || selectionTarget.Count == 0)
        {
            return;
        }

        Matrix newAbs;
        switch (selectionGesture)
        {
            case SelectionGesture.Move:
                newAbs = new Matrix(1, 0, 0, 1, p.X - selectionStartPoint.X, p.Y - selectionStartPoint.Y);
                break;

            case SelectionGesture.Scale:
            {
                var scaleX = selectionHandle is not (SelectionHandle.T or SelectionHandle.B);
                var scaleY = selectionHandle is not (SelectionHandle.L or SelectionHandle.R);
                var sx = scaleX ? (p.X - selectionAnchor.X) / (selectionStartPoint.X - selectionAnchor.X) : 1.0;
                var sy = scaleY ? (p.Y - selectionAnchor.Y) / (selectionStartPoint.Y - selectionAnchor.Y) : 1.0;
                newAbs = Matrix.Identity;
                newAbs.ScaleAt(sx, sy, selectionAnchor.X, selectionAnchor.Y);
                break;
            }

            case SelectionGesture.Rotate:
                newAbs = Matrix.Identity;
                newAbs.RotateAt(
                    AngleFrom(selectionCenter, p) - AngleFrom(selectionCenter, selectionStartPoint),
                    selectionCenter.X,
                    selectionCenter.Y);
                break;

            default:
                return;
        }

        selectionLastPoint = p;
        ApplyTransformDelta(newAbs);
    }

    /// <summary>
    /// 以绝对矩阵为基准施加相对上一帧的增量变换，避免浮点漂移累积。
    /// </summary>
    private void ApplyTransformDelta(Matrix newAbs)
    {
        // 奇异矩阵不可逆（如双指完全重合导致缩放为 0）：跳过本帧，否则 Invert 抛异常。
        // newAbs 相对手势起点始终是绝对矩阵，下一有效帧的增量依旧正确，无需补偿；
        // selectionAbs 只会被赋 Identity 或已通过本守卫的矩阵，恒可逆。
        if (!newAbs.HasInverse)
        {
            return;
        }

        var inverse = selectionAbs;
        inverse.Invert( );
        var delta = newAbs * inverse;

        selectionAbs = newAbs;

        if (!IsNearIdentity(delta))
        {
            selectionTarget!.Transform(delta, false);
            selection.RecomputeBounds( );
            selection.Invalidate(halo: false);
        }

        RaiseViewOrSelectionChanged( );
    }

    private void CommitTransform( )
    {
        if (selectionTarget is { Count: > 0 } && !IsNearIdentity(selectionAbs))
        {
            PushChange(new TransformChanges(selectionTarget, selectionAbs));
            // 变换不触发 StrokesChanged，手动上报以更新外部脏标记（无增删，仅 TransformOnly）
            StrokesChanged?.Invoke(
                this,
                new InkCanvasStrokesChangedEventArgs([], [], transformOnly: true));
        }
    }

    private void ResetSelectionGesture( )
    {
        selectionGesture = SelectionGesture.None;
        selectionHandle = SelectionHandle.None;
        selectionTarget = null;
        selectionAbs = Matrix.Identity;
        selectionStartPoint = default;
        selectionLastPoint = default;
        lassoPath.Clear( );
        lassoSelected.Clear( );
        lassoDragged = false;
        pinchInit = false;

        if (lassoTester is not null)
        {
            lassoTester.SelectionChanged -= OnLassoSelectionChanged;
            lassoTester.EndHitTesting( );
            lassoTester = null;
        }
    }

    /// <summary>取消进行中的鼠标选择手势（不记录变换），并释放捕获。用于离开 Select 模式/重置输入。</summary>
    private void CancelSelectionGesture( )
    {
        if (selectionGesture == SelectionGesture.None)
        {
            return;
        }

        ResetSelectionGesture( );
        Canvas.ReleaseMouseCapture( );
        selection.Invalidate( );
    }

    // ---------- 套索 ----------

    /// <summary>空白处按下（鼠标/触摸共用）：清空当前选型，以落点为起点开始增量套索测试。</summary>
    private void BeginLasso(Point p)
    {
        selection.Clear( );
        selectionGesture = SelectionGesture.Lasso;
        lassoPath.Add(p);
        lassoDragged = false;
        lassoTester = Canvas.Strokes.GetIncrementalLassoHitTester(50);
        lassoTester.SelectionChanged += OnLassoSelectionChanged;
        lassoTester.AddPoints([ToStylusPoint(p)]);
    }

    /// <summary>套索轨迹追加（鼠标/触摸共用）：去抖后累积路径点并更新命中测试与套索视觉。</summary>
    private void UpdateLassoTrack(Point p)
    {
        if (Geometry.Distance2(p, lassoPath[^1]) < LassoPointDistance2)
        {
            return;
        }

        lassoDragged = true;
        lassoPath.Add(p);
        lassoTester?.AddPoints([ToStylusPoint(p)]);
        selection.InvalidateLasso(lassoPath);
    }

    private void OnLassoSelectionChanged(object? sender, LassoSelectionChangedEventArgs e)
    {
        foreach (var s in e.SelectedStrokes)
        {
            lassoSelected.Add(s);
        }

        foreach (var s in e.DeselectedStrokes)
        {
            lassoSelected.Remove(s);
        }
    }

    private void EndLasso( )
    {
        lassoTester?.EndHitTesting( );
        if (lassoTester is not null)
        {
            lassoTester.SelectionChanged -= OnLassoSelectionChanged;
            lassoTester = null;
        }

        lassoPath.Clear( );
        selection.InvalidateLasso(null);

        if (lassoDragged)
        {
            selection.SetStrokes(lassoSelected);
        }
        else
        {
            // 无拖拽视为点选：命中单笔则选中，否则清除
            HandlePointSelect(selectionStartPoint);
        }
    }

    private void HandlePointSelect(Point p)
    {
        var hit = Canvas.Strokes.HitTest(p);
        if (hit.Count > 0)
        {
            selection.SetStrokes(hit);
        }
        else
        {
            selection.Clear( );
        }
    }

    // ---------- 命中与几何 ----------

    private SelectionHandle HitTestHandle(Point p)
    {
        var b = selection.Bounds;
        if (b.IsEmpty)
        {
            return SelectionHandle.None;
        }

        if (Near(p, b.TopLeft, HandleHitRadius))
        {
            return SelectionHandle.TL;
        }

        if (Near(p, b.TopRight, HandleHitRadius))
        {
            return SelectionHandle.TR;
        }

        if (Near(p, b.BottomLeft, HandleHitRadius))
        {
            return SelectionHandle.BL;
        }

        if (Near(p, b.BottomRight, HandleHitRadius))
        {
            return SelectionHandle.BR;
        }

        if (Near(p, new Point(b.Left + b.Width / 2, b.Top), HandleHitRadius))
        {
            return SelectionHandle.T;
        }

        if (Near(p, new Point(b.Left + b.Width / 2, b.Bottom), HandleHitRadius))
        {
            return SelectionHandle.B;
        }

        if (Near(p, new Point(b.Left, b.Top + b.Height / 2), HandleHitRadius))
        {
            return SelectionHandle.L;
        }

        if (Near(p, new Point(b.Right, b.Top + b.Height / 2), HandleHitRadius))
        {
            return SelectionHandle.R;
        }

        return SelectionHandle.None;
    }

    private static Point GetAnchorFor(SelectionHandle handle, Rect b)
    {
        var cx = b.Left + b.Width / 2;
        var cy = b.Top + b.Height / 2;
        return handle switch
        {
            SelectionHandle.TL => b.BottomRight,
            SelectionHandle.TR => b.BottomLeft,
            SelectionHandle.BL => b.TopRight,
            SelectionHandle.BR => b.TopLeft,
            SelectionHandle.T => new Point(cx, b.Bottom),
            SelectionHandle.B => new Point(cx, b.Top),
            SelectionHandle.L => new Point(b.Right, cy),
            SelectionHandle.R => new Point(b.Left, cy),
            _ => new Point(cx, cy)
        };
    }

    private static bool Near(Point a, Point b, double radius)
    {
        return Geometry.Distance2(a, b) <= radius * radius;
    }

    private static double AngleFrom(Point center, Point p)
    {
        return Math.Atan2(p.Y - center.Y, p.X - center.X);
    }

    private static bool IsNearIdentity(Matrix m)
    {
        return Math.Abs(m.M11 - 1) < 1e-6
            && Math.Abs(m.M22 - 1) < 1e-6
            && Math.Abs(m.M12) < 1e-6
            && Math.Abs(m.M21) < 1e-6
            && Math.Abs(m.OffsetX) < 1e-6
            && Math.Abs(m.OffsetY) < 1e-6;
    }

    private static StylusPoint ToStylusPoint(Point p)
    {
        return new StylusPoint(p.X, p.Y);
    }
}
