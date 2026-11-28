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

public partial class InkCanvasNext
{
    /// <summary>
    /// 盖章武装标记：当前触摸序列在盖章模式下以单指 EvalDraw 开始，且尚未升级为手势接管状态
    /// </summary>
    private bool stampArmed;

    internal TouchState State { get; private set; } = TouchState.Idle;

    /// <summary>
    /// 内容平移会改动手势基线，只在没有触摸序列时执行
    /// </summary>
    internal bool CanRebase => State == TouchState.Idle;

    /// <summary>
    /// 是否有输入序列在途：状态离开 Idle，或原生墨迹画布持有笔捕获
    /// </summary>
    public bool IsGestureActive => State != TouchState.Idle || InnerCanvas.IsStylusCaptured;

    /// <summary>
    /// 当前工具快照，工具差异判定的唯一读取入口
    /// </summary>
    internal ToolSnapshot Snapshot => new(DrawingAttributes.Mode, DrawingAttributes.Stamp, IsReadOnly);

    /// <summary>
    /// 触摸状态迁移入口：switch 臂自上而下即优先级（见 docs/TouchStates2.md），
    /// 触点坐标取根可视空间，阈值不随缩放变化
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void UpdateState( )
    {
        var (count, palmCount) = CountTouches(touches);

        // 面积通道在入口统一处理，下面的 switch 只处理手指数通道
        if (IsPalmTakeover(State, count, palmCount))
        {
            SetState(TouchState.Eraser);
            return;
        }

        var d2 = GetMaxDistance2(touches, count);
        var l2 = options.TouchSpacingThreshold2;
        var c2 = options.TouchDisplacementThreshold2;
        var x2 = Get1stFingerDispl2(touches, touchStarts);
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
                // 手掌压屏常呈张开，掌擦不要求并拢；擦除工具下多指一律掌擦
                >= 3 when palm && (d2 <= l2 || tool.IsErase) => TouchState.Eraser,
                2 when d2 <= l2 => TouchState.PanZoom,
                >= 3 when d2 <= l2 => TouchState.EvalPan,
                >= 2 when d2 > l2 && tool.AllowsMultiDraw => TouchState.MultiDraw,
                _ => State,
            },

            TouchState.Draw => count switch
            {
                0 => TouchState.Idle,
                >= 2 when tool.AllowsMultiDraw => TouchState.MultiDraw,
                _ => State,
            },

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

            TouchState.PanZoom => count switch
            {
                0 => TouchState.Idle,
                1 => TouchState.EvalPan,
                >= 3 when d2 <= l2 => TouchState.EvalPan,
                _ => State,
            },

            TouchState.Pan => count switch
            {
                0 => TouchState.Idle,
                _ => State,
            },

            // 面积几何与调整窗口由 UpdateAreaEraser 按手掌簇变化重启
            TouchState.Eraser => touches.Count == 0
                ? TouchState.Idle
                : State,

            TouchState.Selection => count switch
            {
                // 选区内的手掌不在此接管，回 Idle 后由手掌移动重评
                0 => TouchState.Idle,
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
    /// 迁出结算与迁入建立完成后才更新 State，迁入钩子仍可读到迁出状态
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
            // 套索被手势接管时放弃路径，保留进入前的选区
            if (to != TouchState.Idle && IsPathGesture)
            {
                ResetSelectionGesture( );
                selectionVisual.ClearLasso( );
            }

            EndSelectionTouch( );
        }

        // 擦除周期先于编辑模式恢复结算：周期结束的回调可能换工具，恢复须按新工具执行
        if (IsAreaEraserActive(from) && !IsAreaEraserActive(to))
        {
            EndEraserCycle( );
        }

        if (from == TouchState.Eraser)
        {
            ResetPalmEraseLifetime( );
        }

        if (to == TouchState.Idle)
        {
            ReleaseAll( );
            ApplyModeToEditing(DrawingAttributes.Mode);

            // 手势期间不补余量也不刷新提示，结束时统一结算平移、补一屏余量并刷新一次
            ApplyView(CurrentView.Shifted(ApplyPendingRebase( )));

            UpdateEdgeHints( );
        }
        else if (TraitsOf(from).OverridesEditing && !TraitsOf(to).OverridesEditing)
        {
            ApplyModeToEditing(DrawingAttributes.Mode);
        }

        // 形状只允许在单指绘制上下文存活，否则 shapeActive 会在事件处理器中抢占手势路由；
        // 迁入 MultiDraw 时按预览提交，其余迁移放弃预览
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

        // 盖章只由未升级为手势的单指序列触发，进入手势接管状态即解除，避免中途退出盖章后残留标记
        if (to is not (TouchState.EvalDraw or TouchState.Draw))
        {
            stampArmed = false;
        }
    }

    private void EnterState(TouchState to, TouchState from)
    {
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
                if (from is not (TouchState.EvalPan or TouchState.PanZoom or TouchState.Pan))
                {
                    ReleaseAll( );
                    InnerCanvas.EditingMode = InkCanvasEditingMode.None;
                    InnerCanvas.ClearWet( );
                    CaptureAll( );

                    // 从组外进入才算新平移：累计位移清零，组内迁移与基线重立不清空
                    PanDisplacement = default;

                    // 手势开始补一屏余量，手势中不扩展，靠这次余量吸收拖动；
                    // 左/上已有空闲期补足的余量，这里只补右/下，避免起手前再平移内容
                    ApplyView(CurrentView, prefetchLeftTop: false);
                }

                InitGesture( );
                break;

            case TouchState.Eraser:
                HandOffDrawingStroke( );
                BeginPalmEraseLifetime( );
                ReleaseAll( );
                InnerCanvas.EditingMode = InkCanvasEditingMode.None;
                InnerCanvas.ClearWet( );
                CaptureAll( );
                break;

            case TouchState.MultiDraw:
                // 原绘制指在多指笔画里从触点续接，收笔处不渐细
                HandOffDrawingStroke(continues: true);
                ReleaseAll( );
                InnerCanvas.EditingMode = InkCanvasEditingMode.None;
                InnerCanvas.ClearWet( );
                CaptureAll( );
                StartMultiTouch( );
                break;
        }
    }
}
