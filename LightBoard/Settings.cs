using System;

using System.Text.Json.Serialization;

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
    int MaxHistoryCount = InkCanvasNextOptions.MaxHistoryCountDefault,
    int PalmEraseFingers = InkCanvasNextOptions.PalmEraseFingersDefault,
    double EraserDiameter = EraserParameters.DiameterDefault,
    double EraserPalmExtra = EraserParameters.PalmExtraDefault,
    double EraserMinLogicalDiameter = EraserParameters.MinLogicalDiameterDefault,
    double EraserRebuildThreshold = EraserParameters.RebuildThresholdDefault,
    double ToolbarScale = SettingsLimits.ToolbarScaleDefault,
    string SwitchOutProcessName = SettingsLimits.SwitchOutProcessDefault
)
{
    public const double ToolbarScaleDefault = SettingsLimits.ToolbarScaleDefault;

    /// <summary>
    /// 创建画布时读取的选项，改动需重启
    /// </summary>
    [JsonIgnore]
    public InkCanvasNextOptions CanvasOptions => new( )
    {
        PalmEraseFingers = PalmEraseFingers,
        TouchDisplacementThreshold = TouchDisplacementThreshold,
        PanZoomDisplaceThreshold = PanZoomDisplaceThreshold,
        PinchLockDistance = PinchLockDistance,
        SmoothZoom = SmoothZoom,
        EdgeScrollTopThreshold = EdgeScrollHint ? EdgeScrollTopThreshold : 0,
        EdgeScrollBottomThreshold = EdgeScrollHint ? EdgeScrollBottomThreshold : 0,
        EdgeScrollLeftThreshold = EdgeScrollHint ? EdgeScrollLeftThreshold : 0,
        EdgeScrollRightThreshold = EdgeScrollHint ? EdgeScrollRightThreshold : 0,
        MaxHistoryCount = MaxHistoryCount,
        Eraser = new( )
        {
            Diameter = EraserDiameter,
            PalmExtra = EraserPalmExtra,
            MinLogicalDiameter = EraserMinLogicalDiameter,
            RebuildThreshold = EraserRebuildThreshold
        }
    };

    /// <summary>
    /// 把手工改过的越界值收进可选范围：设置窗口与画布都按返回值工作
    /// </summary>
    internal static LightBoardSettings Normalize(LightBoardSettings data)
    {
        return data with
        {
            EdgeScrollTopThreshold = Math.Clamp(data.EdgeScrollTopThreshold, InkCanvasNextOptions.EdgeScrollThresholdMin, InkCanvasNextOptions.EdgeScrollThresholdMax),
            EdgeScrollBottomThreshold = Math.Clamp(data.EdgeScrollBottomThreshold, InkCanvasNextOptions.EdgeScrollThresholdMin, InkCanvasNextOptions.EdgeScrollThresholdMax),
            EdgeScrollLeftThreshold = Math.Clamp(data.EdgeScrollLeftThreshold, InkCanvasNextOptions.EdgeScrollThresholdMin, InkCanvasNextOptions.EdgeScrollThresholdMax),
            EdgeScrollRightThreshold = Math.Clamp(data.EdgeScrollRightThreshold, InkCanvasNextOptions.EdgeScrollThresholdMin, InkCanvasNextOptions.EdgeScrollThresholdMax),
            TouchDisplacementThreshold = Math.Clamp(data.TouchDisplacementThreshold, InkCanvasNextOptions.GestureMin, InkCanvasNextOptions.GestureMax),
            PanZoomDisplaceThreshold = Math.Clamp(data.PanZoomDisplaceThreshold, InkCanvasNextOptions.GestureMin, InkCanvasNextOptions.GestureMax),
            PinchLockDistance = Math.Clamp(data.PinchLockDistance, InkCanvasNextOptions.GestureMin, InkCanvasNextOptions.GestureMax),
            MaxHistoryCount = Math.Clamp(data.MaxHistoryCount, SettingsLimits.HistoryMin, InkCanvasNextOptions.MaxHistoryCountMax),
            PalmEraseFingers = Math.Clamp(data.PalmEraseFingers, InkCanvasNextOptions.PalmEraseFingersMin, InkCanvasNextOptions.PalmEraseFingersMax),
            EraserDiameter = Math.Clamp(data.EraserDiameter, EraserParameters.DiameterMin, EraserParameters.DiameterMax),
            EraserPalmExtra = Math.Clamp(data.EraserPalmExtra, EraserParameters.PalmExtraMin, EraserParameters.PalmExtraMax),
            EraserMinLogicalDiameter = Math.Clamp(data.EraserMinLogicalDiameter, EraserParameters.MinLogicalDiameterMin, EraserParameters.MinLogicalDiameterMax),
            EraserRebuildThreshold = Math.Clamp(data.EraserRebuildThreshold, EraserParameters.RebuildThresholdMin, EraserParameters.RebuildThresholdMax),
            ToolbarScale = Math.Clamp(data.ToolbarScale, SettingsLimits.ToolbarScaleMin, SettingsLimits.ToolbarScaleMax)
        };
    }
}

/// <summary>
/// 应用层设置项的范围，设置窗口与 <see cref="LightBoardSettings.Normalize"/> 共用；
/// 画布参数的范围由 InkCanvasNextOptions 与 EraserParameters 的常数给出
/// </summary>
internal static class SettingsLimits
{
    public const int HistoryMin = 10;
    public const double ToolbarScaleDefault = 1;
    public const double ToolbarScaleMin = 0.75;
    public const double ToolbarScaleMax = 1.5;
#if DEBUG
    public const string SwitchOutProcessDefault = "thorium";
#else
    public const string SwitchOutProcessDefault = "msedge";
#endif
}
