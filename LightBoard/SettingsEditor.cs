using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using InkCanvasNext;

namespace LightBoard;

/// <summary>
/// 设置窗口的表单状态：字段文本、边缘滚动开关、掌擦触点数，以及折叠回设置的入口
/// </summary>
public sealed class SettingsEditor : INotifyPropertyChanged
{
    private const string ViewportDIP = "视口 DIP";
    private const string ContentDIP = "内容 DIP";

    private readonly LightBoardSettings baseline;
    private bool edgeScrollHint;
    private bool smoothZoom;
    private int palmEraseFingers;
    private string switchOutProcess;

    public SettingsEditor(LightBoardSettings settings)
    {
        baseline = settings;
        edgeScrollHint = settings.EdgeScrollHint;
        smoothZoom = settings.SmoothZoom;
        palmEraseFingers = settings.PalmEraseFingers;
        switchOutProcess = settings.SwitchOutProcessName;

        GestureFields =
        [
            new("单指起笔位移阈值", ViewportDIP, InkCanvasNextOptions.GestureMin, InkCanvasNextOptions.GestureMax, 1, false,
                s => s.TouchDisplacementThreshold, (s, v) => s with { TouchDisplacementThreshold = v }),
            new("双指平移锁定阈值", ViewportDIP, InkCanvasNextOptions.GestureMin, InkCanvasNextOptions.GestureMax, 1, false,
                s => s.PanZoomDisplaceThreshold, (s, v) => s with { PanZoomDisplaceThreshold = v }),
            new("双指捏合锁定间距", ViewportDIP, InkCanvasNextOptions.GestureMin, InkCanvasNextOptions.GestureMax, 1, false,
                s => s.PinchLockDistance, (s, v) => s with { PinchLockDistance = v }),
        ];

        EdgeFields =
        [
            new("上边缘热区", ViewportDIP, InkCanvasNextOptions.EdgeScrollThresholdMin, InkCanvasNextOptions.EdgeScrollThresholdMax, 8, false,
                s => s.EdgeScrollTopThreshold, (s, v) => s with { EdgeScrollTopThreshold = v }),
            new("下边缘热区", ViewportDIP, InkCanvasNextOptions.EdgeScrollThresholdMin, InkCanvasNextOptions.EdgeScrollThresholdMax, 8, false,
                s => s.EdgeScrollBottomThreshold, (s, v) => s with { EdgeScrollBottomThreshold = v }),
            new("左边缘热区", ViewportDIP, InkCanvasNextOptions.EdgeScrollThresholdMin, InkCanvasNextOptions.EdgeScrollThresholdMax, 8, false,
                s => s.EdgeScrollLeftThreshold, (s, v) => s with { EdgeScrollLeftThreshold = v }),
            new("右边缘热区", ViewportDIP, InkCanvasNextOptions.EdgeScrollThresholdMin, InkCanvasNextOptions.EdgeScrollThresholdMax, 8, false,
                s => s.EdgeScrollRightThreshold, (s, v) => s with { EdgeScrollRightThreshold = v }),
        ];

        EraserFields =
        [
            new("橡皮直径", ViewportDIP, EraserParameters.DiameterMin, EraserParameters.DiameterMax, 5, false,
                s => s.EraserDiameter, (s, v) => s with { EraserDiameter = v }),
            new("掌擦外扩", ViewportDIP, EraserParameters.PalmExtraMin, EraserParameters.PalmExtraMax, 1, false,
                s => s.EraserPalmExtra, (s, v) => s with { EraserPalmExtra = v }),
            new("最小逻辑直径", ContentDIP, EraserParameters.MinLogicalDiameterMin, EraserParameters.MinLogicalDiameterMax, 0.1, false,
                s => s.EraserMinLogicalDiameter, (s, v) => s with { EraserMinLogicalDiameter = v }),
            new("命中重建阈值", ContentDIP, EraserParameters.RebuildThresholdMin, EraserParameters.RebuildThresholdMax, 0.05, false,
                s => s.EraserRebuildThreshold, (s, v) => s with { EraserRebuildThreshold = v }),
        ];

        UndoFields =
        [
            new("撤销历史条数", "条", SettingsLimits.HistoryMin, InkCanvasNextOptions.MaxHistoryCountMax, 10, true,
                s => s.MaxHistoryCount, (s, v) => s with { MaxHistoryCount = (int)v }),
        ];

        ToolbarFields =
        [
            new("工具栏大小", "倍率", SettingsLimits.ToolbarScaleMin, SettingsLimits.ToolbarScaleMax, 0.05, false,
                s => s.ToolbarScale, (s, v) => s with { ToolbarScale = v }),
        ];

        Fields = [.. GestureFields, .. EdgeFields, .. EraserFields, .. UndoFields, .. ToolbarFields];

        foreach (var field in Fields)
        {
            field.Load(settings);
            field.PropertyChanged += FieldChanged;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<SettingsField> GestureFields { get; }

    public IReadOnlyList<SettingsField> EdgeFields { get; }

    public IReadOnlyList<SettingsField> EraserFields { get; }

    public IReadOnlyList<SettingsField> UndoFields { get; }

    public IReadOnlyList<SettingsField> ToolbarFields { get; }

    public IReadOnlyList<SettingsField> Fields { get; }

    public bool HasErrors => Fields.Any(f => f.HasErrors);

    public bool EdgeScrollHint
    {
        get => edgeScrollHint;
        set
        {
            if (edgeScrollHint == value)
            {
                return;
            }

            edgeScrollHint = value;
            Raise(nameof(EdgeScrollHint));
        }
    }

    public bool SmoothZoom
    {
        get => smoothZoom;
        set
        {
            if (smoothZoom == value)
            {
                return;
            }

            smoothZoom = value;
            Raise(nameof(SmoothZoom));
        }
    }

    public string SwitchOutProcess
    {
        get => switchOutProcess;
        set
        {
            if (switchOutProcess == value)
            {
                return;
            }

            switchOutProcess = value;
            Raise(nameof(SwitchOutProcess));
        }
    }

    public bool FourFingerErase
    {
        get => palmEraseFingers == InkCanvasNextOptions.PalmEraseFingersMin;
        set
        {
            if (value)
            {
                SetPalmEraseFingers(InkCanvasNextOptions.PalmEraseFingersMin);
            }
        }
    }

    public bool FiveFingerErase
    {
        get => palmEraseFingers == InkCanvasNextOptions.PalmEraseFingersMax;
        set
        {
            if (value)
            {
                SetPalmEraseFingers(InkCanvasNextOptions.PalmEraseFingersMax);
            }
        }
    }

    public LightBoardSettings Build( )
    {
        var result = baseline with
        {
            EdgeScrollHint = edgeScrollHint,
            SmoothZoom = smoothZoom,
            PalmEraseFingers = palmEraseFingers,
            SwitchOutProcessName = string.IsNullOrWhiteSpace(switchOutProcess) ? baseline.SwitchOutProcessName : switchOutProcess.Trim( )
        };
        foreach (var field in Fields)
        {
            result = field.Apply(result);
        }

        return result;
    }

    public void Reset( )
    {
        var defaults = new LightBoardSettings( );
        foreach (var field in Fields)
        {
            field.Load(defaults);
        }

        EdgeScrollHint = defaults.EdgeScrollHint;
        SmoothZoom = defaults.SmoothZoom;
        SetPalmEraseFingers(defaults.PalmEraseFingers);
        SwitchOutProcess = defaults.SwitchOutProcessName;
    }

    private void SetPalmEraseFingers(int fingers)
    {
        if (palmEraseFingers == fingers)
        {
            return;
        }

        palmEraseFingers = fingers;
        Raise(nameof(FourFingerErase));
        Raise(nameof(FiveFingerErase));
    }

    private void FieldChanged(object? o, PropertyChangedEventArgs e)
    {
        Raise(nameof(HasErrors));
    }

    private void Raise(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
