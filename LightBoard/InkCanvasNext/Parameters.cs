using System.Windows;
using System.Windows.Media;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    internal static readonly Size DefaultCanvasSize = new(5760, 3240);

#if DEBUG
    internal const double DistanceThresholdFactor = 0.9;
#else
    internal const double DistanceThresholdFactor = 0.1;
#endif

    internal const double MinScale = 0.1;
    internal const double MaxScale = 10.0;
    internal readonly double distanceThreshold2;

    internal const double PanZoomDisplaceThreshold2 = 30.0 * 30.0;
    internal const double PinchMinDistance2 = 24.0 * 24.0;

    internal const double T = 0.2;
    internal const double Q = 0.5;
    internal const double S = 1;

    internal const double HandleHitRadius = 16;
    internal const double LassoPointDistance2 = 16;
    internal const double RotateHitRadius = 24;
}

internal sealed partial class SelectionVisual
{
    internal const double HandleRadius = 5;

    internal const double RotateScreenRadius = 8;
    internal const double RotateGapAboveSelection = 32;
    internal const double ToolbarGapFromSelection = 8;

    private static readonly Color AccentColor = Color.FromRgb(0x4C, 0x8B, 0xF5);
}

internal sealed partial class Eraser
{
    internal const double EraserDefaultDiameter = 50.0;
}
