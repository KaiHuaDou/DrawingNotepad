using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;

using static InkCanvasNext.Geometry;

namespace InkCanvasNext;

internal enum SelectionHandle
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

internal enum SelectionGesture
{
    None,
    Move,
    Scale,
    Rotate,
    Lasso,
    Pinch
}

public partial class InkCanvasNext
{

    private const double HandleHitRadius = 16;
    private const double LassoPointDistance2 = 16;

    /// <summary>旋转手柄命中半径（屏幕像素，÷zoom 折算为内容坐标）：独立于缩放恒定可点按。</summary>
    private const double RotateHitRadius = 24;

    private SelectionGesture selectionGesture = SelectionGesture.None;
    private SelectionHandle selectionHandle = SelectionHandle.None;
    private Point selectionStartPoint;
    private Point selectionLastPoint;
    private Point selectionAnchor;
    private Point selectionCenter;
    private Matrix selectionAbs = Matrix.Identity;
    private StrokeCollection? selectionTarget;

    /// <summary>旋转手势中手柄的轨道位置（内容坐标，绕选区中心等半径跟随鼠标角度）；空闲为 null。</summary>
    private Point? rotateHandleLive;

    /// <summary>供视觉层绘制：旋转手势进行中返回手柄轨道位置，否则 null（视觉层回落默认顶中位置）。</summary>
    internal Point? LiveRotateHandle => rotateHandleLive;

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
                selectionTarget = [with(selection.SelectedStrokes)];
                return;
            }

            if (selection.Bounds.Contains(p))
            {
                selectionGesture = SelectionGesture.Move;
                selectionTarget = [with(selection.SelectedStrokes)];
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
        rotateHandleLive = null;
        InnerCanvas.ReleaseMouseCapture( );
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

        // touches 保插入序：手势起始指在存活期间恒为 First()，抬起后由剩余手指自然接替
        var canvasPos = touches.First( ).Value.Device.GetTouchPoint(InnerCanvas).Position;
        selectionStartPoint = canvasPos;
        selectionLastPoint = canvasPos;

        if (!selection.HasSelection)
        {
            BeginLasso(canvasPos);
            return;
        }

        selectionTarget = [with(selection.SelectedStrokes)];

        if (touches.Count >= 2)
        {
            // 双指：直接进入缩放/旋转（连同平移），无需命中判定；基线取当前两指几何
            selectionGesture = SelectionGesture.Pinch;
            ResetPinchBaseline( );
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
            // 第二指落下：正常已由 TouchDown 处理器切入（基线取落下瞬间），此处兜底
            PromoteMoveToPinch( );
            return;
        }

        if (selectionGesture == SelectionGesture.Pinch)
        {
            UpdatePinch( );
            return;
        }

        if (selectionGesture == SelectionGesture.Lasso)
        {
            UpdateLassoTrack(touches.First( ).Value.Device.GetTouchPoint(InnerCanvas).Position);
            return;
        }

        if (selectionGesture is SelectionGesture.Move or SelectionGesture.Scale or SelectionGesture.Rotate)
        {
            UpdateSelectionTransform(touches.First( ).Value.Device.GetTouchPoint(InnerCanvas).Position);
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
        rotateHandleLive = null;
        selection.Invalidate( );
    }

    private void UpdatePinch( )
    {
        if (touches.Count < 2 || selectionTarget is null)
        {
            return;
        }

        (var c1, var c2) = GetPinchPoints( );
        var center = new Point((c1.X + c2.X) / 2, (c1.Y + c2.Y) / 2);
        var dist = Math.Sqrt(Geometry.Distance2(c1, c2));
        var angle = Math.Atan2(c2.Y - c1.Y, c2.X - c1.X);

        if (!pinchInit)
        {
            InitPinchBaseline(center, dist, angle);
            return;
        }

        var m = new Matrix( );
        m.Translate(center.X - pinchStartCenter.X, center.Y - pinchStartCenter.Y);
        // angle 为弧度，RotateAt 要求角度：需乘 RadToDeg
        m.RotateAt((angle - pinchStartAngle) * RadToDeg, pinchStartCenter.X, pinchStartCenter.Y);
        m.ScaleAt(dist / pinchStartDist, dist / pinchStartDist, pinchStartCenter.X, pinchStartCenter.Y);

        ApplyTransformDelta(m);
    }

    // ---------- 手势切换与主指 ----------

    /// <summary>移动中的选区在第二指落下时切入双指缩放/旋转：提交移动段、重置变换基线并即时采集捏合基线。</summary>
    private void PromoteMoveToPinch( )
    {
        CommitTransform( );
        selectionGesture = SelectionGesture.Pinch;
        selectionAbs = Matrix.Identity;
        selectionTarget = [with(selection.SelectedStrokes)];
        ResetPinchBaseline( );
    }

    /// <summary>以当前两指几何重置捏合基线（第二指落下瞬间调用，消除基线滞后导致的死区）。</summary>
    private void ResetPinchBaseline( )
    {
        if (touches.Count < 2)
        {
            pinchInit = false;
            return;
        }

        (var c1, var c2) = GetPinchPoints( );
        var center = new Point((c1.X + c2.X) / 2, (c1.Y + c2.Y) / 2);
        var dist = Math.Sqrt(Geometry.Distance2(c1, c2));
        InitPinchBaseline(center, dist, Math.Atan2(c2.Y - c1.Y, c2.X - c1.X));
    }

    private void InitPinchBaseline(Point center, double dist, double angle)
    {
        pinchInit = true;
        pinchStartCenter = center;
        pinchStartDist = Math.Max(dist, 1e-6);
        pinchStartAngle = angle;
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
                var scaleX = selectionHandle is not (SelectionHandle.T or SelectionHandle.B);
                var scaleY = selectionHandle is not (SelectionHandle.L or SelectionHandle.R);

                var dx = selectionStartPoint.X - selectionAnchor.X;
                var sx = (scaleX && Math.Abs(dx) > 1e-9) ? (p.X - selectionAnchor.X) / dx : 1.0;

                var dy = selectionStartPoint.Y - selectionAnchor.Y;
                var sy = (scaleY && Math.Abs(dy) > 1e-9) ? (p.Y - selectionAnchor.Y) / dy : 1.0;

                newAbs = Matrix.Identity;
                newAbs.ScaleAt(sx, sy, selectionAnchor.X, selectionAnchor.Y);
                break;

            case SelectionGesture.Rotate:
                newAbs = Matrix.Identity;
                // AngleFrom 返回弧度，RotateAt 要求角度：需乘 RadToDeg，否则旋转角度缩小约 57.3 倍
                newAbs.RotateAt(
                    (AngleFrom(selectionCenter, p) - AngleFrom(selectionCenter, selectionStartPoint)) * RadToDeg,
                    selectionCenter.X,
                    selectionCenter.Y);

                // 手柄沿鼠标角度绕选区中心做圆周运动，到旋转中心距离保持手势起始值（距离不变）
                var vx = p.X - selectionCenter.X;
                var vy = p.Y - selectionCenter.Y;
                var len = Math.Sqrt(vx * vx + vy * vy);
                if (len > 1e-9)
                {
                    var r0 = Distance(selectionCenter, selectionStartPoint);
                    rotateHandleLive = new Point(
                        selectionCenter.X + vx / len * r0,
                        selectionCenter.Y + vy / len * r0);
                }

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
        rotateHandleLive = null;

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
        InnerCanvas.ReleaseMouseCapture( );
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
        lassoTester = InnerCanvas.Strokes.GetIncrementalLassoHitTester(50);
        lassoTester.SelectionChanged += OnLassoSelectionChanged;
        lassoTester.AddPoints([ToStylusPoint(p)]);
    }

    /// <summary>套索轨迹追加（鼠标/触摸共用）：去抖后累积路径点并更新命中测试与套索视觉。</summary>
    private void UpdateLassoTrack(Point p)
    {
        if (Distance2(p, lassoPath[^1]) < LassoPointDistance2)
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
        lassoTester?.SelectionChanged -= OnLassoSelectionChanged;
        lassoTester = null;

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
        var hit = InnerCanvas.Strokes.HitTest(p);
        if (hit.Count > 0)
        {
            selection.SetStrokes(hit);
        }
        else
        {
            selection.Clear( );
        }
    }

    private SelectionHandle HitTestHandle(Point p)
    {
        var b = selection.Bounds;
        if (b.IsEmpty)
        {
            return SelectionHandle.None;
        }

        if (Near(p, SelectionVisual.RotateHandleCenter(b, currentScale), RotateHitRadius / Math.Max(currentScale, 1e-6)))
        {
            return SelectionHandle.Rotate;
        }
        else if (Near(p, b.TopLeft, HandleHitRadius))
        {
            return SelectionHandle.TL;
        }
        else if (Near(p, b.TopRight, HandleHitRadius))
        {
            return SelectionHandle.TR;
        }
        else if (Near(p, b.BottomLeft, HandleHitRadius))
        {
            return SelectionHandle.BL;
        }
        else if (Near(p, b.BottomRight, HandleHitRadius))
        {
            return SelectionHandle.BR;
        }
        else if (Near(p, new Point(b.Left + b.Width / 2, b.Top), HandleHitRadius))
        {
            return SelectionHandle.T;
        }
        else if (Near(p, new Point(b.Left + b.Width / 2, b.Bottom), HandleHitRadius))
        {
            return SelectionHandle.B;
        }
        else if (Near(p, new Point(b.Left, b.Top + b.Height / 2), HandleHitRadius))
        {
            return SelectionHandle.L;
        }
        else if (Near(p, new Point(b.Right, b.Top + b.Height / 2), HandleHitRadius))
        {
            return SelectionHandle.R;
        }
        else
        {
            return SelectionHandle.None;
        }
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
}
