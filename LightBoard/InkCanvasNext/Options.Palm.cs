using System;

namespace InkCanvasNext;

/// <summary>
/// 手掌面积参数：阈值按 CVTouch SDK 的 300 × 200 DIP，判据为长短边的严格大于。
/// 本文件只放「触点分类」参数；手掌擦除的直径与调整窗口属于橡皮行为，见 <see cref="EraserParameters"/>
/// </summary>
public sealed partial record InkCanvasNextOptions
{
    /// <summary>
    /// 手掌触点长边阈值，视口 DIP；构造时保证不小于 <see cref="PalmMinHeight"/>
    /// </summary>
    public double PalmMinWidth
    {
        get;
        init => field = ClampPalm(Math.Max(value, PalmMinHeight), PalmMinWidthMin, PalmMinWidthMax, PalmMinWidthDefault);
    } = PalmMinWidthDefault;

    /// <summary>
    /// 手掌触点短边阈值，视口 DIP；构造时保证不大于 <see cref="PalmMinWidth"/>
    /// </summary>
    public double PalmMinHeight
    {
        get;
        init => field = ClampPalm(Math.Min(value, PalmMinWidth), PalmMinHeightMin, PalmMinHeightMax, PalmMinHeightDefault);
    } = PalmMinHeightDefault;

    /// <summary>
    /// 手掌碎片聚类距离，视口 DIP
    /// </summary>
    public double PalmMergeDistance
    {
        get;
        init
        {
            field = ClampPalm(value, PalmMergeDistanceMin, PalmMergeDistanceMax, PalmMergeDistanceDefault);
            PalmMergeDistance2 = field * field;
        }
    } = PalmMergeDistanceDefault;

    /// <summary>
    /// <see cref="PalmMergeDistance"/> 的平方，逐事件判定的热点路径预计算
    /// </summary>
    internal double PalmMergeDistance2
    {
        get;
        private init;
    } = PalmMergeDistanceDefault * PalmMergeDistanceDefault;

    /// <summary>
    /// 多个手掌碎片的面积和阈值，等于长短边阈值之积
    /// </summary>
    internal double PalmClusterMinArea => PalmMinWidth * PalmMinHeight;

    /// <summary>
    /// 手掌长边阈值默认值，视口 DIP
    /// </summary>
    public const double PalmMinWidthDefault = 300;

    /// <summary>
    /// 手掌长边阈值下限，视口 DIP
    /// </summary>
    public const double PalmMinWidthMin = 240;

    /// <summary>
    /// 手掌长边阈值上限，视口 DIP
    /// </summary>
    public const double PalmMinWidthMax = 400;

    /// <summary>
    /// 手掌短边阈值默认值，视口 DIP
    /// </summary>
    public const double PalmMinHeightDefault = 200;

    /// <summary>
    /// 手掌短边阈值下限，视口 DIP
    /// </summary>
    public const double PalmMinHeightMin = 160;

    /// <summary>
    /// 手掌短边阈值上限，视口 DIP
    /// </summary>
    public const double PalmMinHeightMax = 300;

    /// <summary>
    /// 手掌碎片聚类距离默认值，视口 DIP
    /// </summary>
    public const double PalmMergeDistanceDefault = 120;

    /// <summary>
    /// 手掌碎片聚类距离下限，视口 DIP
    /// </summary>
    public const double PalmMergeDistanceMin = 60;

    /// <summary>
    /// 手掌碎片聚类距离上限，视口 DIP
    /// </summary>
    public const double PalmMergeDistanceMax = 200;

    private static double ClampPalm(double value, double min, double max, double fallback)
    {
        return double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
    }
}
