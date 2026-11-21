using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Threading;

using InkCanvasNext;

using static InkCanvasNext.InkCanvasNext;

namespace LightBoard;

// 调试模式浮层：参数面板（1:1 半径圆、1:1 距离线、渲染层与渲染模式）居中贴底，运行状态计数与帧率分列左右两侧，边缘自动滚动热区以红色虚线标出。
internal sealed class DebugVisual : FrameworkElement
{
    private const double PanelWidth = 900;
    private const double PanelHeight = 350;
    private const double PanelBottomMargin = 70;

    private const double SideMargin = 40;
    private const double CountRowHeight = 34;
    private const double CountColumnGap = 110;

    private const double SampleIntervalMs = 100;

    // 状态侧栏：单列贴左、无背景，只靠标题下的细线分组，避免大面积遮挡画布
    private const double StatusFontSize = 13;
    private const double StatusGroupGap = 8;
    private const double StatusLabelGap = 10;
    private const double StatusRowHeight = 18;
    private const double StatusTitleHeight = 22;
    private const string Dash = "—";

    private static readonly Typeface Font = new("Segoe UI");

    private static readonly Brush TextBrush = MakeBrush("#FFE6E6E6");
    private static readonly Brush DimBrush = MakeBrush("#FF9AA0AC");

    private static readonly Brush[] Palette =
    [
        MakeBrush("#FF4CC2FF"),
        MakeBrush("#FF2BD9C7"),
        MakeBrush("#FFECD201"),
        MakeBrush("#FFFF9F43"),
        MakeBrush("#FFF25767"),
        MakeBrush("#FFA0AAB8"),
    ];

    private static readonly Pen ThinPen = MakePen(MakeBrush("#66FFFFFF"), 1);
    private static readonly Pen DashPen = MakePen(MakeBrush("#CCFFFFFF"), 2, new DashStyle([4, 3], 0));
    private static readonly Pen EdgeScrollPen = MakePen(MakeBrush("#E0FF3B30"), 1.5, new DashStyle([6, 4], 0));

    // 与 Palette 等长：距离行的调色板索引直接取数组下标
    private static readonly Pen[] RowPens = [.. Palette.Select(color => MakePen(color, 2))];

    private readonly InkCanvasNext.InkCanvasNext canvas;
    private readonly DispatcherTimer countTimer;
    private readonly FrameRateMeter frameMeter = new( );
    private double lastScale;
    private (int Touch, int Undo, int Redo, int Stroke) lastCounts = (-1, -1, -1, -1);
    private (double Rate, double Longest) lastFrames = (-1, -1);
    private CanvasStatus lastStatus;

    internal DebugVisual(InkCanvasNext.InkCanvasNext canvas)
    {
        this.canvas = canvas;
        lastScale = canvas.CurrentScale;
        lastStatus = ReadStatus( );
        canvas.ViewOrSelectionChanged += (_, _) =>
        {
            if (Math.Abs(canvas.CurrentScale - lastScale) > 1e-9)
            {
                lastScale = canvas.CurrentScale;
                InvalidateVisual( );
            }
        };

        // 触点数与帧率都没有对应事件，只能定点采样；仅在数值变化时重绘。
        countTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(SampleIntervalMs),
            DispatcherPriority.Normal,
            (_, _) => Refresh( ),
            Dispatcher.CurrentDispatcher
        );

