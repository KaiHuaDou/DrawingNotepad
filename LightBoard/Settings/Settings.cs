using System;

using InkCanvasNext;

namespace LightBoard;

/// <summary>
/// settings.json 的内容：为 null 的项表示用户没设过，按程序默认值生效
/// </summary>
public record LightBoardSettings(
    bool? EdgeScrollHint = null,
    double? EdgeScrollTopThreshold = null,
    double? EdgeScrollBottomThreshold = null,
    double? EdgeScrollLeftThreshold = null,
    double? EdgeScrollRightThreshold = null,
    double? TouchDisplacementThreshold = null,
    double? PanZoomDisplaceThreshold = null,
    double? PinchLockDistance = null,
    bool? SmoothZoom = null,
    bool? FitToCurve = null,
    bool? Taper = null,
    int? TaperPoints = null,
    int? MaxHistoryCount = null,
    int? PalmEraseFingers = null,
    double? EraserDiameter = null,
    double? EraserPalmExtra = null,
    double? EraserMinLogicalDiameter = null,
    double? EraserRebuildThreshold = null,
    double? ToolbarScale = null,
    string? SwitchOutProcessName = null,
    SelectionDragMode? SelectionDragMode = null
)
{
    public const double ToolbarScaleDefault = SettingsLimits.ToolbarScaleDefault;

    /// <summary>
    /// 按当前设置创建画布选项；未设置的项取程序默认值，越界值取边界
    /// </summary>
    public InkCanvasNextOptions CreateCanvasOptions( )
    {
        return new( )
        {
            PalmEraseFingers = EffectivePalmEraseFingers,
            TouchDisplacementThreshold = EffectiveTouchDisplacementThreshold,
            PanZoomDisplaceThreshold = EffectivePanZoomDisplaceThreshold,
            PinchLockDistance = EffectivePinchLockDistance,
            SmoothZoom = EffectiveSmoothZoom,
            FitToCurve = EffectiveFitToCurve,
            Taper = new( ) { Enabled = EffectiveTaper, Points = EffectiveTaperPoints },
            EdgeScrollTopThreshold = EffectiveEdgeScrollHint ? EffectiveEdgeScrollTopThreshold : 0,
            EdgeScrollBottomThreshold = EffectiveEdgeScrollHint ? EffectiveEdgeScrollBottomThreshold : 0,
            EdgeScrollLeftThreshold = EffectiveEdgeScrollHint ? EffectiveEdgeScrollLeftThreshold : 0,
            EdgeScrollRightThreshold = EffectiveEdgeScrollHint ? EffectiveEdgeScrollRightThreshold : 0,
            MaxHistoryCount = EffectiveMaxHistoryCount,
            SelectionDragMode = EffectiveSelectionDragMode,
            Eraser = new( )
            {
                Diameter = EffectiveEraserDiameter,
                PalmExtra = EffectiveEraserPalmExtra,
                MinLogicalDiameter = EffectiveEraserMinLogicalDiameter,
                RebuildThreshold = EffectiveEraserRebuildThreshold
            }
        };
    }

    internal bool EffectiveEdgeScrollHint => EdgeScrollHint ?? SettingsLimits.EdgeScrollHintDefault;

    internal double EffectiveEdgeScrollTopThreshold => EdgeScrollTopThreshold ?? InkCanvasNextOptions.EdgeScrollTopDefault;

    internal double EffectiveEdgeScrollBottomThreshold => EdgeScrollBottomThreshold ?? InkCanvasNextOptions.EdgeScrollBottomDefault;

    internal double EffectiveEdgeScrollLeftThreshold => EdgeScrollLeftThreshold ?? InkCanvasNextOptions.EdgeScrollLeftDefault;

    internal double EffectiveEdgeScrollRightThreshold => EdgeScrollRightThreshold ?? InkCanvasNextOptions.EdgeScrollRightDefault;

    internal double EffectiveTouchDisplacementThreshold => TouchDisplacementThreshold ?? InkCanvasNextOptions.TouchDisplacementThresholdDefault;

    internal double EffectivePanZoomDisplaceThreshold => PanZoomDisplaceThreshold ?? InkCanvasNextOptions.PanZoomDisplaceThresholdDefault;

    internal double EffectivePinchLockDistance => PinchLockDistance ?? InkCanvasNextOptions.PinchLockDistanceDefault;

    internal bool EffectiveSmoothZoom => SmoothZoom ?? InkCanvasNextOptions.SmoothZoomDefault;

    internal bool EffectiveFitToCurve => FitToCurve ?? InkCanvasNextOptions.FitToCurveDefault;

    internal bool EffectiveTaper => Taper ?? TaperParameters.EnabledDefault;

    internal int EffectiveTaperPoints => TaperPoints ?? TaperParameters.PointsDefault;

    internal int EffectiveMaxHistoryCount => MaxHistoryCount ?? InkCanvasNextOptions.MaxHistoryCountDefault;

    internal int EffectivePalmEraseFingers
        => Math.Clamp(PalmEraseFingers ?? InkCanvasNextOptions.PalmEraseFingersDefault, InkCanvasNextOptions.PalmEraseFingersMin, InkCanvasNextOptions.PalmEraseFingersMax);

    internal double EffectiveEraserDiameter => EraserDiameter ?? EraserParameters.DiameterDefault;

    internal double EffectiveEraserPalmExtra => EraserPalmExtra ?? EraserParameters.PalmExtraDefault;

    internal double EffectiveEraserMinLogicalDiameter => EraserMinLogicalDiameter ?? EraserParameters.MinLogicalDiameterDefault;

    internal double EffectiveEraserRebuildThreshold => EraserRebuildThreshold ?? EraserParameters.RebuildThresholdDefault;

    internal double EffectiveToolbarScale => Math.Clamp(ToolbarScale ?? SettingsLimits.ToolbarScaleDefault, SettingsLimits.ToolbarScaleMin, SettingsLimits.ToolbarScaleMax);

    internal string EffectiveSwitchOutProcess
        => string.IsNullOrWhiteSpace(SwitchOutProcessName) ? SettingsLimits.SwitchOutProcessDefault : SwitchOutProcessName.Trim( );

    internal SelectionDragMode EffectiveSelectionDragMode
        => SelectionDragMode is { } mode && Enum.IsDefined(mode) ? mode : InkCanvasNextOptions.SelectionDragModeDefault;
}

/// <summary>
/// 应用层设置项的范围，设置窗口与消费点共用；
/// 画布参数的范围由 InkCanvasNextOptions 与 EraserParameters 的常数给出
/// </summary>
internal static class SettingsLimits
{
    public const bool EdgeScrollHintDefault = true;
    public const double ToolbarScaleDefault = 1;
    public const double ToolbarScaleMin = 0.75;
    public const double ToolbarScaleMax = 1.5;
#if DEBUG
    public const string SwitchOutProcessDefault = "thorium";
#else
    public const string SwitchOutProcessDefault = "msedge";
#endif
}
