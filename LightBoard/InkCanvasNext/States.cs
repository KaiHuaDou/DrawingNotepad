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
    internal ToolSnapshot Snapshot => new(DrawingAttributes.Mode, DrawingAttributes.Stamp, IsReadOnly);

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
        return Traits(state).BlocksNativeInput || IsAreaEraserActive(state) || Snapshot.IsStampSingleFinger(state);
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
        var tool = Snapshot;
        var palm = !tool.IsReadOnly && count >= options.PalmEraseFingers;
        var (selectionAllInside, selectionMixed) = tool.IsSelectionEntry && count >= 2
            ? ClassifyTouchesAgainstSelection( )
            : (false, false);
        var selectionAllOutside = !selectionAllInside && !selectionMixed;

        var newState = State switch
        {
            TouchState.Idle => count switch
            {
                0 => TouchState.Idle,
                // 选区入口：无选区时只有单指进入，其余按一般手势；
                // 有选区时单指、双指全在内、以及横跨边界的组合进入，横跨时由在内的第一根手指拖动
                1 when tool.IsSelectionEntry => TouchState.Selection,
                >= 2 when tool.IsSelectionEntry && selectionMixed => TouchState.Selection,
                2 when tool.IsSelectionEntry && selectionAllInside => TouchState.Selection,
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

            // 掌擦一旦开始就保持到触点清空：附加触点不改变擦除上下文，橡皮几何只取前 PalmEraseFingers 个触点
            TouchState.Eraser => count switch
            {
                0 => TouchState.Idle,
                _ => State,
            },

            TouchState.Selection => count switch
            {
                0 => TouchState.Idle,
                // 选区手势中附加手指：全在选区内保持选区手势（RefreshSelectionGesture 把 3+ 指降为只平移），
                // 横跨边界时由在内的第一根手指继续拖动，全在选区外按一般手势处理
                >= 3 when selectionMixed => TouchState.Selection,
                >= 3 when selectionAllOutside && palm && d2 <= l2 => TouchState.Eraser,
                >= 3 when selectionAllOutside && d2 <= l2 => TouchState.EvalPan,
                2 when selectionAllOutside && d2 <= l2 && IsPathGesture => TouchState.PanZoom,
                >= 2 when selectionAllOutside && d2 > l2 && tool.AllowsMultiDraw => TouchState.MultiDraw,
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
        // 离开平移组：把手势期的 RenderTransform 增量提交回滚动偏移，后续状态继续按已提交视图工作
        if (from is TouchState.EvalPan or TouchState.PanZoom or TouchState.Pan
            && to is not (TouchState.EvalPan or TouchState.PanZoom or TouchState.Pan))
        {
            CommitPan( );
        }

        if (from == TouchState.MultiDraw)
        {
            EndMultiTouch( );
        }

        if (from == TouchState.Selection)
        {
            // 半途的套索被多指手势接管：放弃本次路径，保留进入前的选区
            if (to != TouchState.Idle && IsPathGesture)
            {
                ResetSelectionGesture( );
                selectionVisual.ClearLasso( );
            }

            EndSelectionTouch( );
        }

        // 擦除周期先于编辑模式恢复结算：周期结束的回调可能换工具，恢复必须按新工具执行
        if (IsAreaEraserActive(from) && !IsAreaEraserActive(to))
        {
            EndEraserCycle( );
        }

        // 离开覆盖编辑模式的手势态：把编辑模式恢复为当前工具；回 Idle 时同时释放捕获
        if (to == TouchState.Idle)
        {
            ReleaseAll( );
            ApplyModeToEditing(DrawingAttributes.Mode);

            // 手势期间不补余量也不刷新提示：结束时统一补一屏余量并刷新一次
            EnsureCanvasFill(CurrentView);
            UpdateEdgeHints( );
        }
        else if (Traits(from).OverridesEditing && !Traits(to).OverridesEditing)
        {
            ApplyModeToEditing(DrawingAttributes.Mode);
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
    }

    private void EnterState(TouchState to, TouchState from)
    {
        // 盖章只由未升级为手势的单指序列触发：Idle 进入 EvalDraw 时武装
        if (from == TouchState.Idle && to == TouchState.EvalDraw && Snapshot.IsStamp)
        {
            stampArmed = true;
        }

        switch (to)
        {
            case TouchState.EvalDraw:
                if (Snapshot.WantsPreemptiveCapture)
                {
                    CaptureAll( );
                }

                break;

            case TouchState.Draw:
                // (MultiDraw, Draw) 无需捕获：多指手势期间触点已捕获
                if (from == TouchState.EvalDraw && Snapshot.WantsPreemptiveCapture)
                {
                    CaptureAll( );
                }

                break;

            case TouchState.Selection:
                InnerCanvas.ClearWet( );
                CaptureAll( );
                BeginSelectionTouch( );
                break;

            case TouchState.EvalPan:
            case TouchState.PanZoom:
            case TouchState.Pan:
                // EvalPan 可能已用手势变换平移；进入双指缩放前先折回偏移，避免两套平移叠加
                if (from == TouchState.EvalPan && to == TouchState.PanZoom)
                {
                    CommitPan( );
                }

                if (from is not (TouchState.EvalPan or TouchState.PanZoom or TouchState.Pan))
                {
                    ReleaseAll( );
                    InnerCanvas.EditingMode = InkCanvasEditingMode.None;
                    InnerCanvas.ClearWet( );
                    CaptureAll( );

                    // 手势开始补一屏余量，手势中不扩展，靠这次余量吸收拖动
                    EnsureCanvasFill(CurrentView);
                }

                InitGesture( );
                break;

            case TouchState.Eraser:
                ReleaseAll( );
                InnerCanvas.EditingMode = InkCanvasEditingMode.None;
                InnerCanvas.ClearWet( );
                CaptureAll( );
                break;

            case TouchState.MultiDraw:
                HandOffDrawingStroke( );
                ReleaseAll( );
                InnerCanvas.EditingMode = InkCanvasEditingMode.None;
                InnerCanvas.ClearWet( );
                CaptureAll( );
                StartMultiTouch( );
                break;
        }
    }
}
