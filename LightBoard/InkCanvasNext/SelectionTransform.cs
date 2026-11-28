using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;

using static System.Math;
using static InkCanvasNext.Geometry;

namespace InkCanvasNext;

// 单指变换：手势矩阵、帧合并、增量施加与提交，以及驱动指查找

public partial class InkCanvasNext
{
    private Point selectionStartPoint;
    private Point selectionAnchor;
    private Point selectionCenter;

    /// <summary>段内上一帧的绝对变换；本帧增量 = inv(selectionAbs) × 本帧绝对变换</summary>
    private Matrix selectionAbs = Matrix.Identity;

    /// <summary>整段手势累计施加的增量之积，作为单个撤销单元提交</summary>
    private Matrix selectionTotal = Matrix.Identity;

    private IReadOnlyList<Stroke>? selectionTarget;

    /// <summary>当前手势段的起始包围盒；每帧用段内绝对矩阵重新计算 AABB，避免误差逐帧累积</summary>
    private Rect selectionBoundsBase;

    private bool selectionFrameHooked;
    private bool selectionFramePending;

    // 变换

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

                // 手柄绕选区中心等半径跟随鼠标角度
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

    // 帧节拍：位移按渲染帧合并

    /// <summary>
    /// 首个事件立即生效，同一帧内随后的位移合并到下一帧；立即生效保证抬手前最后一次位移不丢
    /// </summary>
    private void RequestSelectionFrame( )
    {
        if (!selectionFrameHooked)
        {
            ApplySelectionFrame( );
            HookSelectionFrame( );
            return;
        }

        selectionFramePending = true;
    }

    private void HookSelectionFrame( )
    {
        if (selectionFrameHooked)
        {
            return;
        }

        selectionFrameHooked = true;
        CompositionTarget.Rendering += SelectionFrame;
    }

    private void SelectionFrame(object? o, EventArgs e)
    {
        var apply = selectionFramePending;
        UnhookSelectionFrame( );

        if (apply)
        {
            ApplySelectionFrame( );
        }
    }

    /// <summary>手势状态即将变化（落指、抬指、结束）前把合并中的位移补上</summary>
    private void FlushSelectionFrame( )
    {
        if (!selectionFrameHooked)
        {
            return;
        }

        UnhookSelectionFrame( );
        ApplySelectionFrame( );
    }

    private void UnhookSelectionFrame( )
    {
        if (!selectionFrameHooked)
        {
            return;
        }

        selectionFrameHooked = false;
        selectionFramePending = false;
        CompositionTarget.Rendering -= SelectionFrame;
    }

    private void ApplySelectionFrame( )
    {
        switch (selectionGesture)
        {
            case SelectionGesture.Move:
            case SelectionGesture.Scale:
            case SelectionGesture.Rotate:
                UpdateSelectionTransform(GetDriverCanvasPoint( ));
                break;

            case SelectionGesture.Pinch:
                UpdatePinch( );
                break;
        }
    }

    /// <summary>
    /// 以绝对矩阵为基准施加相对上一帧的增量变换
    /// 增量必须左乘旧基线：作用于笔画的是“先撤销上一帧、再施加本帧”，
    /// 右乘得到的是旧基线对本帧绝对矩阵的共轭，只有两帧矩阵可交换（同族平移、同心缩放、同心旋转）时才等价
    /// </summary>
    private void ApplyTransformDelta(Matrix newAbs)
    {
        // 奇异矩阵不可逆（如双指完全重合导致缩放为 0）：跳过本帧，否则 Invert 抛异常。
        // newAbs 相对段基线始终是绝对矩阵，下一有效帧的增量依旧正确，无需补偿；
        // selectionAbs 只会被赋 Identity 或已通过本守卫的矩阵，总是可逆
        if (selectionTarget is not { Count: > 0 } || !newAbs.HasInverse)
        {
            return;
        }

        var inverse = selectionAbs;
        inverse.Invert( );
        var delta = inverse * newAbs;
        selectionAbs = newAbs;

        if (IsNearIdentity(delta))
        {
            return;
        }

        foreach (var stroke in selectionTarget)
        {
            stroke.Transform(delta, false);
        }

        selectionTotal *= delta;

        // 无旋转分量时（平移与分轴缩放）用段起始包围盒乘段内绝对矩阵求 AABB，避免每帧遍历笔画；
        // 有旋转分量时该 AABB 会随角度变大，改为按笔画重新计算
        if (Abs(newAbs.M12) < 1e-9)
        {
            selection.TransformBounds(selectionBoundsBase, newAbs);
        }
        else
        {
            selection.RecomputeBounds( );
        }

        selection.Invalidate(halo: false);
        RaiseViewOrSelectionChanged( );
    }

    /// <summary>整段手势（含中途重建基线）作为单个撤销单元提交</summary>
    private void CommitTransform( )
    {
        if (selectionTarget is { Count: > 0 } && !IsNearIdentity(selectionTotal))
        {
            // 提交前取最终包围盒，供手柄吸附与画布扩展
            selection.RecomputeBounds( );
            PushChange(new TransformChanges(selectionTarget, selectionTotal));

            // 选中内容可被拖到画布外：手势结束时按最终包围盒扩展，中途扩展只会放大画布
            FitBounds(selection.Bounds);

            // 变换不触发 StrokesChanged，手动上报以更新外部脏标记（无增删，仅 TransformOnly）
            StrokesChanged?.Invoke(
                this,
                new InkCanvasStrokesChangedEventArgs([], [], transformOnly: true));
        }
    }

    /// <summary>
    /// 优先选区内或压在手柄上的手指，都不在时退回第一根触点
    /// </summary>
    private (int Id, Point Point) FindSelectionDriver( )
    {
        if (selection.HasSelection)
        {
            foreach (var (id, contact) in touches)
            {
                if (contact.IsPalm)
                {
                    continue;
                }

                var point = contact.Device.GetTouchPoint(InnerCanvas).Position;
                if (selection.Bounds.Contains(point) || HitTestHandle(point) != SelectionHandle.None)
                {
                    return (id, point);
                }
            }
        }

        foreach (var (id, contact) in touches)
        {
            if (!contact.IsPalm)
            {
                return (id, contact.Device.GetTouchPoint(InnerCanvas).Position);
            }
        }

        var first = touches.First( );
        return (first.Key, first.Value.Device.GetTouchPoint(InnerCanvas).Position);
    }

    private Point GetDriverCanvasPoint( )
    {
        if (SelectionDriverId is { } id && touches.TryGetValue(id, out var touch))
        {
            return touch.Device.GetTouchPoint(InnerCanvas).Position;
        }

        return GetFirstCanvasPoint(touches, InnerCanvas);
    }
}
