using System.Windows;
using System.Windows.Media;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    internal static readonly Size DefaultCanvasSize = new(5760, 3240);

#if DEBUG
    private const double DistanceThresholdFactor = 0.9;
#else
    private const double DistanceThresholdFactor = 0.1;
#endif

    private const double MinScale = 0.1;
    private const double MaxScale = 10.0;
    private readonly double distanceThreshold2;

    private const double PanZoomDisplaceThreshold2 = 30.0 * 30.0;
    private const double PinchMinDistance2 = 24.0 * 24.0;

    private const double T = 0.2;
    private const double Q = 0.5;
    private const double S = 1;

    private const double HandleHitRadius = 16;
    private const double LassoPointDistance2 = 16;
    private const double RotateHitRadius = 24;
}

internal sealed partial class SelectionVisual
{
    private const double HandleRadius = 5;

    private const double RotateScreenRadius = 8;
    private const double RotateGapAboveSelection = 32;
    internal const double ToolbarGapFromSelection = 8;

    private static readonly Color AccentColor = Color.FromRgb(0x4C, 0x8B, 0xF5);
}

internal sealed partial class Eraser
{
    internal const double EraserDefaultDiameter = 50.0;
}
