using System.Runtime.CompilerServices;
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

internal readonly record struct ToolSnapshot(InkCanvasNextMode Mode, StampAction Stamp)
{
    internal bool IsStamp => Stamp != StampAction.None;

    internal bool IsSelectionEntry => Mode == InkCanvasNextMode.Select && !IsStamp;

    internal bool IsSelect => Mode == InkCanvasNextMode.Select;

    internal bool IsAreaErase => Mode == InkCanvasNextMode.EraseArea;

    internal bool IsShape => Mode is InkCanvasNextMode.Line or InkCanvasNextMode.Circle;

    internal bool IsCircle => Mode == InkCanvasNextMode.Circle;

    internal bool IsErase => Mode is InkCanvasNextMode.EraseArea or InkCanvasNextMode.EraseStroke;

    internal bool AllowsMultiDraw => !IsErase;

    internal bool WantsPreemptiveCapture => IsAreaErase || IsStamp;

    internal bool IsStampSingleFinger(TouchState state)
    {
        return IsStamp && state is TouchState.EvalDraw or TouchState.Draw;
    }
}

/// <summary>
/// 触摸状态的状态级元数据：每项回答一个独立问题，事件答复与编辑模式覆盖均由本表推导。
/// </summary>
/// <param name="BlocksNativeInput">事件全程接管（Down/Move/Up 均 Handled），原生不再收笔/移动选区。</param>
/// <param name="OverridesEditing">持有画布级手势：进入时把原生编辑模式压为 None，退出时恢复。</param>
/// <param name="PalmErase">掌心擦除态：触点包络作为橡皮实时擦除。</param>
/// <param name="AreaEraseHost">面积擦宿主：EraseArea 工具在这些绘制上下文中持续擦除。</param>
internal readonly record struct StateTraits(
    bool BlocksNativeInput = false,
    bool OverridesEditing = false,
    bool PalmErase = false,
    bool AreaEraseHost = false
);

public partial class InkCanvasNext
{
    /// <summary>
    /// 盖章武装标记：本次触摸序列在盖章模式下以单指 EvalDraw 开始，且尚未升级为任何手势接管态。
    /// </summary>
    private bool stampArmed;

    internal TouchState State { get; private set; } = TouchState.Idle;

    /// <summary>
    /// 当前工具快照，工具差异判定的唯一读取入口。
    /// </summary>
    internal ToolSnapshot Tool => new(Mode, StampAction);

    private static StateTraits Traits(TouchState state)
    {
        return state switch
        {
            TouchState.PanZoom or TouchState.Pan => new(BlocksNativeInput: true, OverridesEditing: true),
            TouchState.MultiDraw => new(BlocksNativeInput: true, OverridesEditing: true, AreaEraseHost: true),
            TouchState.Selection => new(BlocksNativeInput: true),
            TouchState.Eraser => new(OverridesEditing: true, PalmErase: true),
            TouchState.EvalDraw or TouchState.Draw => new(AreaEraseHost: true),
            _ => default,
        };
    }

    private bool HandlesEvent(TouchState state)
    {
        return Traits(state).BlocksNativeInput || IsAreaEraserActive(state) || Tool.IsStampSingleFinger(state);
    }

