using System;

namespace InkCanvasNext;

/// <summary>
/// 橡皮参数
/// </summary>
public sealed record EraserParameters
{
    /// <summary>
    /// 面积擦直径，视口 DIP
    /// </summary>
    public double Diameter
    {
        get;
        init => field = Math.Clamp(value, DiameterMin, DiameterMax);
    } = DiameterDefault;

    /// <summary>
    /// 掌擦圆在触点包络直径之外的外扩量，视口 DIP
    /// </summary>
    public double PalmExtra
    {
        get;
        init => field = Math.Clamp(value, PalmExtraMin, PalmExtraMax);
    } = PalmExtraDefault;

    /// <summary>
    /// 缩放后逻辑直径的下限，内容 DIP
    /// </summary>
    public double MinLogicalDiameter
    {
        get;
        init => field = Math.Clamp(value, MinLogicalDiameterMin, MinLogicalDiameterMax);
    } = MinLogicalDiameterDefault;

    /// <summary>
    /// 逻辑直径变化超过此值才重建命中测试器，内容 DIP
    /// </summary>
    public double RebuildThreshold
    {
        get;
        init => field = Math.Clamp(value, RebuildThresholdMin, RebuildThresholdMax);
    } = RebuildThresholdDefault;

    /// <summary>
    /// 面积擦直径默认值，视口 DIP
    /// </summary>
    public const double DiameterDefault = 75;

    /// <summary>
    /// 面积擦直径下限，视口 DIP
    /// </summary>
    public const double DiameterMin = 1;

    /// <summary>
    /// 面积擦直径上限，视口 DIP
    /// </summary>
    public const double DiameterMax = 400;

    /// <summary>
    /// 掌擦外扩量默认值，视口 DIP
    /// </summary>
    public const double PalmExtraDefault = 8;

    /// <summary>
    /// 掌擦外扩量下限，视口 DIP
    /// </summary>
    public const double PalmExtraMin = 0;

    /// <summary>
    /// 掌擦外扩量上限，视口 DIP
    /// </summary>
    public const double PalmExtraMax = 100;

    /// <summary>
    /// 逻辑直径下限默认值，内容 DIP
    /// </summary>
    public const double MinLogicalDiameterDefault = 1;

    /// <summary>
    /// 逻辑直径下限的下限，内容 DIP
    /// </summary>
    public const double MinLogicalDiameterMin = 0.1;

    /// <summary>
    /// 逻辑直径下限的上限，内容 DIP
    /// </summary>
    public const double MinLogicalDiameterMax = 10;

    /// <summary>
    /// 命中测试器重建阈值默认值，内容 DIP
    /// </summary>
    public const double RebuildThresholdDefault = 0.5;

    /// <summary>
    /// 命中测试器重建阈值下限，内容 DIP
    /// </summary>
    public const double RebuildThresholdMin = 0.01;

    /// <summary>
    /// 命中测试器重建阈值上限，内容 DIP
    /// </summary>
    public const double RebuildThresholdMax = 10;
}
