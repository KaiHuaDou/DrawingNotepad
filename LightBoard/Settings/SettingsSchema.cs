using System;
using System.Collections.Generic;

using InkCanvasNext;

namespace LightBoard;

// 设置项表：分区顺序即侧栏顺序，XAML 里只有模板
internal static class SettingsSchema
{
    private const string ViewportDIP = "视口 DIP";
    private const string ContentDIP = "内容 DIP";

    internal static IReadOnlyList<SettingsSection> Create( )
    {
        return
        [
            new("手势",
            [
                new SettingsField("单指起笔位移阈值", nameof(LightBoardSettings.TouchDisplacementThreshold), ViewportDIP,
                    InkCanvasNextOptions.GestureMin, InkCanvasNextOptions.GestureMax, 1, false,
                    s => s.EffectiveTouchDisplacementThreshold,
                    s => s.TouchDisplacementThreshold,
                    (s, v) => s with { TouchDisplacementThreshold = v }),
                new SettingsField("双指平移锁定阈值", nameof(LightBoardSettings.PanZoomDisplaceThreshold), ViewportDIP,
                    InkCanvasNextOptions.GestureMin, InkCanvasNextOptions.GestureMax, 1, false,
                    s => s.EffectivePanZoomDisplaceThreshold,
                    s => s.PanZoomDisplaceThreshold,
                    (s, v) => s with { PanZoomDisplaceThreshold = v }),
                new SettingsField("双指捏合锁定间距", nameof(LightBoardSettings.PinchLockDistance), ViewportDIP,
                    InkCanvasNextOptions.GestureMin, InkCanvasNextOptions.GestureMax, 1, false,
                    s => s.EffectivePinchLockDistance,
                    s => s.PinchLockDistance,
                    (s, v) => s with { PinchLockDistance = v }),
                new SettingsToggle("双指缩放按平滑曲线跟随", nameof(LightBoardSettings.SmoothZoom),
                    s => s.EffectiveSmoothZoom,
                    s => s.SmoothZoom,
                    (s, v) => s with { SmoothZoom = v }),
                new SettingsChoice("空白处拖动选区", nameof(LightBoardSettings.SelectionDragMode), "圈选", "框选",
                    s => s.EffectiveSelectionDragMode == SelectionDragMode.Lasso,
                    s => s.SelectionDragMode is null ? null : s.SelectionDragMode == SelectionDragMode.Lasso,
                    (s, first) => s with
                    {
                        SelectionDragMode = first is null
                            ? null
                            : first.Value ? SelectionDragMode.Lasso : SelectionDragMode.Rectangle
                    }),
            ]),

            new("书写",
            [
                new SettingsToggle("笔画按曲线拟合", nameof(LightBoardSettings.FitToCurve),
                    s => s.EffectiveFitToCurve,
                    s => s.FitToCurve,
                    (s, v) => s with { FitToCurve = v }),
                new SettingsToggle("笔画末端渐细", nameof(LightBoardSettings.Taper),
                    s => s.EffectiveTaper,
                    s => s.Taper,
                    (s, v) => s with { Taper = v }),
                new SettingsField("末端采样点数", nameof(LightBoardSettings.TaperPoints), "点",
                    TaperParameters.PointsMin, TaperParameters.PointsMax, 1, true,
                    s => s.EffectiveTaperPoints,
                    s => s.TaperPoints,
                    (s, v) => s with { TaperPoints = v is null ? null : (int) v.Value }),
            ]),

            new("手写笔",
            [
                new SettingsToggle("手写笔按压力改变粗细", nameof(LightBoardSettings.PenUseDevicePressure),
                    s => s.EffectivePenUseDevicePressure,
                    s => s.PenUseDevicePressure,
                    (s, v) => s with { PenUseDevicePressure = v }),
                new SettingsToggle("手写笔悬停时显示笔尖", nameof(LightBoardSettings.PenHoverCursor),
                    s => s.EffectivePenHoverCursor,
                    s => s.PenHoverCursor,
                    (s, v) => s with { PenHoverCursor = v }),
                new SettingsToggle("笔杆按钮临时擦除", nameof(LightBoardSettings.PenBarrelErase),
                    s => s.EffectivePenBarrelErase,
                    s => s.PenBarrelErase,
                    (s, v) => s with { PenBarrelErase = v }),
            ]),

            new("边缘滚动提示",
            [
                new SettingsToggle("热区内有墨迹时显示滚动按钮", nameof(LightBoardSettings.EdgeScrollHint),
                    s => s.EffectiveEdgeScrollHint,
                    s => s.EdgeScrollHint,
                    (s, v) => s with { EdgeScrollHint = v }),
                new SettingsField("上边缘热区", nameof(LightBoardSettings.EdgeScrollTopThreshold), ViewportDIP,
                    InkCanvasNextOptions.EdgeScrollThresholdMin, InkCanvasNextOptions.EdgeScrollThresholdMax, 8, false,
                    s => s.EffectiveEdgeScrollTopThreshold,
                    s => s.EdgeScrollTopThreshold,
                    (s, v) => s with { EdgeScrollTopThreshold = v }),
                new SettingsField("下边缘热区", nameof(LightBoardSettings.EdgeScrollBottomThreshold), ViewportDIP,
                    InkCanvasNextOptions.EdgeScrollThresholdMin, InkCanvasNextOptions.EdgeScrollThresholdMax, 8, false,
                    s => s.EffectiveEdgeScrollBottomThreshold,
                    s => s.EdgeScrollBottomThreshold,
                    (s, v) => s with { EdgeScrollBottomThreshold = v }),
                new SettingsField("左边缘热区", nameof(LightBoardSettings.EdgeScrollLeftThreshold), ViewportDIP,
                    InkCanvasNextOptions.EdgeScrollThresholdMin, InkCanvasNextOptions.EdgeScrollThresholdMax, 8, false,
                    s => s.EffectiveEdgeScrollLeftThreshold,
                    s => s.EdgeScrollLeftThreshold,
                    (s, v) => s with { EdgeScrollLeftThreshold = v }),
                new SettingsField("右边缘热区", nameof(LightBoardSettings.EdgeScrollRightThreshold), ViewportDIP,
                    InkCanvasNextOptions.EdgeScrollThresholdMin, InkCanvasNextOptions.EdgeScrollThresholdMax, 8, false,
                    s => s.EffectiveEdgeScrollRightThreshold,
                    s => s.EdgeScrollRightThreshold,
                    (s, v) => s with { EdgeScrollRightThreshold = v }),
            ]),

            new("橡皮",
            [
                new SettingsChoice("掌擦触点数量", nameof(LightBoardSettings.PalmEraseFingers), "四指及以上", "五指及以上",
                    s => s.EffectivePalmEraseFingers == InkCanvasNextOptions.PalmEraseFingersMin,
                    s => s.PalmEraseFingers is null ? null : s.PalmEraseFingers == InkCanvasNextOptions.PalmEraseFingersMin,
                    (s, first) => s with
                    {
                        PalmEraseFingers = first is null
                            ? null
                            : first.Value ? InkCanvasNextOptions.PalmEraseFingersMin : InkCanvasNextOptions.PalmEraseFingersMax
                    }),
                new SettingsField("橡皮直径", nameof(LightBoardSettings.EraserDiameter), ViewportDIP,
                    EraserParameters.DiameterMin, EraserParameters.DiameterMax, 5, false,
                    s => s.EffectiveEraserDiameter,
                    s => s.EraserDiameter,
                    (s, v) => s with { EraserDiameter = v }),
                new SettingsField("掌擦外扩", nameof(LightBoardSettings.EraserPalmExtra), ViewportDIP,
                    EraserParameters.PalmExtraMin, EraserParameters.PalmExtraMax, 1, false,
                    s => s.EffectiveEraserPalmExtra,
                    s => s.EraserPalmExtra,
                    (s, v) => s with { EraserPalmExtra = v }),
                new SettingsField("最小逻辑直径", nameof(LightBoardSettings.EraserMinLogicalDiameter), ContentDIP,
                    EraserParameters.MinLogicalDiameterMin, EraserParameters.MinLogicalDiameterMax, 0.1, false,
                    s => s.EffectiveEraserMinLogicalDiameter,
                    s => s.EraserMinLogicalDiameter,
                    (s, v) => s with { EraserMinLogicalDiameter = v }),
                new SettingsField("命中重建阈值", nameof(LightBoardSettings.EraserRebuildThreshold), ContentDIP,
                    EraserParameters.RebuildThresholdMin, EraserParameters.RebuildThresholdMax, 0.05, false,
                    s => s.EffectiveEraserRebuildThreshold,
                    s => s.EraserRebuildThreshold,
                    (s, v) => s with { EraserRebuildThreshold = v }),
            ]),

            new("撤销",
            [
                new SettingsField("撤销历史条数", nameof(LightBoardSettings.MaxHistoryCount), "条",
                    InkCanvasNextOptions.MaxHistoryCountMin, InkCanvasNextOptions.MaxHistoryCountMax, 10, true,
                    s => s.EffectiveMaxHistoryCount,
                    s => s.MaxHistoryCount,
                    (s, v) => s with { MaxHistoryCount = v is null ? null : (int) v.Value }),
            ]),

            new("工具回退",
            [
                new SettingsToggle("橡皮、荧光笔、选择用完自动回到画笔", nameof(LightBoardSettings.ToolAutoRevert),
                    s => s.EffectiveToolAutoRevert,
                    s => s.ToolAutoRevert,
                    (s, v) => s with { ToolAutoRevert = v }),
                new SettingsField("回退前操作次数", nameof(LightBoardSettings.ToolAutoRevertCount), "次",
                    SettingsLimits.ToolAutoRevertCountMin, SettingsLimits.ToolAutoRevertCountMax, 1, true,
                    s => s.EffectiveToolAutoRevertCount,
                    s => s.ToolAutoRevertCount,
                    (s, v) => s with { ToolAutoRevertCount = v is null ? null : (int) v.Value }),
                new SettingsToggle("橡皮按钮上显示剩余次数", nameof(LightBoardSettings.ShowRevertCount),
                    s => s.EffectiveShowRevertCount,
                    s => s.ShowRevertCount,
                    (s, v) => s with { ShowRevertCount = v }),
            ]),

            new("工具栏",
            [
                new SettingsField("工具栏大小", nameof(LightBoardSettings.ToolbarScale), "倍率",
                    SettingsLimits.ToolbarScaleMin, SettingsLimits.ToolbarScaleMax, 0.05, false,
                    s => s.EffectiveToolbarScale,
                    s => s.ToolbarScale,
                    (s, v) => s with { ToolbarScale = v }),
            ]),

            new("切换目标",
            [
                new SettingsText("切换目标进程", nameof(LightBoardSettings.SwitchOutProcessName),
                    s => s.EffectiveSwitchOutProcess,
                    s => s.SwitchOutProcessName,
                    (s, v) => s with { SwitchOutProcessName = string.IsNullOrWhiteSpace(v) ? null : v.Trim( ) }),
            ]),

            new("导出",
            [
                new SettingsOptions("格式", nameof(LightBoardSettings.LastExportFormat),
                    ["PNG 图片", "PDF 文档", "SVG 矢量图"],
                    s => (int) s.EffectiveExportFormat,
                    s => s.LastExportFormat is { } format && Enum.IsDefined(format) ? (int) format : null,
                    (s, index) => s with { LastExportFormat = index is null ? null : (ExportFormat) index.Value }),
                new SettingsOptions("缩放", nameof(LightBoardSettings.LastExportScale),
                    ["25%", "50%", "100%"],
                    s => Array.IndexOf(SettingsLimits.ExportScales, s.EffectiveExportScale),
                    s => s.LastExportScale is { } scale && Array.IndexOf(SettingsLimits.ExportScales, scale) >= 0
                        ? Array.IndexOf(SettingsLimits.ExportScales, scale)
                        : null,
                    (s, index) => s with { LastExportScale = index is null ? null : SettingsLimits.ExportScales[index.Value] }),
                new SettingsText("路径", nameof(LightBoardSettings.LastExportPath),
                    s => s.EffectiveExportPath,
                    s => s.LastExportPath,
                    (s, value) => s with { LastExportPath = string.IsNullOrWhiteSpace(value) ? null : value.Trim( ) }),
            ]),
        ];
    }
}
