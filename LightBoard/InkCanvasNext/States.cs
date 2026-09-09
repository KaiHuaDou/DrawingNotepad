using System.Windows.Controls;

namespace InkCanvasNext;

internal enum TouchState
{
    Idle,
    EvalDraw,
    Draw,
    PanZoom,
    Pan,
    Eraser,
    MultiDraw,
    Selection
}

public partial class InkCanvasNext
{
    private InkCanvasNextMode prevMode = InkCanvasNextMode.Ink;

    internal TouchState State { get; private set; } = TouchState.Idle;

    /// <summary>
    /// 触点数变化（Down/Up），或 EvalDraw 状态下 Move 时重评迁移。
    /// 守卫内联写死：switch 臂自上而下即优先级（选区 > MultiDraw > 单指 > 平移，参考 TouchStates3.md §4）；
    /// d2/x2 每次重评只计算一次；全部不命中 → 保持当前状态。
    /// </summary>
    private void UpdateState( )
    {
        var count = touches.Count;
        var d2 = GetMaxDistance2( );
        var l2 = distanceThreshold2;
        var x2 = Get1stFingerDispl2( );
        var c2 = touchDisplThreshold * touchDisplThreshold;

        var newState = State switch
        {
            TouchState.Idle => count switch
            {
                0 => TouchState.Idle,
                // 选区入口是高优先级候选：仅 count∈{1,2} 时评估（3+ 指不误入）
                1 or 2 when SelectionTouchCandidate( ) => TouchState.Selection,
                1 => TouchState.EvalDraw,
                2 when d2 <= l2 => TouchState.PanZoom,
                3 or 4 when d2 <= l2 => TouchState.Pan,
                >= 5 when d2 <= l2 => TouchState.Eraser,
                // d > l 视为多人两侧同时落笔，优先于平移
                >= 2 when d2 > l2 => TouchState.MultiDraw,
                _ => State,
            },

            TouchState.EvalDraw => count switch
            {
                0 => TouchState.Idle,
                1 when x2 > c2 => TouchState.Draw,
                2 when d2 <= l2 => TouchState.PanZoom,
                3 or 4 when d2 <= l2 => TouchState.Pan,
                >= 5 when d2 <= l2 => TouchState.Eraser,
                >= 2 when d2 > l2 => TouchState.MultiDraw,
                _ => State,
            },

            TouchState.Draw => count switch
            {
                0 => TouchState.Idle,
                >= 2 when d2 > l2 => TouchState.MultiDraw,
                _ => State,
            },

            TouchState.MultiDraw => count switch
            {
                0 => TouchState.Idle,
                1 => TouchState.Draw,
                _ => State,
            },

            TouchState.PanZoom => count switch
            {
                0 => TouchState.Idle,
                1 or >= 3 when d2 <= l2 => TouchState.Pan,
                > 2 when d2 > l2 => TouchState.MultiDraw,
                _ => State,
            },

            TouchState.Pan => count switch
            {
                0 => TouchState.Idle,
                >= 5 when d2 <= l2 => TouchState.Eraser,
                > 3 when d2 > l2 => TouchState.MultiDraw,
                _ => State,
            },

            TouchState.Eraser => count switch
            {
                0 => TouchState.Idle,
                > 5 when d2 > l2 => TouchState.MultiDraw,
                _ => State,
            },

            TouchState.Selection => count switch
            {
                0 => TouchState.Idle,
                // 选区手势中附加手指：3/4 指并拢平移画布、5 指及以上切换为掌心擦除（与 Idle 入口优先级一致）
                3 or 4 when d2 <= l2 => TouchState.Pan,
                >= 5 when d2 <= l2 => TouchState.Eraser,
                _ => TouchState.Selection,
            },

            _ => State,
        };

        SetState(newState);
    }

    private static bool BlocksNativeInput(TouchState s)
    {
        return s is TouchState.PanZoom or TouchState.Pan or TouchState.MultiDraw or TouchState.Selection;
    }

    private static bool OverridesEditingMode(TouchState s)
    {
        return s is TouchState.PanZoom or TouchState.Pan or TouchState.MultiDraw or TouchState.Eraser;
    }

    private void SetState(TouchState newState)
    {
        if (State == newState)
        {
            return;
        }

        var from = State;

        if (from == TouchState.MultiDraw)
        {
            EndMultiTouch( );
        }

        if (from == TouchState.Selection)
        {
            EndSelectionTouch( );
        }

        if (newState == TouchState.Idle)
        {
            ReleaseAll( );
            RestoreMode( );
        }
        else if (OverridesEditingMode(from) && !OverridesEditingMode(newState))
        {
            RestoreMode( );
        }

        if (from == TouchState.Idle)
        {
            prevMode = Mode;
        }

        // 形状只允许在单指绘制上下文（EvalDraw/Draw）存活：
        // 一旦迁出手势接管态（平移/缩放/多指/擦除/选区/回 Idle），放弃未提交的形状预览，
        // 避免 shapeActive 在事件处理器中抢占手势路由（如形状绘制中落第二指导致平移缩放失效）。
        if (shapeActive && from is TouchState.EvalDraw or TouchState.Draw && newState is not (TouchState.EvalDraw or TouchState.Draw))
        {
            CancelShape( );
        }

        switch (newState)
        {
            case TouchState.EvalDraw:
                if (WantsPreemptiveDrawCapture( ))
                {
                    CaptureAll( );
                }

                break;

            case TouchState.Draw:
                // (MultiDraw, Draw) 已在恢复阶段处理
                if (from == TouchState.EvalDraw && WantsPreemptiveDrawCapture( ))
                {
                    CaptureAll( );
                }

                break;

            case TouchState.Selection:
                CaptureAll( );
                BeginSelectionTouch( );
                break;

            case TouchState.PanZoom:
            case TouchState.Pan:
                if (from != TouchState.PanZoom)
                {
                    ReleaseAll( );
                    InnerCanvas.EditingMode = InkCanvasEditingMode.None;
                    CaptureAll( );
                }

                InitGesture( );
                break;

            case TouchState.Eraser:
                ReleaseAll( );
                InnerCanvas.EditingMode = InkCanvasEditingMode.None;
                CaptureAll( );
                break;

            case TouchState.MultiDraw:
                ReleaseAll( );
                InnerCanvas.EditingMode = InkCanvasEditingMode.None;
                CaptureAll( );
                StartMultiTouch( );
                break;
        }

        if (IsAreaEraserActive(from) && !IsAreaEraserActive(newState))
        {
            EndEraserCycle( );
        }

        State = newState;
    }

    private void RestoreMode( )
    {
        ApplyModeToEditing(prevMode);
    }

    /// <summary>选择工具（且未进入盖章）下的任意单/双指触摸由选区状态接管：有选区则命中手柄缩放/
    /// 旋转、选区内移动、双指缩放；无选区或落点在空白处则进入触屏套索/点选。3+ 指不进入（调用方 count 臂限定）。</summary>
    private bool SelectionTouchCandidate( )
    {
        return Mode == InkCanvasNextMode.Select && StampAction == StampAction.None;
    }
}
