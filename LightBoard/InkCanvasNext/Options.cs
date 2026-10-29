namespace InkCanvasNext;

/// <summary>
/// 画布构造期选项：创建时读入并写入实例的 readonly 字段，运行期不可变；改动需重建画布。
/// </summary>
public sealed record InkCanvasNextOptions
{
    /// <summary>
    /// 触点数量达到此值时掌擦，取值 4 或 5。
    /// </summary>
    public int PalmEraseFingers { get; init; } = InkCanvasNext.PalmEraseFingersDefault;

    /// <summary>
    /// 橡皮参数。
    /// </summary>
    public EraserParameters Eraser { get; init; } = new( );
}

/// <summary>
/// 橡皮参数。
/// </summary>
public sealed record EraserParameters
{
    /// <summary>
    /// 面积擦直径，视口 DIP。
    /// </summary>
    public double Diameter { get; init; } = InkCanvasNext.EraserDiameterDefault;

    /// <summary>
    /// 掌擦圆在触点包络直径之外的外扩量，视口 DIP。
    /// </summary>
    public double PalmExtra { get; init; } = InkCanvasNext.EraserPalmExtraDefault;

    /// <summary>
    /// 缩放后逻辑直径的下限，内容 DIP。
    /// </summary>
    public double MinLogicalDiameter { get; init; } = InkCanvasNext.EraserMinLogicalDiameterDefault;

    /// <summary>
    /// 逻辑直径变化超过此值才重建命中测试器，内容 DIP。
    /// </summary>
    public double RebuildThreshold { get; init; } = InkCanvasNext.EraserRebuildThresholdDefault;
}
