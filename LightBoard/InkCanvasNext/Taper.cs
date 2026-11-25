using System;

using System.Collections.Generic;
using System.Windows.Ink;
using System.Windows.Input;

namespace InkCanvasNext;

/// <summary>
/// 笔锋参数
/// </summary>
public sealed record TaperParameters
{
    /// <summary>
    /// 是否启用笔画末端渐细
    /// </summary>
    public bool Enabled { get; init; } = EnabledDefault;

    /// <summary>
    /// 末端渐细的采样点数，取值 2~40，越界取边界
    /// </summary>
    public int Points
    {
        get;
        init => field = Math.Clamp(value, PointsMin, PointsMax);
    } = PointsDefault;

    /// <summary>
    /// 湿墨阶段是否显示渐细；关闭后湿墨按无笔锋绘制
    /// </summary>
    public bool Wet { get; init; } = WetDefault;

    /// <summary>
    /// 笔锋开关默认值
    /// </summary>
    public const bool EnabledDefault = true;

    /// <summary>
    /// 渐细点数默认值
    /// </summary>
    public const int PointsDefault = 8;

    /// <summary>
    /// 渐细点数下限
    /// </summary>
    public const int PointsMin = 2;

    /// <summary>
    /// 渐细点数上限
    /// </summary>
    public const int PointsMax = 40;

    /// <summary>
    /// 湿墨渐细开关默认值
    /// </summary>
    public const bool WetDefault = true;
}

// 笔锋：笔画末端按二次曲线收细，宽度用采样点压力表达
// WPF 把压力 p 换算为宽度倍率 1.5p + 0.25，压力取 0 时仍有四分之一宽度
internal static class Taper
{
    /// <summary>
    /// 末端宽度占全宽的比例；受压力映射下限约束，再小只能自绘几何
    /// </summary>
    internal const double TipRatio = 0.25;

    /// <summary>
    /// 返回生效的笔锋参数；未启用、笔画忽略压力或末端由后续笔画续接时为 null
    /// </summary>
    internal static TaperParameters? Effective(
        TaperParameters? options,
        DrawingAttributes attributes,
        bool continues = false)
    {
        return options is { Enabled: true } && !attributes.IgnorePressure && !continues ? options : null;
    }

    /// <summary>
    /// 渐细点数：至少保留一个全宽点，taper 为 null 或点数不足时为 0
    /// </summary>
    internal static int Tail(int count, TaperParameters? taper)
    {
        return Math.Min(taper?.Points ?? 0, count - 1);
    }

    /// <summary>
    /// 尾部第 k 个点的宽度比例；k = 0 是尾部起点，k 越大越细
    /// </summary>
    internal static double Ratio(int k, int tail)
    {
        if (tail < 2 || k <= 0)
        {
            return 1;
        }

        var t = (double) k / (tail - 1);

        return 1 - (1 - TipRatio) * t * t;
    }

    /// <summary>
    /// 反解压力使宽度倍率缩放到指定比例；结果夹在 0~1
    /// </summary>
    internal static float Pressure(float pressure, double ratio)
    {
        var scaled = (ratio * (1.5 * pressure + 0.25) - 0.25) / 1.5;

        return (float) Math.Clamp(scaled, 0, 1);
    }

    /// <summary>
    /// 复制 [start, end) 区间的点，压力按整笔尾部改写；taper 为 null 时原样复制
    /// </summary>
    internal static StylusPointCollection CopyRange(
        IReadOnlyList<StylusPoint> points,
        int start,
        int end,
        TaperParameters? taper)
    {
        var copy = new StylusPointCollection(points[start].Description, end - start);
        var tail = Tail(points.Count, taper);
        var head = points.Count - tail;

        for (var i = start; i < end; i++)
        {
            var point = points[i];
            if (i >= head)
            {
                point.PressureFactor = Pressure(point.PressureFactor, Ratio(i - head, tail));
            }

            copy.Add(point);
        }

        return copy;
    }

    /// <summary>
    /// 改写一条笔画的末端压力；taper 为 null 或点数不足时不变
    /// </summary>
    internal static void Apply(Stroke stroke, TaperParameters? taper)
    {
        var points = stroke.StylusPoints;
        if (Tail(points.Count, taper) < 2)
        {
            return;
        }

        stroke.StylusPoints = CopyRange(points, 0, points.Count, taper);
    }
}
