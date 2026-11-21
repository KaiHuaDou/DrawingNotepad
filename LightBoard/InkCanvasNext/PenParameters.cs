namespace InkCanvasNext;

/// <summary>
/// 手写笔选项
/// </summary>
public sealed record PenParameters
{
    /// <summary>
    /// 是否按设备压力改变粗细；关闭后笔触宽度固定
    /// </summary>
    public bool UseDevicePressure { get; init; } = UseDevicePressureDefault;

    /// <summary>
    /// 是否显示笔尖悬停圆环
    /// </summary>
    public bool HoverCursor { get; init; } = HoverCursorDefault;

    /// <summary>
    /// 笔杆按钮是否临时按笔擦除
    /// </summary>
    public bool BarrelErase { get; init; } = BarrelEraseDefault;

    /// <summary>
    /// 设备压力开关默认值
    /// </summary>
    public const bool UseDevicePressureDefault = true;

    /// <summary>
    /// 悬停圆环开关默认值
    /// </summary>
    public const bool HoverCursorDefault = true;

    /// <summary>
    /// 笔杆按钮开关默认值
    /// </summary>
    public const bool BarrelEraseDefault = true;
}
