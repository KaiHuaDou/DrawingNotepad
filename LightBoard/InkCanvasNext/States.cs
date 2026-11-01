using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace InkCanvasNext;

internal enum TouchState
{
    Idle,
    EvalDraw,
    Draw,
    EvalPan,
    PanZoom,
    Pan,
    Eraser,
    MultiDraw,
    Selection
}

internal readonly record struct ToolSnapshot(InkCanvasNextMode Mode, StampAction Stamp, bool IsReadOnly)
{
    internal bool IsStamp => !IsReadOnly && Stamp != StampAction.None;

    internal bool IsSelectionEntry => !IsReadOnly && Mode == InkCanvasNextMode.Select && !IsStamp;

    internal bool IsSelect => !IsReadOnly && Mode == InkCanvasNextMode.Select;

    internal bool IsAreaErase => !IsReadOnly && Mode == InkCanvasNextMode.EraseArea;

    internal bool IsShape => !IsReadOnly && Mode is InkCanvasNextMode.Line or InkCanvasNextMode.Circle;

    internal bool IsCircle => !IsReadOnly && Mode == InkCanvasNextMode.Circle;

    internal bool IsErase => !IsReadOnly && Mode is InkCanvasNextMode.EraseArea or InkCanvasNextMode.EraseStroke;

    internal bool AllowsMultiDraw => !IsReadOnly && !IsErase;

    /// <summary>
    /// 自定义工具：鼠标经合成触点进入状态机；原生工具（墨迹/按笔擦）维持原生鼠标路径
    /// </summary>
    internal bool IsCustomTool => IsStamp || IsShape || IsSelect || IsAreaErase;

    internal bool WantsPreemptiveCapture => IsAreaErase || IsStamp;

    internal bool IsStampSingleFinger(TouchState state)
    {
        return IsStamp && state is TouchState.EvalDraw or TouchState.Draw;
    }
}

/// <summary>
/// 触摸状态的状态级元数据：每项回答一个独立问题，事件答复与编辑模式覆盖均由本表推导
/// </summary>
/// <param name="BlocksNativeInput">事件全程接管（Down/Move/Up 均 Handled），原生管线不接管收笔与选区移动</param>
/// <param name="OverridesEditing">持有画布级手势：进入时把原生编辑模式压为 None，退出时恢复</param>
/// <param name="PalmErase">掌擦态：触点包络作为橡皮实时擦除</param>
/// <param name="AreaEraseHost">面积擦宿主：EraseArea 工具在这些绘制上下文中持续擦除</param>
internal readonly record struct StateTraits(
    bool BlocksNativeInput = false,
    bool OverridesEditing = false,
    bool PalmErase = false,
    bool AreaEraseHost = false
);

public partial class InkCanvasNext
{
    /// <summary>
    /// 盖章武装标记：本次触摸序列在盖章模式下以单指 EvalDraw 开始，且尚未升级为任何手势接管态
    /// </summary>
    private bool stampArmed;

    internal TouchState State { get; private set; } = TouchState.Idle;

    /// <summary>
    /// 是否有输入序列在途：触摸或合成触点使状态离开 Idle，或原生墨迹画布持有笔捕获
    /// </summary>
    public bool IsGestureActive => State != TouchState.Idle || InnerCanvas.IsStylusCaptured;

    /// <summary>
    /// 当前工具快照，工具差异判定的唯一读取入口
    /// </summary>
    internal ToolSnapshot Tool => new(Mode, StampAction, IsReadOnly);

