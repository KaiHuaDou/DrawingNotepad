namespace InkCanvasNext;

/// <summary>
/// InkCanvasNext 选项：运行期不可变；改动需重建画布
/// </summary>
public sealed record InkCanvasNextOptions
{
    /// <summary>
    /// 触点数量达到此值时掌擦，取值 4 或 5
    /// </summary>
    public int PalmEraseFingers { get; init; } = InkCanvasNext.PalmEraseFingersDefault;

    /// <summary>
    /// 橡皮参数
    /// </summary>
    public EraserParameters Eraser { get; init; } = new( );

    /// <summary>
    /// 单指起笔位移阈值，视口 DIP
    /// </summary>
    public double TouchDisplacementThreshold { get; init; } = InkCanvasNext.TouchDisplacementThresholdDefault;

    /// <summary>
    /// 双指平移锁定阈值，视口 DIP
    /// </summary>
    public double PanZoomDisplaceThreshold { get; init; } = InkCanvasNext.PanZoomDisplaceThresholdDefault;

    /// <summary>
    /// 双指捏合锁定间距，视口 DIP
    /// </summary>
    public double PinchLockDistance { get; init; } = InkCanvasNext.PinchLockDistanceDefault;

    /// <summary>
    /// 触点间距阈值，视口 DIP；NaN 表示按主屏工作区宽度派生
    /// </summary>
    public double TouchSpacingThreshold { get; init; } = double.NaN;

    /// <summary>
    /// 上侧热区宽度，视口 DIP；0 关闭该侧提示
    /// </summary>
    public double EdgeScrollTopThreshold { get; init; } = InkCanvasNext.EdgeScrollTopDefaultThreshold;

    /// <summary>
    /// 下侧热区宽度，视口 DIP；0 关闭该侧提示
    /// </summary>
    public double EdgeScrollBottomThreshold { get; init; } = InkCanvasNext.EdgeScrollBottomDefaultThreshold;

    /// <summary>
    /// 左侧热区宽度，视口 DIP；0 关闭该侧提示
    /// </summary>
    public double EdgeScrollLeftThreshold { get; init; } = InkCanvasNext.EdgeScrollLeftDefaultThreshold;

    /// <summary>
    /// 右侧热区宽度，视口 DIP；0 关闭该侧提示
    /// </summary>
    public double EdgeScrollRightThreshold { get; init; } = InkCanvasNext.EdgeScrollRightDefaultThreshold;

    /// <summary>
    /// 撤销历史容量；0 表示不记录历史
    /// </summary>
    public int MaxHistoryCount { get; init; } = InkCanvasNext.MaxHistoryCount;

    /// <summary>
    /// 鼠标滚轮行为
    /// </summary>
    public MouseWheelAction WheelAction { get; init; } = MouseWheelAction.Scroll;
}

/// <summary>
/// 橡皮参数
/// </summary>
public sealed record EraserParameters
{
    /// <summary>
    /// 面积擦直径，视口 DIP
    /// </summary>
    public double Diameter { get; init; } = InkCanvasNext.EraserDiameterDefault;

    /// <summary>
    /// 掌擦圆在触点包络直径之外的外扩量，视口 DIP
    /// </summary>
    public double PalmExtra { get; init; } = InkCanvasNext.EraserPalmExtraDefault;

    /// <summary>
    /// 缩放后逻辑直径的下限，内容 DIP
    /// </summary>
    public double MinLogicalDiameter { get; init; } = InkCanvasNext.EraserMinLogicalDiameterDefault;

    /// <summary>
    /// 逻辑直径变化超过此值才重建命中测试器，内容 DIP
    /// </summary>
    public double RebuildThreshold { get; init; } = InkCanvasNext.EraserRebuildThresholdDefault;
}
