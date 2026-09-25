using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

using InkCanvasNext;

using static InkCanvasNext.InkCanvasNext;

namespace LightBoard;

// 调试模式浮层：参数面板（1:1 半径圆、1:1 距离线、缩放与 Smooth 数轴、渲染层与渲染模式）居中贴底，运行状态计数与帧率分列左右两侧。
internal sealed class ParametersDebugVisual : FrameworkElement
{
    private const double PanelWidth = 900;
    private const double PanelHeight = 350;
    private const double PanelBottomMargin = 70;

    private const double SideMargin = 40;
    private const double CountRowHeight = 34;
    private const double CountColumnGap = 110;

    private const double SampleIntervalMs = 100;

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
    private readonly DispatcherTimer countTimer;
    private readonly FrameRateMeter frameMeter = new( );
    private double lastScale;
    private (int Touch, int Undo, int Redo, int Stroke) lastCounts = (-1, -1, -1, -1);
    private (double Rate, double Longest) lastFrames = (-1, -1);

    internal ParametersDebugVisual(InkCanvasNext.InkCanvasNext canvas)
    {
        this.canvas = canvas;
        lastScale = canvas.CurrentScale;
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
        Text(dc, "InkCanvasNext 参数", 20, TextBrush, 24, 12, dip);
        TextRight(dc, $"RenderCapability.Tier = {RenderCapability.Tier >> 16}   ProcessRenderMode = {RenderOptions.ProcessRenderMode}", 11, DimBrush, PanelWidth - 24, 22, dip);
        DrawRadii(dc, dip);
        DrawDistances(dc, dip);
        DrawScaleAxis(dc, dip);
        DrawSmoothAxis(dc, dip);
        dc.Pop( );

        DrawCounts(dc, dip);
    }

    private void Refresh( )
    {
        var counts = (canvas.ActiveTouchCount, canvas.Position, canvas.RedoDepth, canvas.Strokes.Count);
        frameMeter.Sample(SampleIntervalMs);
        var frames = (frameMeter.Rate, frameMeter.LongestFrame);
        if (counts == lastCounts && frames == lastFrames)
        {
            return;
        }

        lastCounts = counts;
        lastFrames = frames;
        InvalidateVisual( );
    }

    private void DrawCounts(DrawingContext dc, double dip)
    {
        var mid = RenderSize.Height / 2;
        var right = RenderSize.Width - SideMargin;

        Count(dc, "触点", $"{canvas.ActiveTouchCount}", SideMargin, mid - CountRowHeight, dip);
        Count(dc, "笔画", $"{canvas.Strokes.Count}", SideMargin, mid, dip);

        CountRight(dc, "帧率", $"{frameMeter.Rate:0.0}", right, mid - 2 * CountRowHeight, dip);
        CountRight(dc, "最长帧", $"{frameMeter.LongestFrame:0.0} ms", right, mid - CountRowHeight, dip);
        CountRight(dc, "撤销", $"{canvas.Position}", right, mid, dip);
        CountRight(dc, "重做", $"{canvas.RedoDepth}", right, mid + CountRowHeight, dip);
    }

    private static void DrawRadii(DrawingContext dc, double dip)
    {
        Text(dc, "半径（1:1）", 13, DimBrush, 24, 56, dip);

        var center = new Point(120, 180);
        dc.DrawLine(ThinPen, new Point(center.X, center.Y), new Point(center.X + Eraser.EraserDefaultDiameter / 2, center.Y));
        Circle(dc, center, SelectionVisual.HandleRadius, RowPens[0]);
        Circle(dc, center, SelectionVisual.RotateScreenRadius, RowPens[1]);
        Circle(dc, center, HandleHitRadius, RowPens[2]);
        Circle(dc, center, RotateHitRadius, RowPens[3]);
        Circle(dc, center, Eraser.EraserDefaultDiameter / 2, RowPens[4]);
        dc.DrawEllipse(TextBrush, null, center, 2, 2);

        Legend(dc, 0, $"HandleRadius = {SelectionVisual.HandleRadius:0.#}", 96, dip);
        Legend(dc, 1, $"RotateScreenRadius = {SelectionVisual.RotateScreenRadius:0.#}", 122, dip);
        Legend(dc, 2, $"HandleHitRadius = {HandleHitRadius:0.#}", 148, dip);
        Legend(dc, 3, $"RotateHitRadius = {RotateHitRadius:0.#}", 174, dip);
        Legend(dc, 4, $"EraserDefaultDiameter = {Eraser.EraserDefaultDiameter:0.#}", 200, dip);
    }

