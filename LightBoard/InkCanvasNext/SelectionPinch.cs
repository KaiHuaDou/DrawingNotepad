using System.Windows;
using System.Windows.Media;

using static System.Math;
using static InkCanvasNext.Geometry;

namespace InkCanvasNext;

// 双指相似变换，以及触点数变化引起的手势组合切换

public partial class InkCanvasNext
{
    private Point pinchStartCenter;
    private double pinchStartDist;
    private double pinchStartAngle;

    /// <summary>
    /// 笔画与触点同处内容坐标系，新段第一帧的增量即该帧绝对变换，段间不跳变
    /// </summary>
    private void BeginPinch( )
    {
        selectionTarget = selection.SelectedStrokesArray;
        selectionBoundsBase = selection.Bounds;
        selectionGesture = SelectionGesture.Pinch;
        SelectionDriverId = null;
        selectionAbs = Matrix.Identity;
        RefreshPinchBaseline( );
    }

    /// <summary>
    /// 触点数只在 Down/Up 变化，由 TrackTouchDown/TrackTouchUp 调用；基线取变化瞬间的触点位置
    /// </summary>
    private void RefreshSelectionGesture( )
    {
        var fingerCount = CountTouches(touches).Fingers;

        // 套索在途时落在选区内的手指改为拖动选区，放弃在途套索并保留原选型
        if (IsPathGesture
            && fingerCount >= 2
            && selection.HasSelection
            && ClassifyTouchesAgainstSelection( ).Mixed)
        {
            var (driverId, driver) = FindSelectionDriver( );
            ResetSelectionGesture( );
            selectionVisual.ClearLasso( );
            SelectionDriverId = driverId;
            selectionStartPoint = driver;
            BeginHandleOrMove(driver);
            return;
        }

        if (fingerCount == 1 && selectionGesture == SelectionGesture.Pinch)
        {
            RebaseToMove( );
        }
        else if (fingerCount == 2
            && selectionGesture is SelectionGesture.Move or SelectionGesture.Scale or SelectionGesture.Rotate
            && ClassifyTouchesAgainstSelection( ).AllInside)
        {
            BeginPinch( );
        }
        else if (fingerCount >= 3
            && selectionGesture == SelectionGesture.Pinch
            && ClassifyTouchesAgainstSelection( ).AllInside)
        {
            RebaseToMove( );
        }
    }

    /// <summary>
    /// 双指相似变换：把基线两指映射到当前两指，即 平移（中点位移）∘ 绕基线中点等比缩放 k ∘ 绕基线中点旋转 Δθ
    /// 缩放与旋转的中心只能是基线两指中点，改用选区中心会让选区绕手指之外的一点转动而脱离手指
    /// Matrix 的 RotateAt/ScaleAt/Translate 均为后乘（先调用者先作用于点），故按 R → S → T 构造
    /// </summary>
    private void UpdatePinch( )
    {
        if (CountTouches(touches).Fingers < 2)
        {
            return;
        }

        (var c1, var c2) = GetPinchPoints(touches, InnerCanvas);
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
        (var c1, var c2) = GetPinchPoints(touches, InnerCanvas);
        pinchStartCenter = MidPoint(c1, c2);
        pinchStartDist = Max(Distance(c1, c2), 1e-6);
        pinchStartAngle = AngleFrom(c1, c2);
    }

    /// <summary>捏合中抬起一指：剩余指接着拖，选区不跳变</summary>
    private void RebaseToMove( )
    {
        selectionGesture = SelectionGesture.Move;
        (SelectionDriverId, selectionStartPoint) = FindSelectionDriver( );
        selectionAbs = Matrix.Identity;
        selectionBoundsBase = selection.Bounds;
    }

    /// <summary>驱动指抬起后换一根手指接手，基线归零</summary>
    private void RebaseSelectionDriver( )
    {
        if (selectionGesture is not (SelectionGesture.Move or SelectionGesture.Scale or SelectionGesture.Rotate)
            || CountTouches(touches).Fingers == 0)
        {
            return;
        }

        (SelectionDriverId, selectionStartPoint) = FindSelectionDriver( );
        selectionAbs = Matrix.Identity;
        selectionBoundsBase = selection.Bounds;
    }
}
