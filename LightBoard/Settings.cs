using System;

using InkCanvasNext;

namespace LightBoard;

/// <summary>
/// settings.json 的内容，参数默认值同时是设置窗口的默认值
/// </summary>
public record LightBoardSettings(
    bool EdgeScrollHint = true,
    double EdgeScrollTopThreshold = InkCanvasNextOptions.EdgeScrollTopDefault,
    double EdgeScrollBottomThreshold = InkCanvasNextOptions.EdgeScrollBottomDefault,
    double EdgeScrollLeftThreshold = InkCanvasNextOptions.EdgeScrollLeftDefault,
    double EdgeScrollRightThreshold = InkCanvasNextOptions.EdgeScrollRightDefault,
    double TouchDisplacementThreshold = InkCanvasNextOptions.TouchDisplacementThresholdDefault,
    double PanZoomDisplaceThreshold = InkCanvasNextOptions.PanZoomDisplaceThresholdDefault,
    double PinchLockDistance = InkCanvasNextOptions.PinchLockDistanceDefault,
    bool SmoothZoom = InkCanvasNextOptions.SmoothZoomDefault,
    bool FitToCurve = InkCanvasNextOptions.FitToCurveDefault,
    bool Taper = TaperParameters.EnabledDefault,
    int TaperPoints = TaperParameters.PointsDefault,
    int MaxHistoryCount = InkCanvasNextOptions.MaxHistoryCountDefault,
    int PalmEraseFingers = InkCanvasNextOptions.PalmEraseFingersDefault,
    double EraserDiameter = EraserParameters.DiameterDefault,
    double EraserPalmExtra = EraserParameters.PalmExtraDefault,
    double EraserMinLogicalDiameter = EraserParameters.MinLogicalDiameterDefault,
    double EraserRebuildThreshold = EraserParameters.RebuildThresholdDefault,
    double ToolbarScale = SettingsLimits.ToolbarScaleDefault,
    string SwitchOutProcessName = SettingsLimits.SwitchOutProcessDefault,
    SelectionDragMode SelectionDragMode = InkCanvasNextOptions.SelectionDragModeDefault
)
{
    public const double ToolbarScaleDefault = SettingsLimits.ToolbarScaleDefault;

    /// <summary>
    /// 按当前设置创建画布选项；每次调用返回新实例
    /// </summary>
    public InkCanvasNextOptions CreateCanvasOptions( )
    {
        return new( )
        {
            PalmEraseFingers = PalmEraseFingers,
            TouchDisplacementThreshold = TouchDisplacementThreshold,
            PanZoomDisplaceThreshold = PanZoomDisplaceThreshold,
            PinchLockDistance = PinchLockDistance,
            SmoothZoom = SmoothZoom,
            FitToCurve = FitToCurve,
            Taper = new( ) { Enabled = Taper, Points = TaperPoints },
            EdgeScrollTopThreshold = EdgeScrollHint ? EdgeScrollTopThreshold : 0,
            EdgeScrollBottomThreshold = EdgeScrollHint ? EdgeScrollBottomThreshold : 0,
            EdgeScrollLeftThreshold = EdgeScrollHint ? EdgeScrollLeftThreshold : 0,
            EdgeScrollRightThreshold = EdgeScrollHint ? EdgeScrollRightThreshold : 0,
            MaxHistoryCount = MaxHistoryCount,
            SelectionDragMode = SelectionDragMode,
            Eraser = new( )
            {
                Diameter = EraserDiameter,
                PalmExtra = EraserPalmExtra,
                MinLogicalDiameter = EraserMinLogicalDiameter,
                RebuildThreshold = EraserRebuildThreshold
            }
        };
    }

    /// <summary>
    /// 工具栏倍率，手工改过的越界值取边界
    /// </summary>
    internal double EffectiveToolbarScale => Math.Clamp(ToolbarScale, SettingsLimits.ToolbarScaleMin, SettingsLimits.ToolbarScaleMax);

    /// <summary>
    /// 切换目标进程名，空白时取默认值
    /// </summary>
    internal string EffectiveSwitchOutProcess =>
        string.IsNullOrWhiteSpace(SwitchOutProcessName) ? SettingsLimits.SwitchOutProcessDefault : SwitchOutProcessName.Trim( );

    /// <summary>
    /// 选区方式，未定义的值取默认
    /// </summary>
    internal SelectionDragMode EffectiveSelectionDragMode =>
        Enum.IsDefined(SelectionDragMode) ? SelectionDragMode : InkCanvasNextOptions.SelectionDragModeDefault;

    /// <summary>
    /// 掌擦触点数，取值收进 4 或 5
    /// </summary>
    internal int EffectivePalmEraseFingers =>
        Math.Clamp(PalmEraseFingers, InkCanvasNextOptions.PalmEraseFingersMin, InkCanvasNextOptions.PalmEraseFingersMax);
}

/// <summary>
/// 应用层设置项的范围，设置窗口与消费点共用；
/// 画布参数的范围由 InkCanvasNextOptions 与 EraserParameters 的常数给出
/// </summary>
internal static class SettingsLimits
{
    public const double ToolbarScaleDefault = 1;
    public const double ToolbarScaleMin = 0.75;
    public const double ToolbarScaleMax = 1.5;
#if DEBUG
    public const string SwitchOutProcessDefault = "thorium";
#else
    public const string SwitchOutProcessDefault = "msedge";
#endif
}
