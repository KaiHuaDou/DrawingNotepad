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

    public const bool EnabledDefault = true;

    public const int PointsDefault = 8;

    public const int PointsMin = 2;

    public const int PointsMax = 40;

    public const bool WetDefault = true;
}

// 末端按二次曲线收细，宽度由压力表达
// WPF 把压力 p 换算为宽度倍率 1.5p + 0.25，压力 0 仍有四分之一宽度
internal static class Taper
{
    /// <summary>
    /// 受压力映射下限约束，再小只能自绘几何
    /// </summary>
    internal const double TipRatio = 0.25;

    internal static TaperParameters? Effective(
        TaperParameters? options,
        DrawingAttributes attributes,
        bool continues = false)
    {
        return options is { Enabled: true } && !attributes.IgnorePressure && !continues ? options : null;
    }

    internal static int Tail(int count, TaperParameters? taper)
    {
        return Math.Min(taper?.Points ?? 0, count - 1);
    }

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
    /// 按 WPF 宽度倍率反解压力
    /// </summary>
    internal static float Pressure(float pressure, double ratio)
    {
        var scaled = (ratio * (1.5 * pressure + 0.25) - 0.25) / 1.5;

        return (float) Math.Clamp(scaled, 0, 1);
    }

    internal static StylusPointCollection CopyRange(
        IReadOnlyList<StylusPoint> points,
        int start,
        int end,
        TaperParameters? taper)
    {
        var copy = new StylusPointCollection(points[start].Description, end - start);
        // 尾部范围按整笔点数计算，与收笔后的干墨一致
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
