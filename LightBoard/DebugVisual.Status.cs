using System.Globalization;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;

using InkCanvasNext;

namespace LightBoard;

internal sealed partial class DebugVisual
{
    private static StatusGroup[] BuildStatus(CanvasStatus status)
    {
        // 指距与位移只在平移组内有意义；其余状态用占位符，避免误读上一次手势的残留值
        var panGroup = status.State is TouchState.EvalPan or TouchState.PanZoom or TouchState.Pan;
        var panning = status.State is TouchState.EvalPan or TouchState.Pan;
        var tool = status.Tool;
        var pen = status.Pen;
        var palm = status.Palm;

        return
        [
            new("画布", [
                new("触点", $"{status.Touches}"),
                new("笔画", $"{status.Strokes}"),
                new("缩放", $"{status.Scale:0.##}×"),
            ]),
            new("手势", [
                new("State", status.State.ToString( )),
                new("IsGestureActive", YesNo(status.IsGestureActive)),
                new("zoomLocked", panGroup ? YesNo(status.ZoomLocked) : Dash),
                new("distance", panGroup ? $"{status.Distance:0.0} DIP" : Dash),
                new("distance0", panGroup ? $"{status.Distance0:0.0} DIP" : Dash),
                new("PanDisplacement", panning ? $"({status.Pan.X:0.0}, {status.Pan.Y:0.0})" : Dash),
            ]),
            new("状态特征", [
                new("BlocksNativeInput", YesNo(status.Traits.BlocksNativeInput)),
                new("OverridesEditing", YesNo(status.Traits.OverridesEditing)),
                new("PalmErase", YesNo(status.Traits.PalmErase)),
                new("AreaEraseHost", YesNo(status.Traits.AreaEraseHost)),
            ]),
            new("掌擦", [
                new("掌阈值", $"{palm.MinWidth:0} × {palm.MinHeight:0} DIP"),
                new("手掌触点", $"{palm.PalmCount}"),
                new("面积源", palm.HasContactSize ? "有" : "无"),
                new("触点", $"{palm.LastContact.Width:0} × {palm.LastContact.Height:0}"),
                new("簇", $"{palm.ClusterWidth:0} × {palm.ClusterHeight:0}"),
                new("对角线", $"{palm.Diagonal:0}"),
                new("面积直径", $"{palm.AreaDiameter:0.#}"),
                new("包络直径", $"{palm.EnvelopeDiameter:0.#}"),
                new("最终直径", $"{palm.FinalDiameter:0.#}"),
                new("判定", palm.IsAreaPalm ? "面积掌擦" : palm.PalmCount > 0 ? "手掌未接管" : "手指数"),
            ]),
            new("工具快照", [
                new("Mode", tool.Mode.ToString( )),
                new("Stamp", tool.Stamp.ToString( )),
                new("IsReadOnly", YesNo(tool.IsReadOnly)),
                new("IsStamp", YesNo(tool.IsStamp)),
                new("IsSelectionEntry", YesNo(tool.IsSelectionEntry)),
                new("IsSelect", YesNo(tool.IsSelect)),
                new("IsAreaErase", YesNo(tool.IsAreaErase)),
                new("IsShape", YesNo(tool.IsShape)),
                new("IsCircle", YesNo(tool.IsCircle)),
                new("IsErase", YesNo(tool.IsErase)),
                new("AllowsMultiDraw", YesNo(tool.AllowsMultiDraw)),
                new("IsCustomTool", YesNo(tool.IsCustomTool)),
                new("WantsPreemptiveCapture", YesNo(tool.WantsPreemptiveCapture)),
            ]),
            new("笔触", [
                new("Color", pen.Color.ToString( )),
                new("Width", pen.Width.ToString("0.##", CultureInfo.CurrentCulture)),
                new("Height", pen.Height.ToString("0.##", CultureInfo.CurrentCulture)),
                new("StylusTip", pen.StylusTip.ToString( )),
                new("StylusTipTransform", $"{pen.Transform.M11:0.##}, {pen.Transform.M12:0.##}, {pen.Transform.M21:0.##}, {pen.Transform.M22:0.##}, {pen.Transform.OffsetX:0.##}, {pen.Transform.OffsetY:0.##}"),
                new("FitToCurve", YesNo(pen.FitToCurve)),
                new("IgnorePressure", YesNo(pen.IgnorePressure)),
                new("IsHighlighter", YesNo(pen.IsHighlighter)),
            ]),
        ];
    }

    private CanvasStatus ReadStatus( )
    {
        var a = canvas.DrawingAttributes.DrawingAttributes;
        return new(
            canvas.ActiveTouchCount, canvas.Strokes.Count, canvas.CurrentScale,
            canvas.State, canvas.IsGestureActive, canvas.ZoomLocked, canvas.PinchDistance, canvas.PinchDistance0, canvas.PanDisplacement, canvas.Snapshot, canvas.Traits, canvas.PalmDebug,
            new PenStatus(a.Color, a.Width, a.Height, a.StylusTip, a.StylusTipTransform, a.FitToCurve, a.IgnorePressure, a.IsHighlighter));
    }

    private readonly record struct StatusRow(string Label, string Value);

    private sealed record StatusGroup(string Title, StatusRow[] Rows);

    private readonly record struct PenStatus(Color Color, double Width, double Height, StylusTip StylusTip, Matrix Transform, bool FitToCurve, bool IgnorePressure, bool IsHighlighter);

    private readonly record struct CanvasStatus(int Touches, int Strokes, double Scale, TouchState State, bool IsGestureActive, bool ZoomLocked, double Distance, double Distance0, Vector Pan, ToolSnapshot Tool, StateTraits Traits, PalmDebugSnapshot Palm, PenStatus Pen);
}
