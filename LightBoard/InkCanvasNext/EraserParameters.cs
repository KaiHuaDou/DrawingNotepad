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
    /// 面积掌擦的基础直径，视口 DIP；实际直径按接触区域对角线分档放大
    /// </summary>
    public double PalmBaseDiameter
    {
        get;
        init => field = Math.Clamp(value, PalmBaseDiameterMin, PalmBaseDiameterMax);
    } = PalmBaseDiameterDefault;

    /// <summary>
    /// 面积掌擦的最小直径，视口 DIP
    /// </summary>
    public double PalmMinDiameter
    {
        get;
        init => field = Math.Clamp(value, PalmMinDiameterMin, PalmMinDiameterMax);
    } = PalmMinDiameterDefault;

    /// <summary>
    /// 擦除开始后允许按面积调整直径的时间，毫秒；此后直径保持首次取值
    /// </summary>
    public int PalmSizeAdjustDuration
    {
        get;
        init => field = Math.Clamp(value, PalmSizeAdjustDurationMin, PalmSizeAdjustDurationMax);
    } = PalmSizeAdjustDurationDefault;

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
    /// 面积掌擦基础直径默认值，视口 DIP
    /// </summary>
    public const double PalmBaseDiameterDefault = 75;

    /// <summary>
    /// 面积掌擦基础直径下限，视口 DIP
    /// </summary>
    public const double PalmBaseDiameterMin = 40;

    /// <summary>
    /// 面积掌擦基础直径上限，视口 DIP
    /// </summary>
    public const double PalmBaseDiameterMax = 150;

    /// <summary>
    /// 面积掌擦最小直径默认值，视口 DIP
    /// </summary>
    public const double PalmMinDiameterDefault = 120;

    /// <summary>
    /// 面积掌擦最小直径下限，视口 DIP
    /// </summary>
    public const double PalmMinDiameterMin = 40;

    /// <summary>
    /// 面积掌擦最小直径上限，视口 DIP
    /// </summary>
    public const double PalmMinDiameterMax = 300;

    /// <summary>
    /// 面积调整窗口默认值，毫秒
    /// </summary>
    public const int PalmSizeAdjustDurationDefault = 100;

    /// <summary>
    /// 面积调整窗口下限，毫秒
    /// </summary>
    public const int PalmSizeAdjustDurationMin = 0;

    /// <summary>
    /// 面积调整窗口上限，毫秒
    /// </summary>
    public const int PalmSizeAdjustDurationMax = 500;

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
