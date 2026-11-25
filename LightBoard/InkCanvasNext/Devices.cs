using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    private readonly OrderedDictionary<int, TouchContact> touches = [with(20)];
    private readonly Dictionary<int, Point> touchStarts = [];
    private bool releasingCaptures;

    internal int ActiveTouchCount => touches.Count;

    /// <summary>
    /// 重置触摸输入状态：释放全部触摸捕获、清空触点并取消进行中的手势
    /// </summary>
    public void ResetTouchState( )
    {
        // 合并中的位移按当前触点结清，否则重置会吞掉最后一帧
        FlushSelectionFrame( );

        releasingCaptures = true;
        try
        {
            foreach (var contact in touches.Values)
            {
                UnsubscribeDeactivated(contact.Device);
                contact.Device.Capture(null);
            }

            touches.Clear( );
            touchStarts.Clear( );
            ResetPalmState( );
            MousePointer.Reset( );
            PenPointer.Reset( );
            HidePenCursor( );
            arbiter.Reset( );
            ReleasePenCapture( );
            SetState(TouchState.Idle);
            InnerCanvas.AbortWetInk( );
            CancelSelectionGesture( );
            CancelShape( );
            CancelEdgeScroll( );
        }
        finally
        {
            releasingCaptures = false;
        }
    }

    private void CanvasLostTouchCapture(object? o, TouchEventArgs e)
    {
        if (releasingCaptures)
        {
            return;
        }

        RemoveDevice(e.TouchDevice);

        if (e.TouchDevice is SyntheticTouchDevice pointer)
        {
            pointer.Reset( );
        }

        UpdateAreaEraser( );
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void CanvasPreviewTouchDown(object? o, TouchEventArgs e)
    {
        CancelEdgeScroll( );
        HidePenCursor( );

        var touchPoint = e.GetTouchPoint(this);

        // 触点集合变化会换掉手势基线：先把合并中的位移按旧触点结清
        FlushSelectionFrame( );

        // 先分类再仲裁：手掌落下前要提交在绘笔迹，其他触摸维持原有的 CancelLower 丢弃语义
        var isPalm = e.TouchDevice is not SyntheticTouchDevice
            && ShouldTreatAsPalm(touchPoint.Position, touchPoint.Size);

        if (e.TouchDevice is not SyntheticTouchDevice)
        {
            if (arbiter.TouchCount == 0)
            {
                if (isPalm)
                {
                    InnerCanvas.CommitStroke( );
                }

                CancelLower(InputKind.Touch);
            }

            arbiter.Begin(InputKind.Touch, e.TouchDevice.Id);
        }

        TrackTouchDown(e.TouchDevice.Id, e.TouchDevice, touchPoint.Position, touchPoint.Size, isPalm);
        SubscribeDeactivated(e.TouchDevice);
        UpdateState( );

        // 接管判定由 HandlesEvent 统一给出（手势接管态、面积擦叠加态、盖章的单指上下文）；
        // 手掌触点在普通绘制态也需要拦截，避免落墨
        e.Handled = HandlesEvent(State) || isPalm;

        if (!isPalm)
        {
            if (State == TouchState.Selection)
            {
                // 选区手势中新落的手指同样要捕获：CaptureAll 只在进入 Selection 时执行一次，
                // 未捕获的手指移出画布边界即触发 TouchLeave 而被移除
                e.TouchDevice.Capture(InnerCanvas);
            }
            else if (State == TouchState.MultiDraw)
            {
                // 形状已由 SetState 钩子取消（离开 EvalDraw/Draw 进入手势接管态），此处无需重复处理
                e.TouchDevice.Capture(InnerCanvas);
                if (!multiTouchStrokes.ContainsKey(e.TouchDevice.Id))
                {
                    var canvasPos = e.GetTouchPoint(InnerCanvas).Position;
                    StartMultiTouchStroke(e.TouchDevice.Id, canvasPos);
                }
            }
            else if (Snapshot.IsShape && State == TouchState.EvalDraw && !shapeActive)
            {
                var canvasPos = e.GetTouchPoint(InnerCanvas).Position;
                StartShape(canvasPos);
                e.TouchDevice.Capture(InnerCanvas);
                e.Handled = true;
            }
        }

        UpdateAreaEraser( );
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void CanvasPreviewTouchMove(object? o, TouchEventArgs e)
    {
        var touchPoint = e.GetTouchPoint(this);
        var id = e.TouchDevice.Id;

        if (!touches.TryGetValue(id, out var touch))
        {
            return;
        }

        touch = touch with { Position = touchPoint.Position, Size = touchPoint.Size };
        touches[id] = touch;

        // 手掌移动也重评迁移：选区内落掌、绘制结束后滞留的手掌都能在移动时回到正确状态
        if (touch.IsPalm)
        {
            UpdateState( );
            e.Handled = true;
            UpdateAreaEraser( );
            return;
        }

        if (multiTouchStrokes.ContainsKey(e.TouchDevice.Id))
        {
            var canvasPos = e.GetTouchPoint(InnerCanvas).Position;
            ContinueMultiTouchStroke(e.TouchDevice.Id, canvasPos);
            e.Handled = true;
            return;
        }

        // 路由只由状态机决定：形状在途时仅当状态仍在单指绘制上下文（EvalDraw/Draw）才更新预览，
        // 一旦状态机迁入手势接管态（PanZoom/Pan/MultiDraw/Eraser/Selection），形状已被 SetState 取消，
        // Move 事件不会再被 shapeActive 抢占。
        switch (State)
        {
            case TouchState.EvalDraw:
                UpdateShapeIfActive(e);
                UpdateState( );
                break;

            case TouchState.Draw:
                UpdateShapeIfActive(e);
                break;

            case TouchState.EvalPan:
                // 先平移后重评：提交帧的位移用旧基准结算，重评触发的 InitGesture 再以新位置重立基准
                Pan( );
                UpdateState( );
                break;

            case TouchState.PanZoom: PanZoom( ); break;
            case TouchState.Pan: Pan( ); break;
            case TouchState.Selection: UpdateSelectionTouch( ); break;
        }

        // EvalDraw/Draw 保留未拦截（InkCanvas 原生收笔）；其余接管态见 HandlesEvent；
        // MultiDraw 的 Move 已在上面多画笔画分支接管
        if (HandlesEvent(State))
        {
            e.Handled = true;
        }

        UpdateAreaEraser( );
    }

    /// <summary>形状模式下的 EvalDraw/Draw 路由：形状在途时用终点触点更新预览并拦截事件；
    /// 形状已被状态机取消（手势接管）或非形状模式时为空操作，让原生 InkCanvas 收笔</summary>
    private void UpdateShapeIfActive(TouchEventArgs e)
    {
        if (!shapeActive)
        {
            return;
        }

        UpdateShape(GetFirstCanvasPoint(touches, InnerCanvas));
        e.Handled = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void CanvasPreviewTouchUp(object? o, TouchEventArgs e)
    {
        var wasHandled = HandlesEvent(State);
        var wasMultiTouch = multiTouchStrokes.ContainsKey(e.TouchDevice.Id);
        var isPalm = IsPalmTouch(touches, e.TouchDevice.Id);

        if (wasMultiTouch)
        {
            EndMultiTouchStroke(e.TouchDevice.Id);
        }

        // 形状只允许在单指绘制上下文（EvalDraw/Draw）存活：仅当手势未被状态机接管
        // （未迁入平移/缩放/多指等）时才提交，否则由 SetState 已取消、此处直接跳过 形状只由插入序第一指驱动：其余手指的移动与抬起均忽略，第一指抬起时按其触点提交
        if (!isPalm && shapeActive && State is TouchState.EvalDraw or TouchState.Draw && IsFirstTouch(touches, e.TouchDevice.Id))
        {
            UpdateShape(e.GetTouchPoint(InnerCanvas).Position);
            CommitShape( );
            e.Handled = true;
        }

        // 盖章提交：未升级为手势的单指序列在其抬手位置落章
        if (!isPalm && stampArmed && State is TouchState.EvalDraw or TouchState.Draw
            && TryStamp(e.GetTouchPoint(InnerCanvas).Position))
        {
            e.Handled = true;
        }

        RemoveDevice(e.TouchDevice);
        e.Handled |= wasHandled || wasMultiTouch;
        UpdateAreaEraser( );
    }

    private void CanvasTouchLeave(object? o, TouchEventArgs e)
    {
        if (releasingCaptures || e.TouchDevice.Captured == InnerCanvas)
        {
            return;
        }

        RemoveDevice(e.TouchDevice);
        UpdateAreaEraser( );
    }
}
