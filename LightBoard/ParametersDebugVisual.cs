using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

using InkCanvasNext;

using static InkCanvasNext.InkCanvasNext;

namespace LightBoard;

// Parameters.cs 参数调试面板：半径画 1:1 同心圆、距离画 1:1 线段，附缩放范围与 Smooth 数轴。
internal sealed class ParametersDebugVisual : FrameworkElement
{
    private const double PanelWidth = 900;
    private const double PanelHeight = 470;

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
    ];

    private static readonly Pen ThinPen = MakePen(MakeBrush("#66FFFFFF"), 1);
    private static readonly Pen DashPen = MakePen(MakeBrush("#CCFFFFFF"), 2, new DashStyle([4, 3], 0));
    private static readonly Pen MarkPen = MakePen(Palette[4], 3);
    private static readonly Pen AccentPen = MakePen(Palette[0], 5);
    private static readonly Pen[] RowPens =
    [
        MakePen(Palette[0], 2),
        MakePen(Palette[1], 2),
        MakePen(Palette[2], 2),
        MakePen(Palette[3], 2),
        MakePen(Palette[4], 2),
    ];

    private readonly InkCanvasNext.InkCanvasNext canvas;
    private double lastScale;

    internal ParametersDebugVisual(InkCanvasNext.InkCanvasNext canvas)
    {
        this.canvas = canvas;
        Width = PanelWidth;
        Height = PanelHeight;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Bottom;
        Margin = new Thickness(0, 0, 0, 70);
        lastScale = canvas.CurrentScale;
        canvas.ViewOrSelectionChanged += (_, _) =>
        {
            if (Math.Abs(canvas.CurrentScale - lastScale) > 1e-9)
            {
                lastScale = canvas.CurrentScale;
                InvalidateVisual( );
            }
        };
    }

    protected override void OnRender(DrawingContext dc)
    {
        var dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        Text(dc, "InkCanvasNext 调试参数（Parameters.cs）", 22, TextBrush, 24, 14, dip);

        DrawRadii(dc, dip);
        DrawDistances(dc, dip);
        DrawScaleAxis(dc, dip);
        DrawSmoothAxis(dc, dip);

        Text(dc, "圆与线段均为 1:1 实际像素；DistanceThreshold 与两数轴除外。", 11, DimBrush, 24, 436, dip);
    }

    private static void DrawRadii(DrawingContext dc, double dip)
    {
        Text(dc, "半径（1:1 同心圆）", 13, DimBrush, 24, 58, dip);

        var center = new Point(120, 195);
        dc.DrawLine(ThinPen, new Point(center.X, center.Y), new Point(center.X + Eraser.EraserDefaultDiameter / 2, center.Y));
        Circle(dc, center, SelectionVisual.HandleRadius, RowPens[0]);
        Circle(dc, center, SelectionVisual.RotateScreenRadius, RowPens[1]);
        Circle(dc, center, HandleHitRadius, RowPens[2]);
        Circle(dc, center, RotateHitRadius, RowPens[3]);
        Circle(dc, center, Eraser.EraserDefaultDiameter / 2, RowPens[4]);
        dc.DrawEllipse(TextBrush, null, center, 2, 2);

        Legend(dc, 0, $"HandleRadius = {SelectionVisual.HandleRadius:0.#}", "选区手柄绘制半径", 110, dip);
        Legend(dc, 1, $"RotateScreenRadius = {SelectionVisual.RotateScreenRadius:0.#}", "旋转手柄绘制半径", 144, dip);
        Legend(dc, 2, $"HandleHitRadius = {HandleHitRadius:0.#}", "手柄命中半径", 178, dip);
        Legend(dc, 3, $"RotateHitRadius = {RotateHitRadius:0.#}", "旋转手柄命中半径", 212, dip);
        Legend(dc, 4, $"EraserDefaultDiameter = {Eraser.EraserDefaultDiameter:0.#}", "橡皮擦默认直径（图中为 r = 25）", 246, dip);
    }

    private void DrawDistances(DrawingContext dc, double dip)
    {
        Text(dc, "距离（1:1 线段）", 13, DimBrush, 460, 58, dip);

        DistanceRow(dc, 0, 88, "LassoPointDistance", $"{Math.Sqrt(LassoPointDistance2):0.#} px", "套索采样点最小间距", Math.Sqrt(LassoPointDistance2), false, dip);
        DistanceRow(dc, 1, 124, "ToolbarGapFromSelection", $"{SelectionVisual.ToolbarGapFromSelection:0.#} px", "工具栏与选区间距", SelectionVisual.ToolbarGapFromSelection, false, dip);
        DistanceRow(dc, 2, 160, "PinchMinDistance", ThresholdText(PinchMinDistance2), "双指最小捏合间距（Release = 24 px）", InfinityLength(PinchMinDistance2), double.IsInfinity(PinchMinDistance2), dip);
        DistanceRow(dc, 3, 196, "PanZoomDisplaceThreshold", ThresholdText(PanZoomDisplaceThreshold2), "双指中点位移阈值（Release = 30 px）", InfinityLength(PanZoomDisplaceThreshold2), double.IsInfinity(PanZoomDisplaceThreshold2), dip);
        DistanceRow(dc, 4, 232, "RotateGapAboveSelection", $"{SelectionVisual.RotateGapAboveSelection:0.#} px", "选区顶边到旋转手柄间距", SelectionVisual.RotateGapAboveSelection, false, dip);
        DistanceRow(dc, 5, 268, "DistanceThreshold", $"≈ {Math.Sqrt(canvas.distanceThreshold2):0.#} px", "多指并拢判定阈值 = 0.9 × 屏宽（非 1:1）", 100, true, dip);
    }

    private void DrawScaleAxis(DrawingContext dc, double dip)
    {
        Text(dc, "缩放范围（对数轴）", 13, DimBrush, 60, 326, dip);
        dc.DrawLine(ThinPen, new Point(60, 368), new Point(420, 368));
        foreach (var v in new[] { 0.1, 0.2, 0.5, 1.0, 2.0, 5.0, 10.0 })
        {
            var x = ScalePos(v);
            dc.DrawLine(ThinPen, new Point(x, 362), new Point(x, 374));
            TextCentered(dc, $"{v:0.#}", 11, TextBrush, x, 378, dip);
        }

        var cx = Math.Clamp(ScalePos(canvas.CurrentScale), 60, 420);
        dc.DrawLine(MarkPen, new Point(cx, 356), new Point(cx, 380));
        Text(dc, $"{canvas.CurrentScale:0.00}× 当前", 11, Palette[4], cx + 6, 344, dip);

        Text(dc, "PanZoom 目标缩放被夹在此范围内", 11, DimBrush, 60, 402, dip);
    }

    private static void DrawSmoothAxis(DrawingContext dc, double dip)
    {
        Text(dc, $"Smooth 平滑：T = {T:0.#}，Q = {Q:0.#}，S = {S:0.#}", 13, DimBrush, 460, 326, dip);
        dc.DrawLine(ThinPen, new Point(460, 368), new Point(880, 368));
        dc.DrawLine(RowPens[1], new Point(460, 368), new Point(SmoothPos(1 - T), 368));
        dc.DrawLine(RowPens[1], new Point(SmoothPos(1 + T), 368), new Point(880, 368));
        dc.DrawLine(AccentPen, new Point(SmoothPos(1 - T), 368), new Point(SmoothPos(1 + T), 368));

        foreach (var v in new[] { 0.5, 1 - T, 1.0, 1 + T, 1.5 })
        {
            var x = SmoothPos(v);
            dc.DrawLine(ThinPen, new Point(x, 362), new Point(x, 374));
            TextCentered(dc, $"{v:0.#}", 11, TextBrush, x, 378, dip);
        }

        TextCentered(dc, "死区（缩放锁定 1）", 11, Palette[0], SmoothPos(1), 346, dip);
        Text(dc, "两指距离比在死区内锁定缩放，过渡区平滑渐变，其余直通", 11, DimBrush, 460, 402, dip);
    }

    private static void Circle(DrawingContext dc, Point center, double radius, Pen pen)
    {
        dc.DrawEllipse(null, pen, center, radius, radius);
    }

    private static void Legend(DrawingContext dc, int palette, string title, string note, double y, double dip)
    {
        dc.DrawEllipse(Palette[palette], null, new Point(198, y + 7), 4, 4);
        Text(dc, title, 13, TextBrush, 212, y, dip);
        Text(dc, note, 11, DimBrush, 212, y + 17, dip);
    }

    private static void DistanceRow(DrawingContext dc, int palette, double y, string name, string value, string note, double length, bool dashed, double dip)
    {
        var pen = dashed ? DashPen : RowPens[palette];
        dc.DrawLine(pen, new Point(470, y + 10), new Point(470 + length, y + 10));
        dc.DrawLine(pen, new Point(470, y + 4), new Point(470, y + 16));
        dc.DrawLine(pen, new Point(470 + length, y + 4), new Point(470 + length, y + 16));
        Text(dc, $"{name} = {value}", 13, TextBrush, 590, y - 2, dip);
        Text(dc, note, 11, DimBrush, 590, y + 16, dip);
    }

    private static void Text(DrawingContext dc, string s, double size, Brush brush, double x, double y, double dip)
    {
        var ft = new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Font, size, brush, dip);
        dc.DrawText(ft, new Point(x, y));
    }

    private static void TextCentered(DrawingContext dc, string s, double size, Brush brush, double centerX, double y, double dip)
    {
        var ft = new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Font, size, brush, dip);
        dc.DrawText(ft, new Point(centerX - ft.Width / 2, y));
    }

    private static string ThresholdText(double threshold2)
    {
        return double.IsInfinity(threshold2) ? "∞（禁用）" : $"{Math.Sqrt(threshold2):0.#} px";
    }

    private static double InfinityLength(double threshold2)
    {
        return double.IsInfinity(threshold2) ? 44 : Math.Sqrt(threshold2);
    }

    private static double ScalePos(double v)
    {
        return 60 + (Math.Log10(v) + 1) / 2 * 360;
    }

    private static double SmoothPos(double v)
    {
        return 460 + (v - 0.5) * 420;
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
