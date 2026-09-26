using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;

using static System.Math;
using static InkCanvasNext.Geometry;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    private Point selectionStartPoint;
    private Point selectionAnchor;
    private Point selectionCenter;

    /// <summary>段内上一帧的绝对变换；本帧增量 = inv(selectionAbs) × 本帧绝对变换。</summary>
    private Matrix selectionAbs = Matrix.Identity;

    /// <summary>整段手势累计施加的增量之积，作为单个撤销单元提交。</summary>
    private Matrix selectionTotal = Matrix.Identity;

    private StrokeCollection? selectionTarget;

    private Point pinchStartCenter;
    private double pinchStartDist;
    private double pinchStartAngle;

    // ---------- 变换 ----------

    private void UpdateSelectionTransform(Point p)
    {
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
                var sx = scaleX && Abs(dx) > 1e-9 ? Max((p.X - selectionAnchor.X) / dx, MinSelectionScale) : 1.0;

                var dy = selectionStartPoint.Y - selectionAnchor.Y;
                var sy = scaleY && Abs(dy) > 1e-9 ? Max((p.Y - selectionAnchor.Y) / dy, MinSelectionScale) : 1.0;

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
                var len = Sqrt(vx * vx + vy * vy);
                if (len > 1e-9)
                {
                    var r0 = Distance(selectionCenter, selectionStartPoint);
                    RotateHandleLive = new Point(
                        selectionCenter.X + vx / len * r0,
                        selectionCenter.Y + vy / len * r0);
                }

                break;

            default:
                return;
        }

        ApplyTransformDelta(newAbs);
    }

    /// <summary>
    /// 以绝对矩阵为基准施加相对上一帧的增量变换。
    /// 增量必须左乘旧基线：作用于笔画的是"先撤销上一帧、再施加本帧"，
    /// 右乘得到的是旧基线对本帧绝对矩阵的共轭，只有两帧矩阵可交换（同族平移、同心缩放、同心旋转）时才等价。
    /// </summary>
    private void ApplyTransformDelta(Matrix newAbs)
    {
        // 奇异矩阵不可逆（如双指完全重合导致缩放为 0）：跳过本帧，否则 Invert 抛异常。
        // newAbs 相对段基线始终是绝对矩阵，下一有效帧的增量依旧正确，无需补偿；
        // selectionAbs 只会被赋 Identity 或已通过本守卫的矩阵，恒可逆。
        if (selectionTarget is not { Count: > 0 } || !newAbs.HasInverse)
        {
            return;
        }

        var inverse = selectionAbs;
        inverse.Invert( );
        var delta = inverse * newAbs;
        selectionAbs = newAbs;

        if (!IsNearIdentity(delta))
        {
            selectionTarget.Transform(delta, false);
            selectionTotal *= delta;
            selection.RecomputeBounds( );
            selection.Invalidate(halo: false);
        }

        RaiseViewOrSelectionChanged( );
    }

    /// <summary>整段手势（含中途重建基线）作为单个撤销单元提交。</summary>
    private void CommitTransform( )
    {
        if (selectionTarget is { Count: > 0 } && !IsNearIdentity(selectionTotal))
        {
            PushChange(new TransformChanges(selectionTarget, selectionTotal));
            // 变换不触发 StrokesChanged，手动上报以更新外部脏标记（无增删，仅 TransformOnly）
            StrokesChanged?.Invoke(
                this,
                new InkCanvasStrokesChangedEventArgs([], [], transformOnly: true));
        }
    }

    // ---------- 双指 ----------

    /// <summary>
    /// 进入双指手势：以当前两指几何重建基线，并把段基线归零。
    /// 笔画坐标与触点坐标同处内容坐标系，新段第一帧的增量即该帧绝对变换，段间不产生跳变。
    /// </summary>
    private void BeginPinch( )
    {
        selectionTarget = [with(selection.SelectedStrokes)];
        selectionGesture = SelectionGesture.Pinch;
        selectionAbs = Matrix.Identity;
        RefreshPinchBaseline( );
    }

    /// <summary>
    /// 触点数变化后重建手势基线（触点数只在 Down/Up 变化，故由 TrackTouchDown/TrackTouchUp 调用）：
    /// 双指落在单指手势上即升级为双指相似变换，双指手势减为单指即降级为拖动。
    /// 基线取变化瞬间的触点位置，手势从新的触点组合无缝续接。
    /// </summary>
    private void RefreshSelectionGesture( )
    {
        if (touches.Count >= 2 && selectionGesture is SelectionGesture.Move or SelectionGesture.Scale or SelectionGesture.Rotate)
        {
            BeginPinch( );
        }
        else if (touches.Count == 1 && selectionGesture == SelectionGesture.Pinch)
        {
            RebaseToMove( );
        }
    }

    /// <summary>
    /// 双指相似变换：把基线两指映射到当前两指，即 平移（中点位移）∘ 绕基线中点等比缩放 k ∘ 绕基线中点旋转 Δθ。
    /// 缩放与旋转的中心只能是基线两指中点，改用选区中心会让选区绕手指之外的一点转动而脱离手指。
    /// Matrix 的 RotateAt/ScaleAt/Translate 均为后乘（先调用者先作用于点），故按 R → S → T 构造。
    /// </summary>
    private void UpdatePinch( )
    {
        if (touches.Count < 2)
        {
            return;
        }

        (var c1, var c2) = GetPinchPoints( );
        var center = MidPoint(c1, c2);
        var dist = Distance(c1, c2);
        var angle = AngleFrom(c1, c2);

        var m = new Matrix( );
        m.RotateAt((angle - pinchStartAngle) * RadToDeg, pinchStartCenter.X, pinchStartCenter.Y);
        m.ScaleAt(dist / pinchStartDist, dist / pinchStartDist, pinchStartCenter.X, pinchStartCenter.Y);
        m.Translate(center.X - pinchStartCenter.X, center.Y - pinchStartCenter.Y);

        ApplyTransformDelta(m);
    }

    private void RefreshPinchBaseline( )
    {
        (var c1, var c2) = GetPinchPoints( );
        pinchStartCenter = MidPoint(c1, c2);
        pinchStartDist = Max(Distance(c1, c2), 1e-6);
        pinchStartAngle = AngleFrom(c1, c2);
    }

    /// <summary>捏合中抬起一指：以剩余指当前位置为新起点续接拖动，选区不停留在原地。</summary>
    private void RebaseToMove( )
    {
        selectionGesture = SelectionGesture.Move;
        selectionStartPoint = GetFirstCanvasPoint( );
        selectionAbs = Matrix.Identity;
    }
}
