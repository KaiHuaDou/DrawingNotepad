using System.Windows.Media;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    // ---------- 画布与视图 ----------

    public const double MinScale = 0.1;
    public const double MaxScale = 10.0;
    public const double WheelZoomFactor = 1.1;

    /// <summary>
    /// 内容 DIP，乘 Scale 后为视口位移
    /// </summary>
    public const double ScrollStep = 25;

    // ---------- 手势 ----------
    // 触点坐标统一取自 GetTouchPoint(this)（根可视空间：WPF 只按 relativeTo 的祖先链做变换），
    // 不经过画布 LayoutTransform，因此以下视口 DIP 阈值不随缩放变化。

#if DEBUG
    public const double DistanceThresholdFactor = 0.9;
#else
    public const double DistanceThresholdFactor = 0.1;
#endif

    /// <summary>
    /// 视口 DIP²，构造时按 DistanceThresholdFactor × 工作区宽度算出
    /// </summary>
    internal readonly double distanceThreshold2;

    /// <summary>
    /// 视口 DIP，单指 Move 位移超过此值即起笔（默认值）
    /// </summary>
    public const double TouchDisplacementThresholdDefault = 20;

    /// <summary>
    /// 视口 DIP，双指平移位移超过此值即锁定缩放（默认值）
    /// </summary>
    public const double PanZoomDisplaceThresholdDefault = 30;

    /// <summary>
    /// 视口 DIP，两指间距低于此值即锁定缩放（默认值）
    /// </summary>
    public const double PinchLockDistanceDefault = 24;

    /// <summary>
    /// 视口 DIP^2
    /// </summary>
    public double TouchDisplacementThreshold2 { get; set; } = TouchDisplacementThresholdDefault * TouchDisplacementThresholdDefault;

    /// <summary>
    /// 视口 DIP^2
    /// </summary>
    public double PanZoomDisplaceThreshold2 { get; set; } = PanZoomDisplaceThresholdDefault * PanZoomDisplaceThresholdDefault;

    /// <summary>
    /// 视口 DIP^2，两指间距低于此值即锁定缩放
    /// </summary>
    public double PinchLockDistance2 { get; set; } = PinchLockDistanceDefault * PinchLockDistanceDefault;

    // Smooth 缩放曲线：|k - 1| ≤ T 时输出 1，≥ Q 时原样输出，S 为过渡段指数
    public const double T = 0.2;
    public const double Q = 0.5;
    public const double S = 1;

    public const float DefaultPressure = 0.5f;

    // ---------- 边缘自动滚动 ----------

    /// <summary>
    /// 视口 DIP，抬手落点距上/下边缘小于此值即触发
    /// </summary>
    public const double AutoScrollVerticalDefaultThreshold = 96;

    /// <summary>
    /// 视口 DIP，抬手落点距左/右边缘小于此值即触发
    /// </summary>
    public const double AutoScrollHorizontalDefaultThreshold = 96;

    public double AutoScrollVerticalThreshold { get; set; } = AutoScrollVerticalDefaultThreshold; // 视口 DIP，抬手落点距上/下边缘小于此值即触发
    public double AutoScrollHorizontalThreshold { get; set; } = AutoScrollHorizontalDefaultThreshold; // 视口 DIP，抬手落点距左/右边缘小于此值即触发

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

    // ---------- 撤销 ----------

    /// <summary>
    /// 最大历史记录数量默认值
    /// </summary>
    public const int MaxHistoryCount = 200;

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
    internal const double ToolbarGapFromSelection = 8; // 视口 DIP

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

internal sealed partial class Eraser
{
    internal const double DefaultDiameter = 50.0; // 视口 DIP
    internal const double MinLogicalDiameter = 1.0; // 内容 DIP
    internal const double RebuildThreshold = 0.5; // 内容 DIP，逻辑直径变化超过此值才重建命中测试器
    internal const double EraserPalmExtra = 8; // 视口 DIP，掌擦圆在触点包络直径之外的外扩量
}

internal sealed partial class StrokeVisual
{
    internal const double HighlighterOpacity = 0.5;
}

internal static partial class StrokeCollectionExtension
{
    internal const int PreviewWidth = 300; // 位图像素
    internal const int PreviewHeight = PreviewWidth / 16 * 9;

    internal const double FallbackCanvasWidth = 1920; // 内容 DIP
    internal const double FallbackCanvasHeight = 1080;

    internal const double RenderPadding = 64; // 内容 DIP
    internal const double PreviewContentFill = 1;

    private static readonly SolidColorBrush Background = new(Color.FromRgb(0x1E, 0x1E, 0x1E));

    static StrokeCollectionExtension( )
    {
        Background.Freeze( );
    }
}
