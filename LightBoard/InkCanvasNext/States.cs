using System.Linq;
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
                1 or 2 when SelectionTouchTarget( ) => TouchState.Selection,
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

    /// <summary>该状态全程接管原生输入（Down/Move/Up 均 Handled，InkCanvas 不再收笔）。
    /// 与 IsAreaEraserActive 叠加构成事件的 Handled 判定。</summary>
    private static bool BlocksNativeInput(TouchState s)
    {
        return s is TouchState.PanZoom or TouchState.Pan or TouchState.MultiDraw or TouchState.Selection;
    }

    /// <summary>进入时需把 EditingMode 置 None 的状态（即"手势接管"状态）。</summary>
    private static bool OverridesEditingMode(TouchState s)
    {
        return s is TouchState.PanZoom or TouchState.Pan or TouchState.MultiDraw or TouchState.Eraser;
    }

    /// <summary>
    /// 迁移副作用由"退出源 + 进入目标"组合得出（参考 TouchStates3.md §5），
    /// 取代原先 26 个 (from, to) 元组特判。
    /// </summary>
    private void SetState(TouchState newState)
    {
        if (state == newState)
        {
            return;
        }

        var from = state;

        // ---- 退出源清理 ----
        if (from == TouchState.MultiDraw)
        {
            EndMultiTouch( );
        }

        if (from == TouchState.Selection)
        {
            EndSelectionTouch( );
        }

        // ---- 恢复模式：回 Idle，或离开"覆盖编辑模式"状态（MultiDraw→Draw 等须恢复 EditingMode）----
        if (newState == TouchState.Idle)
        {
            ReleaseAll( );
            RestoreMode( );
        }
        else if (OverridesEditingMode(from) && !OverridesEditingMode(newState))
        {
            RestoreMode( );
        }

        // ---- 进入目标 ----
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
                // (PanZoom→Pan) 只校准基准（TrackTouchDown/Up 已通用化 InitGesture），不重做 Release/Capture
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

        // ---- 离开擦除叠加态即结算一次橡皮循环（通用钩子，原样保留）----
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

    /// <summary>判断首指起点是否落在当前选区内，决定触屏进入选择操作而非绘制/平移。</summary>
    private bool SelectionTouchTarget( )
    {
        if (Mode != InkCanvasNextMode.Select || StampAction != StampAction.None || !selection.HasSelection)
        {
            return false;
        }

        if (touches.Count == 0)
        {
            return false;
        }

        var first = touches.First( );
        if (!touchCanvasStarts.TryGetValue(first.Key, out var start))
        {
            return false;
        }

        return selection.Bounds.Contains(start);
    }
}
