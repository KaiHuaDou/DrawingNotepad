using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using InkCanvasNext;

namespace LightBoard;

/// <summary>
/// 设置窗口的表单状态：一份草稿记录承载开关与下拉，数值项各自成项
/// </summary>
public sealed class SettingsEditor : INotifyPropertyChanged
{
    private const string ViewportDIP = "视口 DIP";
    private const string ContentDIP = "内容 DIP";

    private readonly LightBoardSettings baseline;
    private LightBoardSettings draft;

    public SettingsEditor(LightBoardSettings settings)
    {
        baseline = settings;
        draft = settings;

        GestureFields =
        [
            new("单指起笔位移阈值", ViewportDIP, InkCanvasNextOptions.GestureMin, InkCanvasNextOptions.GestureMax, 1, false,
                s => s.TouchDisplacementThreshold, (s, v) => s with { TouchDisplacementThreshold = v }),
            new("双指平移锁定阈值", ViewportDIP, InkCanvasNextOptions.GestureMin, InkCanvasNextOptions.GestureMax, 1, false,
                s => s.PanZoomDisplaceThreshold, (s, v) => s with { PanZoomDisplaceThreshold = v }),
            new("双指捏合锁定间距", ViewportDIP, InkCanvasNextOptions.GestureMin, InkCanvasNextOptions.GestureMax, 1, false,
                s => s.PinchLockDistance, (s, v) => s with { PinchLockDistance = v }),
        ];

        TaperFields =
        [
            new("末端采样点数", "点", TaperParameters.PointsMin, TaperParameters.PointsMax, 1, true,
                s => s.TaperPoints, (s, v) => s with { TaperPoints = (int) v }),
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
            new("撤销历史条数", "条", InkCanvasNextOptions.MaxHistoryCountMin, InkCanvasNextOptions.MaxHistoryCountMax, 10, true,
                s => s.MaxHistoryCount, (s, v) => s with { MaxHistoryCount = (int) v }),
        ];

        ToolbarFields =
        [
            new("工具栏大小", "倍率", SettingsLimits.ToolbarScaleMin, SettingsLimits.ToolbarScaleMax, 0.05, false,
                s => s.ToolbarScale, (s, v) => s with { ToolbarScale = v }),
        ];

        Fields = [.. GestureFields, .. TaperFields, .. EdgeFields, .. EraserFields, .. UndoFields, .. ToolbarFields];

        foreach (var field in Fields)
        {
            field.Load(draft);
            field.PropertyChanged += FieldChanged;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<SettingsField> GestureFields { get; }

    public IReadOnlyList<SettingsField> TaperFields { get; }

    public IReadOnlyList<SettingsField> EdgeFields { get; }

    public IReadOnlyList<SettingsField> EraserFields { get; }

    public IReadOnlyList<SettingsField> UndoFields { get; }

    public IReadOnlyList<SettingsField> ToolbarFields { get; }

    public IReadOnlyList<SettingsField> Fields { get; }

    public bool HasErrors => Fields.Any(f => f.HasErrors);

    public bool EdgeScrollHint
    {
        get => draft.EdgeScrollHint;
        set => Update(draft with { EdgeScrollHint = value }, nameof(EdgeScrollHint));
    }

    public bool SmoothZoom
    {
        get => draft.SmoothZoom;
        set => Update(draft with { SmoothZoom = value }, nameof(SmoothZoom));
    }

    public bool TaperEnabled
    {
        get => draft.Taper;
        set => Update(draft with { Taper = value }, nameof(TaperEnabled));
    }

    public bool FitToCurve
    {
        get => draft.FitToCurve;
        set => Update(draft with { FitToCurve = value }, nameof(FitToCurve));
    }

    public string SwitchOutProcess
    {
        get => draft.SwitchOutProcessName;
        set => Update(draft with { SwitchOutProcessName = value }, nameof(SwitchOutProcess));
    }

    public bool FourFingerErase
    {
        get => draft.EffectivePalmEraseFingers == InkCanvasNextOptions.PalmEraseFingersMin;
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
        get => draft.EffectivePalmEraseFingers == InkCanvasNextOptions.PalmEraseFingersMax;
        set
        {
            if (value)
            {
                SetPalmEraseFingers(InkCanvasNextOptions.PalmEraseFingersMax);
            }
        }
    }

    public bool LassoDrag
    {
        get => draft.EffectiveSelectionDragMode == SelectionDragMode.Lasso;
        set
        {
            if (value)
            {
                SetSelectionDragMode(SelectionDragMode.Lasso);
            }
        }
    }

    public bool RectangleDrag
    {
        get => draft.EffectiveSelectionDragMode == SelectionDragMode.Rectangle;
        set
        {
            if (value)
            {
                SetSelectionDragMode(SelectionDragMode.Rectangle);
            }
        }
    }

    public LightBoardSettings Build( )
    {
        var result = draft;
        foreach (var field in Fields)
        {
            result = field.Apply(result);
        }

        return result with
        {
            SwitchOutProcessName = string.IsNullOrWhiteSpace(result.SwitchOutProcessName) ? baseline.SwitchOutProcessName : result.SwitchOutProcessName.Trim( )
        };
    }

    public void Reset( )
    {
        draft = new LightBoardSettings( );
        foreach (var field in Fields)
        {
            field.Load(draft);
        }

        // 空属性名表示所有属性都已变化
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    private void SetSelectionDragMode(SelectionDragMode mode)
    {
        if (draft.EffectiveSelectionDragMode == mode)
        {
            return;
        }

        draft = draft with { SelectionDragMode = mode };
        Raise(nameof(LassoDrag));
        Raise(nameof(RectangleDrag));
    }

    private void SetPalmEraseFingers(int fingers)
    {
        if (draft.EffectivePalmEraseFingers == fingers)
        {
            return;
        }

        draft = draft with { PalmEraseFingers = fingers };
        Raise(nameof(FourFingerErase));
        Raise(nameof(FiveFingerErase));
    }

    private void FieldChanged(object? o, PropertyChangedEventArgs e)
    {
        Raise(nameof(HasErrors));
    }

    private void Update(LightBoardSettings next, string name)
    {
        draft = next;
        Raise(name);
    }

    private void Raise(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
