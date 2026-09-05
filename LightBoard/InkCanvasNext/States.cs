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
    private TouchState state = TouchState.Idle;

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

        var newState = state switch
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
                _ => state,
            },

            TouchState.EvalDraw => count switch
            {
                0 => TouchState.Idle,
                1 when x2 > c2 => TouchState.Draw,
                2 when d2 <= l2 => TouchState.PanZoom,
                3 or 4 when d2 <= l2 => TouchState.Pan,
                >= 5 when d2 <= l2 => TouchState.Eraser,
                >= 2 when d2 > l2 => TouchState.MultiDraw,
                _ => state,
            },

            TouchState.Draw => count switch
            {
                0 => TouchState.Idle,
                >= 2 when d2 > l2 => TouchState.MultiDraw,
                _ => state,
            },

            TouchState.MultiDraw => count switch
            {
                0 => TouchState.Idle,
                1 => TouchState.Draw,
                _ => state,
            },

            TouchState.PanZoom => count switch
            {
                0 => TouchState.Idle,
                3 or 4 when d2 <= l2 => TouchState.Pan,
                >= 5 when d2 <= l2 => TouchState.Eraser,
                > 2 when d2 > l2 => TouchState.MultiDraw,
                _ => state,
            },

            TouchState.Pan => count switch
            {
                0 => TouchState.Idle,
                >= 5 when d2 <= l2 => TouchState.Eraser,
                > 3 when d2 > l2 => TouchState.MultiDraw,
                _ => state,
            },

            TouchState.Eraser => count switch
            {
                0 => TouchState.Idle,
                > 5 when d2 > l2 => TouchState.MultiDraw,
                _ => state,
            },

            TouchState.Selection => count == 0 ? TouchState.Idle : TouchState.Selection,

            _ => state,
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
        if (state == newState)
        {
            return;
        }

        var from = state;

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
                    Canvas.EditingMode = InkCanvasEditingMode.None;
                    CaptureAll( );
                }

                InitGesture( );
                break;

            case TouchState.Eraser:
                ReleaseAll( );
                Canvas.EditingMode = InkCanvasEditingMode.None;
                CaptureAll( );
                break;

            case TouchState.MultiDraw:
                ReleaseAll( );
                Canvas.EditingMode = InkCanvasEditingMode.None;
                CaptureAll( );
                StartMultiTouch( );
                break;
        }

        if (IsAreaEraserActive(from) && !IsAreaEraserActive(newState))
        {
            EndEraserCycle( );
        }

        state = newState;
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
