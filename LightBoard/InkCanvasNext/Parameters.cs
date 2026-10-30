using System.Windows;
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

    // ---------- 掌擦 ----------

    /// <summary>
    /// 掌擦触点数的可选范围与默认值
    /// </summary>
    public const int PalmEraseFingersMin = 4;
    public const int PalmEraseFingersMax = 5;
    public const int PalmEraseFingersDefault = PalmEraseFingersMax;

    /// <summary>
    /// 触点数量达到此值即掌擦，构造期固定
    /// </summary>
    internal readonly int palmEraseFingers;

    // ---------- 橡皮 ----------

    /// <summary>
    /// 面积擦直径默认值，视口 DIP
    /// </summary>
    public const double EraserDiameterDefault = 75;

    /// <summary>
    /// 掌擦圆在触点包络直径之外的外扩量默认值，视口 DIP
    /// </summary>
    public const double EraserPalmExtraDefault = 8;

    /// <summary>
    /// 缩放后逻辑直径的下限默认值，内容 DIP
    /// </summary>
    public const double EraserMinLogicalDiameterDefault = 1;

    /// <summary>
    /// 命中测试器重建阈值默认值，内容 DIP，逻辑直径变化超过此值才重建
    /// </summary>
    public const double EraserRebuildThresholdDefault = 0.5;

    /// <summary>
    /// 掌擦圆外扩量，构造期固定，视口 DIP
    /// </summary>
    internal readonly double eraserPalmExtra;

    // ---------- 边缘墨迹滚动提示 ----------

    /// <summary>
    /// 视口 DIP，墨迹进入距该边缘此宽度的热区即显示提示按钮
    /// </summary>
    public const double EdgeScrollTopDefaultThreshold = 64;
    public const double EdgeScrollBottomDefaultThreshold = 192;
    public const double EdgeScrollLeftDefaultThreshold = 64;
    public const double EdgeScrollRightDefaultThreshold = 96;

    /// <summary>
    /// 提示按钮距视口四边的间距默认值，视口 DIP
    /// </summary>
    public const double EdgeHintMarginDefault = 8;

    private double edgeScrollTopThreshold = EdgeScrollTopDefaultThreshold;
    private double edgeScrollBottomThreshold = EdgeScrollBottomDefaultThreshold;
    private double edgeScrollLeftThreshold = EdgeScrollLeftDefaultThreshold;
    private double edgeScrollRightThreshold = EdgeScrollRightDefaultThreshold;
    private Thickness edgeHintMargin = new(EdgeHintMarginDefault);

    /// <summary>
    /// 提示按钮距视口四边的间距，视口 DIP；宿主用它在工具栏等常驻界面上方留出位置。
    /// </summary>
    public Thickness EdgeHintMargin
    {
        get => edgeHintMargin;
        set
        {
            if (value == edgeHintMargin)
            {
                return;
            }

            edgeHintMargin = value;
            UpdateEdgeHints( );
        }
    }

    /// <summary>
    /// 置 0 关闭该侧提示；写入立即刷新按钮。
    /// </summary>
    public double EdgeScrollTopThreshold
    {
        get => edgeScrollTopThreshold;
        set => SetEdgeScrollThreshold(ref edgeScrollTopThreshold, value);
    }

    /// <summary>
    /// 置 0 关闭该侧提示；写入立即刷新按钮。
    /// </summary>
    public double EdgeScrollBottomThreshold
    {
        get => edgeScrollBottomThreshold;
        set => SetEdgeScrollThreshold(ref edgeScrollBottomThreshold, value);
    }

    /// <summary>
    /// 置 0 关闭该侧提示；写入立即刷新按钮。
    /// </summary>
    public double EdgeScrollLeftThreshold
    {
        get => edgeScrollLeftThreshold;
        set => SetEdgeScrollThreshold(ref edgeScrollLeftThreshold, value);
    }

    /// <summary>
    /// 置 0 关闭该侧提示；写入立即刷新按钮。
    /// </summary>
    public double EdgeScrollRightThreshold
    {
        get => edgeScrollRightThreshold;
        set => SetEdgeScrollThreshold(ref edgeScrollRightThreshold, value);
    }

    private void SetEdgeScrollThreshold(ref double field, double value)
    {
        if (value == field)
        {
            return;
        }

        field = value;
        UpdateEdgeHints( );
    }

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