    private void DrawDistances(DrawingContext dc, double dip)
    {
        Text(dc, "距离（1:1）", 13, DimBrush, 460, 56, dip);

        DistanceRow(dc, 0, 96, "LassoPointDistance", $"{Math.Sqrt(LassoPointDistance2):0.#} px", Math.Sqrt(LassoPointDistance2), false, dip);
        DistanceRow(dc, 1, 122, "ToolbarGapFromSelection", $"{SelectionVisual.ToolbarGapFromSelection:0.#} px", SelectionVisual.ToolbarGapFromSelection, false, dip);
        DistanceRow(dc, 2, 148, "PinchMinDistance", ThresholdText(PinchMinDistance2), ThresholdLength(PinchMinDistance2), ThresholdDisabled(PinchMinDistance2), dip);
        DistanceRow(dc, 3, 174, "PanZoomDisplaceThreshold", ThresholdText(PanZoomDisplaceThreshold2), ThresholdLength(PanZoomDisplaceThreshold2), ThresholdDisabled(PanZoomDisplaceThreshold2), dip);
        DistanceRow(dc, 4, 200, "RotateGapAboveSelection", $"{SelectionVisual.RotateGapAboveSelection:0.#} px", SelectionVisual.RotateGapAboveSelection, false, dip);
        DistanceRow(dc, 5, 226, "DistanceThreshold", $"≈ {Math.Sqrt(canvas.distanceThreshold2):0.#} px", 100, true, dip);
    }

    private void DrawScaleAxis(DrawingContext dc, double dip)
    {
        Text(dc, "缩放（对数）", 13, DimBrush, 60, 268, dip);
        dc.DrawLine(ThinPen, new Point(60, 316), new Point(420, 316));
        foreach (var v in new[] { 0.1, 0.2, 0.5, 1.0, 2.0, 5.0, 10.0 })
        {
            var x = ScalePos(v);
            dc.DrawLine(ThinPen, new Point(x, 310), new Point(x, 322));
            TextCentered(dc, $"{v:0.#}", 11, TextBrush, x, 326, dip);
        }

        var cx = Math.Clamp(ScalePos(canvas.CurrentScale), 60, 420);
        dc.DrawLine(MarkPen, new Point(cx, 302), new Point(cx, 330));
        Text(dc, $"{canvas.CurrentScale:0.00}×", 11, Palette[4], cx + 6, 290, dip);
    }

    private static void DrawSmoothAxis(DrawingContext dc, double dip)
    {
        Text(dc, $"Smooth  T = {T:0.#}  Q = {Q:0.#}  S = {S:0.#}", 13, DimBrush, 460, 268, dip);
        dc.DrawLine(ThinPen, new Point(460, 316), new Point(880, 316));
        dc.DrawLine(RowPens[1], new Point(460, 316), new Point(SmoothPos(1 - T), 316));
        dc.DrawLine(RowPens[1], new Point(SmoothPos(1 + T), 316), new Point(880, 316));
        dc.DrawLine(AccentPen, new Point(SmoothPos(1 - T), 316), new Point(SmoothPos(1 + T), 316));

        foreach (var v in new[] { 0.5, 1 - T, 1.0, 1 + T, 1.5 })
        {
            var x = SmoothPos(v);
            dc.DrawLine(ThinPen, new Point(x, 310), new Point(x, 322));
            TextCentered(dc, $"{v:0.#}", 11, TextBrush, x, 326, dip);
        }
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

    private static void Count(DrawingContext dc, string label, string value, double x, double y, double dip)
    {
        Text(dc, label, 14, DimBrush, x, y + 5, dip);
        Text(dc, value, 20, TextBrush, x + CountColumnGap, y, dip);
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

    private static void TextCentered(DrawingContext dc, string s, double size, Brush brush, double centerX, double y, double dip)
    {
        var ft = new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Font, size, brush, dip);
        dc.DrawText(ft, new Point(centerX - ft.Width / 2, y));
    }

    private static void TextRight(DrawingContext dc, string s, double size, Brush brush, double right, double y, double dip)
    {
        var ft = new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Font, size, brush, dip);
        dc.DrawText(ft, new Point(right - ft.Width, y));
    }

    private static string ThresholdText(double threshold2)
    {
        return ThresholdDisabled(threshold2) ? "禁用" : $"{Math.Sqrt(threshold2):0.#} px";
    }

    private static double ThresholdLength(double threshold2)
    {
        return ThresholdDisabled(threshold2) ? 44 : Math.Sqrt(threshold2);
    }

    // 阈值以超出屏幕尺度的极大值或非正值表示关闭（见 Parameters.cs），此时不参与距离比较。
    private static bool ThresholdDisabled(double threshold2)
    {
        return !double.IsFinite(threshold2) || threshold2 < 0 || threshold2 > 1e10;
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
