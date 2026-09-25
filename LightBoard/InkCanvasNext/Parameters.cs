using System.Windows;
using System.Windows.Media;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    // ---------- 画布与视图 ----------

    internal const double MinScale = 0.1;
    internal const double MaxScale = 10.0;
    internal const double WheelZoomFactor = 1.1;
    internal const double ScrollStep = 25; // 内容 DIP，乘 Scale 后为视口位移

    // ---------- 手势 ----------

#if DEBUG
    internal const double DistanceThresholdFactor = 0.9;
#else
    internal const double DistanceThresholdFactor = 0.1;
#endif

    internal readonly double distanceThreshold2; // 视口 DIP²，构造时按 DistanceThresholdFactor × 工作区宽度算出

    internal const double TouchDisplacementThreshold2 = 20 * 20; // 视口 DIP²
    internal const double PanZoomDisplaceThreshold2 = 30 * 30; // 视口 DIP²
    internal const double PinchLockDistance2 = 24 * 24; // 视口 DIP²，两指间距低于此即锁定缩放

    // Smooth 缩放曲线：|k - 1| ≤ T 时输出 1，≥ Q 时原样输出，S 为过渡段指数
    internal const double T = 0.2;
    internal const double Q = 0.5;
    internal const double S = 1;

    internal const float DefaultPressure = 0.5f;

    // ---------- 选区 ----------

    internal const double HandleHitRadius = 16; // 内容 DIP
    internal const double RotateHitScreenRadius = 24; // 视口 DIP，使用时除以缩放转内容
    internal const double LassoPointDistance2 = 16; // 内容 DIP²
    internal const int LassoHitPercentage = 50; // 笔画点落入套索的百分比阈值（0~100）

    // ---------- 撤销 ----------

    internal const int MaxHistoryCount = 200;

    // ---------- 形状 ----------

    internal const double ShapeMinStrokeWidth = 1.0; // 内容 DIP
    internal const double ShapeCommitMinDistance = 4; // 内容 DIP
    internal const int CircleSegments = 64;
    internal const double CircleMinRadius = 1; // 内容 DIP
}

internal sealed partial class SelectionVisual
{
    internal const double HandleRadius = 5; // 内容 DIP

    internal const double RotateScreenRadius = 8; // 视口 DIP
    internal const double RotateGapAboveSelection = 32; // 视口 DIP，使用时除以缩放转内容
    internal const double ToolbarGapFromSelection = 8; // 视口 DIP

    internal const double HaloWidthFactor = 2.4;
    internal const byte HaloAlpha = 120; // 0~255
    internal const byte BorderAlpha = 235; // 0~255
    internal const double BorderWidth = 2.0; // 内容 DIP
    internal const double LassoWidth = 2.0; // 内容 DIP
    internal static readonly double[] LassoDashPattern = [4, 3]; // 内容 DIP

    private static readonly Color AccentColor = Color.FromRgb(0x4C, 0x8B, 0xF5);
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