    private static StateTraits Traits(TouchState state)
    {
        return state switch
        {
            TouchState.EvalPan or TouchState.PanZoom or TouchState.Pan => new(BlocksNativeInput: true, OverridesEditing: true),
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
    /// 触点数变化（Down/Up）重评迁移，EvalDraw/EvalPan 在 Move 时重评以跟踪位移提交；
    /// 守卫内联写死：switch 臂自上而下即优先级（见 docs/TouchStates2.md）；
    /// 掌擦触点数构造期固定；d2/x2 每次重评只计算一次；全部不命中 → 保持当前状态。
    /// 触点坐标取自 GetTouchPoint(this)（根可视空间：WPF 只按 relativeTo 的祖先链做变换），
    /// 不经过画布 LayoutTransform，因此阈值（视口 DIP）不随缩放变化
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void UpdateState( )
    {
        var count = touches.Count;
        var d2 = GetMaxDistance2( );
        var l2 = options.TouchSpacingThreshold2;
        var c2 = options.TouchDisplacementThreshold2;
        var x2 = Get1stFingerDispl2( );
        var tool = Tool;
        var palm = !tool.IsReadOnly && count >= options.PalmEraseFingers;

        var newState = State switch
        {
            TouchState.Idle => count switch
            {
                0 => TouchState.Idle,
                // 选区入口是高优先级候选：仅 count in {1,2} 时评估（3+ 指不误入）
                1 or 2 when tool.IsSelectionEntry => TouchState.Selection,
                1 => TouchState.EvalDraw,
                2 when d2 <= l2 => TouchState.PanZoom,
                >= 3 when palm && d2 <= l2 => TouchState.Eraser,
                >= 3 when d2 <= l2 => TouchState.EvalPan,
                >= 2 when d2 > l2 && tool.AllowsMultiDraw => TouchState.MultiDraw,
                _ => State,
            },

            TouchState.EvalDraw => count switch
            {
                0 => TouchState.Idle,
                1 when x2 > c2 => TouchState.Draw,
                // 擦除工具多指手势与普通模式同款（并拢判定），差异：掌擦触点数起无论间距均掌擦（手掌压屏常呈张开），
                // 多指张开不迁多指画笔、保持擦除上下文
                >= 3 when palm && (d2 <= l2 || tool.IsErase) => TouchState.Eraser,
                2 when d2 <= l2 => TouchState.PanZoom,
                >= 3 when d2 <= l2 => TouchState.EvalPan,
                >= 2 when d2 > l2 && tool.AllowsMultiDraw => TouchState.MultiDraw,
                _ => State,
            },

            // 绘制提交后不再迁掌擦：掌擦只能发生在评估期
            TouchState.Draw => count switch
            {
                0 => TouchState.Idle,
                >= 2 when tool.AllowsMultiDraw => TouchState.MultiDraw,
                _ => State,
            },

            // 平移评估期：平移随 Move 实时生效，位移超过阈值前可迁掌擦与多指画笔
            TouchState.EvalPan => count switch
            {
                0 => TouchState.Idle,
                >= 3 when palm && (d2 <= l2 || tool.IsErase) => TouchState.Eraser,
                >= 2 when d2 > l2 && tool.AllowsMultiDraw => TouchState.MultiDraw,
                _ when x2 > c2 => TouchState.Pan,
                _ => State,
            },

            TouchState.MultiDraw => count switch
            {
                0 => TouchState.Idle,
                _ => State,
            },

            // 双指手势按前两指执行：并拢的附加指迁平移评估，张开的附加指不参与（不迁多指书写）
            TouchState.PanZoom => count switch
            {
                0 => TouchState.Idle,
                1 => TouchState.EvalPan,
                >= 3 when d2 <= l2 => TouchState.EvalPan,
                _ => State,
            },

            // 平移提交后锁定：无论触点如何变化，仅触点清空迁回 Idle
            TouchState.Pan => count switch
            {
                0 => TouchState.Idle,
                _ => State,
            },

            TouchState.Eraser => count switch
            {
                0 => TouchState.Idle,
                // 擦除工具多指全程掌擦，不迁多指画笔
                _ when count > options.PalmEraseFingers && d2 > l2 && tool.AllowsMultiDraw => TouchState.MultiDraw,
                _ => State,
            },

            TouchState.Selection => count switch
            {
                0 => TouchState.Idle,
                // 选区手势中附加手指：并拢平移画布、达到掌擦触点数切换为掌擦（与 Idle 入口优先级一致）
                >= 3 when palm && d2 <= l2 => TouchState.Eraser,
                >= 3 when d2 <= l2 => TouchState.EvalPan,
                _ => TouchState.Selection,
            },

            _ => State,
        };

        SetState(newState);
    }

    /// <summary>
    /// 状态切换：先按迁出状态结算（提交/取消/补余量），再按迁入状态建立（捕获/基线/编辑模式）/ State 在两侧结算完成后才更新，迁入规则据此仍可读到迁出状态
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
        if (from is TouchState.EvalPan or TouchState.Pan or TouchState.PanZoom
            && to is not (TouchState.EvalPan or TouchState.Pan or TouchState.PanZoom))
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

        // 盖章只由未升级为手势的单指序列触发：迁入任何手势接管态（含 MultiDraw）即解除 解除不依赖盖章仍激活，避免中途退出盖章后残留脏标记
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

            case TouchState.EvalPan:
            case TouchState.PanZoom:
            case TouchState.Pan:
                if (from is not (TouchState.EvalPan or TouchState.PanZoom or TouchState.Pan))
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
