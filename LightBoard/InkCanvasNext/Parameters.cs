using System.Windows.Media;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    /// <summary>
    /// 构造期选项，取值已由选项类型收拢
    /// </summary>
    internal readonly InkCanvasNextOptions options;

    // ---------- 画布与视图 ----------

    public const double MinScale = 0.1;
    public const double MaxScale = 10.0;
    public const double WheelZoomFactor = 1.1;

    /// <summary>
    /// 内容 DIP，乘 Scale 后为视口位移
    /// </summary>
    public const double ScrollStep = 25;

    // ---------- 手势 ----------

    // Smooth 缩放曲线：|k - 1| ≤ T 时输出 1，≥ Q 时原样输出，S 为过渡段指数
    public const double T = 0.2;
    public const double Q = 0.5;
    public const double S = 1;

    public const float DefaultPressure = 0.5f;

    // ---------- 选区 ----------

    /// <summary>
    /// 视口 DIP，使用时除以缩放转内容
    /// </summary>
    public const double HandleHitRadius = 16;

    /// <summary>
    /// 视口 DIP，使用时除以缩放转内容
    /// </summary>
    public const double RotateHitScreenRadius = 24;

    /// <summary>
    /// 视口 DIP^2，使用时除以缩放平方转内容
    /// </summary>
    public const double LassoPointDistance2 = 16;

    /// <summary>
    /// 笔画点落入套索的百分比阈值（0~100）
    /// </summary>
    public const int LassoHitPercentage = 50;

    /// <summary>
    /// 手柄单轴缩放因子下限（相对手势起点，无量纲），低于即停，防止缩成一点或翻转到锚点另一侧
    /// </summary>
    public const double MinSelectionScale = 0.05;

    // ---------- 形状 ----------

    /// <summary>
    /// 内容 DIP
    /// </summary>
    public const double ShapeMinStrokeWidth = 1.0;

    /// <summary>
    /// 内容 DIP
    /// </summary>
    public const double ShapeCommitMinDistance = 4;
    public const int CircleSegments = 64;

    /// <summary>
    /// 内容 DIP
    /// </summary>
    public const double CircleMinRadius = 1;
}

internal sealed partial class SelectionVisual
{
    internal const double HandleRadius = 5; // 视口 DIP，绘制时除以缩放转内容

    internal const double RotateScreenRadius = 8; // 视口 DIP
    internal const double RotateGapAboveSelection = 32; // 视口 DIP，使用时除以缩放转内容

    internal const double HaloWidthFactor = 2.4;
    internal const byte HaloAlpha = 120; // 0~255
    internal const byte BorderAlpha = 235; // 0~255
    internal const double BorderWidth = 2.0; // 视口 DIP，绘制时除以缩放转内容
    internal const double LassoWidth = 2.0; // 视口 DIP，绘制时除以缩放转内容
    internal static readonly double[] LassoDashPattern = [4, 3]; // 视口 DIP，绘制时除以缩放转内容

    private static readonly Color AccentColor = Color.FromRgb(0x4C, 0x8B, 0xF5);

    private static readonly SolidColorBrush HaloBrush = CreateAccentBrush(HaloAlpha);
    private static readonly SolidColorBrush BorderBrush = CreateAccentBrush(BorderAlpha);
    private static readonly SolidColorBrush LassoBrush = CreateAccentBrush(0xFF);

    private static SolidColorBrush CreateAccentBrush(byte alpha)
    {
        var brush = new SolidColorBrush(Color.FromArgb(alpha, AccentColor.R, AccentColor.G, AccentColor.B));
        brush.Freeze( );
        return brush;
    }
}

internal sealed partial class StrokeVisual
{
    internal const double HighlighterOpacity = 0.5;
}
