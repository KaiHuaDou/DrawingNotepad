using System;
using System.Text.Json.Serialization;

using InkCanvasNext;

using static InkCanvasNext.InkCanvasNext;

namespace LightBoard;

/// <summary>
/// settings.json 的内容，参数默认值同时是设置窗口的默认值
/// </summary>
public record LightBoardSettings(
    bool EdgeScrollHint = true,
    double EdgeScrollTopThreshold = EdgeScrollTopDefaultThreshold,
    double EdgeScrollBottomThreshold = EdgeScrollBottomDefaultThreshold,
    double EdgeScrollLeftThreshold = EdgeScrollLeftDefaultThreshold,
    double EdgeScrollRightThreshold = EdgeScrollRightDefaultThreshold,
    double TouchDisplacementThreshold = TouchDisplacementThresholdDefault,
    double PanZoomDisplaceThreshold = PanZoomDisplaceThresholdDefault,
    double PinchLockDistance = PinchLockDistanceDefault,
    int MaxHistoryCount = MaxHistoryCount,
    int PalmEraseFingers = PalmEraseFingersDefault,
    double EraserDiameter = EraserDiameterDefault,
    double EraserPalmExtra = EraserPalmExtraDefault,
    double EraserMinLogicalDiameter = EraserMinLogicalDiameterDefault,
    double EraserRebuildThreshold = EraserRebuildThresholdDefault,
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
            EdgeScrollTopThreshold = Math.Clamp(data.EdgeScrollTopThreshold, SettingsLimits.EdgeScrollMin, SettingsLimits.EdgeScrollMax),
            EdgeScrollBottomThreshold = Math.Clamp(data.EdgeScrollBottomThreshold, SettingsLimits.EdgeScrollMin, SettingsLimits.EdgeScrollMax),
            EdgeScrollLeftThreshold = Math.Clamp(data.EdgeScrollLeftThreshold, SettingsLimits.EdgeScrollMin, SettingsLimits.EdgeScrollMax),
            EdgeScrollRightThreshold = Math.Clamp(data.EdgeScrollRightThreshold, SettingsLimits.EdgeScrollMin, SettingsLimits.EdgeScrollMax),
            TouchDisplacementThreshold = Math.Clamp(data.TouchDisplacementThreshold, SettingsLimits.GestureMin, SettingsLimits.GestureMax),
            PanZoomDisplaceThreshold = Math.Clamp(data.PanZoomDisplaceThreshold, SettingsLimits.GestureMin, SettingsLimits.GestureMax),
            PinchLockDistance = Math.Clamp(data.PinchLockDistance, SettingsLimits.GestureMin, SettingsLimits.GestureMax),
            MaxHistoryCount = Math.Clamp(data.MaxHistoryCount, SettingsLimits.HistoryMin, SettingsLimits.HistoryMax),
            PalmEraseFingers = Math.Clamp(data.PalmEraseFingers, PalmEraseFingersMin, PalmEraseFingersMax),
            EraserDiameter = Math.Clamp(data.EraserDiameter, SettingsLimits.EraserDiameterMin, SettingsLimits.EraserDiameterMax),
            EraserPalmExtra = Math.Clamp(data.EraserPalmExtra, SettingsLimits.PalmExtraMin, SettingsLimits.PalmExtraMax),
            EraserMinLogicalDiameter = Math.Clamp(data.EraserMinLogicalDiameter, SettingsLimits.LogicalDiameterMin, SettingsLimits.LogicalDiameterMax),
            EraserRebuildThreshold = Math.Clamp(data.EraserRebuildThreshold, SettingsLimits.RebuildThresholdMin, SettingsLimits.RebuildThresholdMax),
            ToolbarScale = Math.Clamp(data.ToolbarScale, SettingsLimits.ToolbarScaleMin, SettingsLimits.ToolbarScaleMax)
        };
    }
}

/// <summary>
/// 设置项的可选范围，设置窗口与 <see cref="LightBoardSettings.Normalize"/> 共用
/// </summary>
internal static class SettingsLimits
{
    public const double GestureMin = 1;
    public const double GestureMax = 200;
    public const double EdgeScrollMin = 0;
    public const double EdgeScrollMax = 1000;
    public const int HistoryMin = 10;
    public const int HistoryMax = 1000;
    public const double EraserDiameterMin = 1;
    public const double EraserDiameterMax = 400;
    public const double PalmExtraMin = 0;
    public const double PalmExtraMax = 100;
    public const double LogicalDiameterMin = 0.1;
    public const double LogicalDiameterMax = 10;
    public const double RebuildThresholdMin = 0.01;
    public const double RebuildThresholdMax = 10;
    public const double ToolbarScaleDefault = 1;
    public const double ToolbarScaleMin = 0.75;
    public const double ToolbarScaleMax = 1.5;
#if DEBUG
    public const string SwitchOutProcessDefault = "thorium";
#else
    public const string SwitchOutProcessDefault = "msedge";
#endif
}
