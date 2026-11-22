using System.Runtime.CompilerServices;

namespace InkCanvasNext;

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
/// 触摸状态的状态级元数据：每项回答一个独立问题；编辑模式覆盖与大部分事件答复由本表推导，
/// 手掌触点（<see cref="TouchKind.Palm"/>）在普通绘制态另由 Kind 单独拦截（见 CanvasPreviewTouchDown）
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
    /// 当前状态的状态级元数据
    /// </summary>
    internal StateTraits Traits => TraitsOf(State);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static StateTraits TraitsOf(TouchState state)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool HandlesEvent(TouchState state)
    {
        return TraitsOf(state).BlocksNativeInput || IsAreaEraserActive(state) || Snapshot.IsStampSingleFinger(state);
    }
}
