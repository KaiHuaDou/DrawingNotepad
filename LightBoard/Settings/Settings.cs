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
    SelectionDragMode? SelectionDragMode = null,
    bool? ToolAutoRevert = null,
    int? ToolAutoRevertCount = null,
    bool? ShowRevertCount = null,
    bool? PenUseDevicePressure = null,
    bool? PenHoverCursor = null,
    bool? PenBarrelErase = null
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
            Taper = new( ) { Enabled = EffectiveTaper, Points = EffectiveTaperPoints },
            EdgeScrollTopThreshold = EffectiveEdgeScrollHint ? EffectiveEdgeScrollTopThreshold : 0,
            EdgeScrollBottomThreshold = EffectiveEdgeScrollHint ? EffectiveEdgeScrollBottomThreshold : 0,
            EdgeScrollLeftThreshold = EffectiveEdgeScrollHint ? EffectiveEdgeScrollLeftThreshold : 0,
            EdgeScrollRightThreshold = EffectiveEdgeScrollHint ? EffectiveEdgeScrollRightThreshold : 0,
            MaxHistoryCount = EffectiveMaxHistoryCount,
            SelectionDragMode = EffectiveSelectionDragMode,
            Pen = new( )
            {
                UseDevicePressure = EffectivePenUseDevicePressure,
                HoverCursor = EffectivePenHoverCursor,
                BarrelErase = EffectivePenBarrelErase
            },
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

    internal bool EffectiveFitToCurve => FitToCurve ?? SettingsLimits.FitToCurveDefault;

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

    internal bool EffectiveToolAutoRevert => ToolAutoRevert ?? SettingsLimits.ToolAutoRevertDefault;

    internal int EffectiveToolAutoRevertCount
        => Math.Clamp(ToolAutoRevertCount ?? SettingsLimits.ToolAutoRevertCountDefault, SettingsLimits.ToolAutoRevertCountMin, SettingsLimits.ToolAutoRevertCountMax);

    internal bool EffectiveShowRevertCount => ShowRevertCount ?? SettingsLimits.ShowRevertCountDefault;

    internal bool EffectivePenUseDevicePressure => PenUseDevicePressure ?? PenParameters.UseDevicePressureDefault;

    internal bool EffectivePenHoverCursor => PenHoverCursor ?? PenParameters.HoverCursorDefault;

    internal bool EffectivePenBarrelErase => PenBarrelErase ?? PenParameters.BarrelEraseDefault;
}

/// <summary>
/// 应用层设置项的范围，设置窗口与消费点共用；
/// 画布参数的范围由 InkCanvasNextOptions 与 EraserParameters 的常数给出
/// </summary>
internal static class SettingsLimits
{
    public const bool EdgeScrollHintDefault = true;
    public const bool FitToCurveDefault = true;
    public const double ToolbarScaleDefault = 1;
    public const double ToolbarScaleMin = 0.75;
    public const double ToolbarScaleMax = 1.5;
    public const bool ToolAutoRevertDefault = false;
    public const int ToolAutoRevertCountDefault = 1;
    public const int ToolAutoRevertCountMin = 1;
    public const int ToolAutoRevertCountMax = 10;
    public const bool ShowRevertCountDefault = true;
#if DEBUG
    public const string SwitchOutProcessDefault = "thorium";
#else
    public const string SwitchOutProcessDefault = "msedge";
#endif
}