        // 视口尺寸变化时热区线位置随之变化，且没有对应事件，只能依赖自身 SizeChanged 重绘
        SizeChanged += (_, _) => InvalidateVisual( );

        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                frameMeter.Start( );
                Refresh( );
                countTimer.Start( );
            }
            else
            {
                countTimer.Stop( );
                frameMeter.Stop( );
            }
        };
    }

    protected override void OnRender(DrawingContext dc)
    {
        var dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        var left = Math.Max(0, (RenderSize.Width - PanelWidth) / 2);
        var top = Math.Max(0, RenderSize.Height - PanelHeight - PanelBottomMargin);
        dc.PushTransform(new TranslateTransform(left, top));
        DrawRadii(dc, dip);
        DrawDistances(dc, dip);
        dc.Pop( );

        // 热区线在视口坐标系绘制，不参与面板平移
        DrawEdgeScrollZones(dc);

        DrawCounts(dc, dip);
        DrawStatus(dc, dip);
    }

    private void Refresh( )
    {
        var counts = (canvas.ActiveTouchCount, canvas.Position, canvas.RedoDepth, canvas.Strokes.Count);
        frameMeter.Sample(SampleIntervalMs);
        var frames = (frameMeter.Rate, frameMeter.LongestFrame);
        var status = ReadStatus( );
        if (counts == lastCounts && frames == lastFrames && status == lastStatus)
        {
            return;
        }

        lastCounts = counts;
        lastFrames = frames;
        lastStatus = status;
        InvalidateVisual( );
    }

    private void DrawCounts(DrawingContext dc, double dip)
    {
        var mid = RenderSize.Height / 2;
        var right = RenderSize.Width - SideMargin;

        CountRight(dc, "帧率", $"{frameMeter.Rate:0.0}", right, mid - 3.5 * CountRowHeight, dip);
        CountRight(dc, "最长帧", $"{frameMeter.LongestFrame:0.0} ms", right, mid - 2.5 * CountRowHeight, dip);
        CountRight(dc, "撤销", $"{canvas.Position}", right, mid - 1.5 * CountRowHeight, dip);
        CountRight(dc, "重做", $"{canvas.RedoDepth}", right, mid - 0.5 * CountRowHeight, dip);
        CountRight(dc, "系统缩放", $"{dip:P0}", right, mid + 0.5 * CountRowHeight, dip);
        CountRight(dc, "1 DIP", $"{dip:0.###} px", right, mid + 1.5 * CountRowHeight, dip);
        CountRight(dc, "渲染", $"Tier {RenderCapability.Tier >> 16}", right, mid + 2.5 * CountRowHeight, dip);
        CountRight(dc, "", $"{RenderOptions.ProcessRenderMode}", right, mid + 3.5 * CountRowHeight, dip);
    }

    // 状态侧栏贴在左侧：无背景只留文字与标题细线，避免大面积遮挡画布；标签列宽取全栏最宽
    private void DrawStatus(DrawingContext dc, double dip)
    {
        var groups = BuildStatus(lastStatus);
        var labelWidth = groups.SelectMany(group => group.Rows).Max(row => TextWidth(row.Label, StatusFontSize, dip));
        var valueWidth = groups.SelectMany(group => group.Rows).Max(row => TextWidth(row.Value, StatusFontSize, dip));
        var x = SideMargin;
        var y = SideMargin;

        foreach (var group in groups)
        {
            Text(dc, group.Title, StatusFontSize + 1, TextBrush, x, y + 3, dip);
            dc.DrawLine(ThinPen, new Point(x, y + StatusTitleHeight - 4), new Point(x + labelWidth + StatusLabelGap + valueWidth, y + StatusTitleHeight - 4));
            y += StatusTitleHeight;

            foreach (var row in group.Rows)
            {
                Text(dc, row.Label, StatusFontSize, DimBrush, x, y, dip);
                Text(dc, row.Value, StatusFontSize, TextBrush, x + labelWidth + StatusLabelGap, y, dip);
                y += StatusRowHeight;
            }

            y += StatusGroupGap;
        }
    }

    private static StatusGroup[] BuildStatus(CanvasStatus status)
    {
        // 指距与位移只在平移组内有意义；其余状态用占位符，避免误读上一次手势的残留值
        var panGroup = status.State is TouchState.EvalPan or TouchState.PanZoom or TouchState.Pan;
        var panning = status.State is TouchState.EvalPan or TouchState.Pan;
        var tool = status.Tool;
        var pen = status.Pen;

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
            canvas.State, canvas.IsGestureActive, canvas.ZoomLocked, canvas.PinchDistance, canvas.PinchDistance0, canvas.PanDisplacement, canvas.Snapshot, canvas.Traits,
            new PenStatus(a.Color, a.Width, a.Height, a.StylusTip, a.StylusTipTransform, a.FitToCurve, a.IgnorePressure, a.IsHighlighter));
    }

    private static double TextWidth(string s, double size, double dip) => new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Font, size, Brushes.Transparent, dip).Width;

    private static string YesNo(bool value) => value ? "是" : "否";

    private readonly record struct StatusRow(string Label, string Value);

    private sealed record StatusGroup(string Title, StatusRow[] Rows);

    private readonly record struct PenStatus(Color Color, double Width, double Height, StylusTip StylusTip, Matrix Transform, bool FitToCurve, bool IgnorePressure, bool IsHighlighter);

    private readonly record struct CanvasStatus(int Touches, int Strokes, double Scale, TouchState State, bool IsGestureActive, bool ZoomLocked, double Distance, double Distance0, Vector Pan, ToolSnapshot Tool, StateTraits Traits, PenStatus Pen);

    private void DrawRadii(DrawingContext dc, double dip)
    {
        var eraserRadius = canvas.EraserDiameter / 2;
        var center = new Point(120, 180);
        dc.DrawLine(ThinPen, new Point(center.X, center.Y), new Point(center.X + eraserRadius, center.Y));
        Circle(dc, center, SelectionVisual.HandleRadius, RowPens[0]);
        Circle(dc, center, SelectionVisual.RotateScreenRadius, RowPens[1]);
        Circle(dc, center, HandleHitRadius, RowPens[2]);
        Circle(dc, center, RotateHitScreenRadius, RowPens[3]);
        Circle(dc, center, eraserRadius, RowPens[4]);
        dc.DrawEllipse(TextBrush, null, center, 2, 2);

        Legend(dc, 0, $"HandleRadius = {SelectionVisual.HandleRadius:0.#} DIP", 96, dip);
        Legend(dc, 1, $"RotateScreenRadius = {SelectionVisual.RotateScreenRadius:0.#} DIP", 122, dip);
        Legend(dc, 2, $"HandleHitRadius = {HandleHitRadius:0.#} DIP", 148, dip);
        Legend(dc, 3, $"RotateHitScreenRadius = {RotateHitScreenRadius:0.#} DIP", 174, dip);
        Legend(dc, 4, $"EraserDiameter/2 = {eraserRadius:0.#} DIP", 200, dip);
    }

    private void DrawDistances(DrawingContext dc, double dip)
    {
        DistanceRow(dc, 0, 96, "LassoPointDistance", $"{Math.Sqrt(LassoPointDistance2):0.#} DIP", Math.Sqrt(LassoPointDistance2), false, dip);
        DistanceRow(dc, 1, 122, "SelectionToolbarGap", $"{SelectionToolbarGap:0.#} DIP", SelectionToolbarGap, false, dip);
        DistanceRow(dc, 2, 148, "PinchLockDistance", ThresholdText(canvas.options.PinchLockDistance), canvas.options.PinchLockDistance, false, dip);
        DistanceRow(dc, 3, 174, "PanZoomDisplaceThreshold", ThresholdText(canvas.options.PanZoomDisplaceThreshold), canvas.options.PanZoomDisplaceThreshold, false, dip);
        DistanceRow(dc, 4, 200, "RotateGapAboveSelection", $"{SelectionVisual.RotateGapAboveSelection:0.#} DIP", SelectionVisual.RotateGapAboveSelection, false, dip);
        var spacing = canvas.options.TouchSpacingThreshold;
        DistanceRow(dc, 5, 226, "DistanceThreshold", ThresholdText(spacing), Math.Min(spacing, MaxThresholdLineLength), spacing > MaxThresholdLineLength, dip);
    }

    // 边缘热区：在视口上下左右各阈值距离处画红色虚线
    private void DrawEdgeScrollZones(DrawingContext dc)
    {
        var viewport = canvas.ViewportSize;
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        var top = canvas.options.EdgeScrollTopThreshold;
        var bottom = canvas.options.EdgeScrollBottomThreshold;
        var left = canvas.options.EdgeScrollLeftThreshold;
        var right = canvas.options.EdgeScrollRightThreshold;
        dc.DrawLine(EdgeScrollPen, new Point(0, top), new Point(viewport.Width, top));
        dc.DrawLine(EdgeScrollPen, new Point(0, viewport.Height - bottom), new Point(viewport.Width, viewport.Height - bottom));
        dc.DrawLine(EdgeScrollPen, new Point(left, 0), new Point(left, viewport.Height));
        dc.DrawLine(EdgeScrollPen, new Point(viewport.Width - right, 0), new Point(viewport.Width - right, viewport.Height));
    }

    private static void Circle(DrawingContext dc, Point center, double radius, Pen pen)
    {
        dc.DrawEllipse(null, pen, center, radius, radius);
    }

    private static void Legend(DrawingContext dc, int palette, string title, double y, double dip)
    {
        dc.DrawEllipse(Palette[palette], null, new Point(196, y + 7), 4, 4);
        Text(dc, title, 13, TextBrush, 210, y, dip);
    }

    private static void DistanceRow(DrawingContext dc, int palette, double y, string name, string value, double length, bool dashed, double dip)
    {
        var pen = dashed ? DashPen : RowPens[palette];
        dc.DrawLine(pen, new Point(470, y + 10), new Point(470 + length, y + 10));
        dc.DrawLine(pen, new Point(470, y + 4), new Point(470, y + 16));
        dc.DrawLine(pen, new Point(470 + length, y + 4), new Point(470 + length, y + 16));
        Text(dc, $"{name} = {value}", 13, TextBrush, 590, y, dip);
    }

    private static void CountRight(DrawingContext dc, string label, string value, double x, double y, double dip)
    {
        TextRight(dc, label, 14, DimBrush, x - CountColumnGap, y + 5, dip);
        TextRight(dc, value, 20, TextBrush, x, y, dip);
    }

    private static void Text(DrawingContext dc, string s, double size, Brush brush, double x, double y, double dip)
    {
        var ft = new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Font, size, brush, dip);
        dc.DrawText(ft, new Point(x, y));
    }

    private static void TextRight(DrawingContext dc, string s, double size, Brush brush, double right, double y, double dip)
    {
        var ft = new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Font, size, brush, dip);
        dc.DrawText(ft, new Point(right - ft.Width, y));
    }

    // 值文本列固定在 590：线段起点 470，可用长度留出间距
    private const double MaxThresholdLineLength = 112;

    private static string ThresholdText(double threshold)
    {
        return threshold > MaxThresholdLineLength ? $"≈ {threshold:0.#} DIP" : $"{threshold:0.#} DIP";
    }

    private static SolidColorBrush MakeBrush(string hex)
    {
        var brush = new SolidColorBrush((Color) ColorConverter.ConvertFromString(hex));
        brush.Freeze( );
        return brush;
    }

    private static Pen MakePen(Brush brush, double thickness, DashStyle? dash = null)
    {
        var pen = new Pen(brush, thickness) { DashStyle = dash };
        pen.Freeze( );
        return pen;
    }
}