    /// <summary>
    /// 触点数变化（Down/Up），或 EvalDraw 状态下 Move 时重评迁移。
    /// 守卫内联写死：switch 臂自上而下即优先级（选区 > MultiDraw > 单指 > 平移，见 docs/TouchStates2.md）；
    /// d2/x2 每次重评只计算一次；全部不命中 → 保持当前状态。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void UpdateState( )
    {
        var count = touches.Count;
        var d2 = GetMaxDistance2( );
        var l2 = distanceThreshold2;
        var c2 = TouchDisplacementThreshold2;
        var x2 = Get1stFingerDispl2( );
        var tool = Tool;

        var newState = State switch
        {
            TouchState.Idle => count switch
            {
                0 => TouchState.Idle,
                // 选区入口是高优先级候选：仅 count∈{1,2} 时评估（3+ 指不误入）
                1 or 2 when tool.IsSelectionEntry => TouchState.Selection,
                1 => TouchState.EvalDraw,
                2 when d2 <= l2 => TouchState.PanZoom,
                3 or 4 when d2 <= l2 => TouchState.Pan,
                >= 5 when d2 <= l2 => TouchState.Eraser,
                >= 2 when d2 > l2 => TouchState.MultiDraw,
                _ => State,
            },

            TouchState.EvalDraw => count switch
            {
                0 => TouchState.Idle,
                1 when x2 > c2 => TouchState.Draw,
                // 擦除工具多指手势与普通模式同款（并拢判定），差异：5+ 指无论间距均掌擦（手掌压屏常呈张开），
                // 多指张开不迁多指画笔、保持擦除上下文
                >= 5 when d2 <= l2 || tool.IsErase => TouchState.Eraser,
                2 when d2 <= l2 => TouchState.PanZoom,
                3 or 4 when d2 <= l2 => TouchState.Pan,
                >= 2 when d2 > l2 && tool.AllowsMultiDraw => TouchState.MultiDraw,
                _ => State,
            },

            TouchState.Draw => count switch
            {
                0 => TouchState.Idle,
                >= 5 when tool.IsErase => TouchState.Eraser,
                >= 2 when tool.AllowsMultiDraw => TouchState.MultiDraw,
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
                > 2 when d2 > l2 && tool.AllowsMultiDraw => TouchState.MultiDraw,
                _ => State,
            },

            TouchState.Pan => count switch
            {
                0 => TouchState.Idle,
                >= 5 when d2 <= l2 || tool.IsErase => TouchState.Eraser,
                > 3 when d2 > l2 && tool.AllowsMultiDraw => TouchState.MultiDraw,
                _ => State,
            },

            TouchState.Eraser => count switch
            {
                0 => TouchState.Idle,
                // 擦除工具多指全程掌擦，不迁多指画笔
                > 5 when d2 > l2 && tool.AllowsMultiDraw => TouchState.MultiDraw,
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

    /// <summary>
    /// 状态切换：先按迁出状态结算（提交/取消/补余量），再按迁入状态建立（捕获/基线/编辑模式）。
    /// State 在两侧结算完成后才更新，迁入规则据此仍可读到迁出状态。
    /// </summary>
    private void SetState(TouchState newState)
    {
        if (State == newState)
        {
            return;
        }

        var from = State;

        ExitState(from, newState);
        EnterState(newState, from);

        State = newState;
    }

    private void ExitState(TouchState from, TouchState to)
    {
        if (from == TouchState.MultiDraw)
        {
            EndMultiTouch( );
        }

        if (from == TouchState.Selection)
        {
            EndSelectionTouch( );
        }

        // 迁出平移/缩放手势族（含回 Idle 或转擦除/多指）即视为手势结束，补足右/下边距
        if (from is TouchState.Pan or TouchState.PanZoom && to is not (TouchState.Pan or TouchState.PanZoom))
        {
            EnsureEdgeMargin( );
        }

        // 离开覆盖编辑模式的手势态：把编辑模式恢复为当前工具；回 Idle 时同时释放捕获
        if (to == TouchState.Idle)
        {
            ReleaseAll( );
            ApplyModeToEditing(Mode);
        }
        else if (Traits(from).OverridesEditing && !Traits(to).OverridesEditing)
        {
            ApplyModeToEditing(Mode);
        }

        // 形状只允许在单指绘制上下文（EvalDraw/Draw）存活：迁入 MultiDraw 时按当前预览提交，
        // 迁入其余接管态（平移/缩放/擦除/选区/回 Idle）则放弃预览，
        // 避免 shapeActive 在事件处理器中抢占手势路由。
        if (shapeActive && from is TouchState.EvalDraw or TouchState.Draw && to is not (TouchState.EvalDraw or TouchState.Draw))
        {
            if (to == TouchState.MultiDraw)
            {
                CommitShape( );
            }
            else
            {
                CancelShape( );
            }
        }

        // 盖章只由未升级为手势的单指序列触发：迁入任何手势接管态（含 MultiDraw）即解除。
        // 解除不依赖盖章仍激活，避免中途退出盖章后残留脏标记
        if (to is not (TouchState.EvalDraw or TouchState.Draw))
        {
            stampArmed = false;
        }

        if (IsAreaEraserActive(from) && !IsAreaEraserActive(to))
        {
            EndEraserCycle( );
        }
    }

    private void EnterState(TouchState to, TouchState from)
    {
        // 盖章只由未升级为手势的单指序列触发：Idle 进入 EvalDraw 时武装
        if (from == TouchState.Idle && to == TouchState.EvalDraw && Tool.IsStamp)
        {
            stampArmed = true;
        }

        switch (to)
        {
            case TouchState.EvalDraw:
                if (Tool.WantsPreemptiveCapture)
                {
                    CaptureAll( );
                }

                break;

            case TouchState.Draw:
                // (MultiDraw, Draw) 无需捕获：多指手势期间触点已捕获
                if (from == TouchState.EvalDraw && Tool.WantsPreemptiveCapture)
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
                HandOffDrawingStroke( );
                ReleaseAll( );
                InnerCanvas.EditingMode = InkCanvasEditingMode.None;
                CaptureAll( );
                StartMultiTouch( );
                break;
        }
    }
}
