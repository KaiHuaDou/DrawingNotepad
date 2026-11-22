using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Threading;

using InkCanvasNext;

using static InkCanvasNext.InkCanvasNext;

namespace LightBoard;

// 调试模式浮层：参数面板（1:1 半径圆、1:1 距离线、渲染层与渲染模式）居中贴底，运行状态计数与帧率分列左右两侧，右下角为 GC/内存/Dispatcher 运行时指标，边缘自动滚动热区以红色虚线标出。
internal sealed partial class DebugVisual : FrameworkElement
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

    // 运行时指标采样周期：进程内存与 GC 计数器读取代价较高，低于此间隔沿用上次快照
    private const double RuntimeSampleIntervalMs = 500;

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
    private readonly Process process = Process.GetCurrentProcess( );
    private double lastScale;
    private (int Touch, int Undo, int Redo, int Stroke) lastCounts = (-1, -1, -1, -1);
    private (double Rate, double Longest) lastFrames = (-1, -1);
    private CanvasStatus lastStatus;
    private long runtimeSampleTimestamp;
    private RuntimeStatus lastRuntime;
    private long dispatcherProbeTimestamp;
    private double dispatcherLatency;

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

        // 运行时侧栏贴右下角；右侧计数栏据此上移避让，避免窗口较矮时两组文字重叠
        var runtime = BuildRuntime(lastRuntime);
        var runtimeHeight = runtime.Sum(group => StatusTitleHeight + group.Rows.Length * StatusRowHeight + StatusGroupGap) - StatusGroupGap;
        var runtimeTop = Math.Max(SideMargin, RenderSize.Height - SideMargin - runtimeHeight);

        DrawCounts(dc, dip, runtimeTop);
        DrawStatus(dc, dip);
        DrawRuntime(dc, runtime, runtimeTop, dip);
    }

    private void Refresh( )
    {
        var counts = (canvas.ActiveTouchCount, canvas.Position, canvas.RedoDepth, canvas.Strokes.Count);
        frameMeter.Sample(SampleIntervalMs);
        var frames = (frameMeter.Rate, frameMeter.LongestFrame);
        var runtime = SampleRuntime( );
        var status = ReadStatus( );
        if (counts == lastCounts && frames == lastFrames && runtime == lastRuntime && status == lastStatus)
        {
            return;
        }

        lastCounts = counts;
        lastFrames = frames;
        lastRuntime = runtime;
        lastStatus = status;
        InvalidateVisual( );
    }

    // 按固定周期抓取运行时快照；两次采样间隔同时用于换算分配速率
    private RuntimeStatus SampleRuntime( )
    {
        var now = Stopwatch.GetTimestamp( );
        var elapsed = (now - runtimeSampleTimestamp) * 1000.0 / Stopwatch.Frequency;
        if (elapsed < RuntimeSampleIntervalMs)
        {
            return lastRuntime;
        }

        runtimeSampleTimestamp = now;

        // Background 优先级的空工作项从入队到执行的耗时，即 Dispatcher 当前队列积压时长
        dispatcherProbeTimestamp = Stopwatch.GetTimestamp( );
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(MeasureDispatcherLatency));

        process.Refresh( );
        var info = GC.GetGCMemoryInfo( );
        var allocated = GC.GetTotalAllocatedBytes(false);
        var rate = lastRuntime.Allocated > 0
            ? (allocated - lastRuntime.Allocated) / (elapsed / 1000.0) / (1024.0 * 1024.0)
            : 0;
        var pause = info.PauseDurations.Length > 0 ? info.PauseDurations[0].TotalMilliseconds : 0;
        return new(
            GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
            GC.GetTotalMemory(false), info.TotalCommittedBytes, allocated, rate, pause, info.PauseTimePercentage,
            process.WorkingSet64, process.PrivateMemorySize64, process.PeakWorkingSet64,
            Environment.CurrentManagedThreadId, dispatcherLatency);
    }

    private void MeasureDispatcherLatency( )
    {
        dispatcherLatency = (Stopwatch.GetTimestamp( ) - dispatcherProbeTimestamp) * 1000.0 / Stopwatch.Frequency;
    }

    private void DrawCounts(DrawingContext dc, double dip, double runtimeTop)
    {
        var mid = Math.Max(SideMargin, Math.Min(RenderSize.Height / 2, runtimeTop - 40 - 3.5 * CountRowHeight));
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
        const double x = SideMargin;
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

    private static double TextWidth(string s, double size, double dip)
    {
        return new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Font, size, Brushes.Transparent, dip).Width;
    }

    // 运行时侧栏贴在右下角：标签左对齐、数值右对齐到视口边缘，分组样式与左侧状态栏一致
    private void DrawRuntime(DrawingContext dc, StatusGroup[] groups, double top, double dip)
    {
        var labelWidth = groups.SelectMany(group => group.Rows).Max(row => TextWidth(row.Label, StatusFontSize, dip));
        var valueWidth = groups.SelectMany(group => group.Rows).Max(row => TextWidth(row.Value, StatusFontSize, dip));
        var right = RenderSize.Width - SideMargin;
        var x = right - (labelWidth + StatusLabelGap + valueWidth);
        var y = top;

        foreach (var group in groups)
        {
            Text(dc, group.Title, StatusFontSize + 1, TextBrush, x, y + 3, dip);
            dc.DrawLine(ThinPen, new Point(x, y + StatusTitleHeight - 4), new Point(right, y + StatusTitleHeight - 4));
            y += StatusTitleHeight;

            foreach (var row in group.Rows)
            {
                Text(dc, row.Label, StatusFontSize, DimBrush, x, y, dip);
                TextRight(dc, row.Value, StatusFontSize, TextBrush, right, y, dip);
                y += StatusRowHeight;
            }

            y += StatusGroupGap;
        }
    }

    private static StatusGroup[] BuildRuntime(RuntimeStatus runtime)
    {
        return
        [
            new("GC", [
                new("Gen0/1/2", $"{runtime.Gen0} / {runtime.Gen1} / {runtime.Gen2}"),
                new("托管堆", FormatBytes(runtime.ManagedHeap)),
                new("已分配", FormatBytes(runtime.Allocated)),
                new("分配速率", $"{runtime.AllocatedRate:0.0} MB/s"),
                new("最近暂停", $"{runtime.LastPause:0.0} ms"),
                new("暂停占比", $"{runtime.PausePercentage:0.0}%"),
            ]),
            new("内存", [
                new("工作集", FormatBytes(runtime.WorkingSet)),
                new("私有", FormatBytes(runtime.Private)),
                new("峰值工作集", FormatBytes(runtime.PeakWorkingSet)),
                new("GC 提交", FormatBytes(runtime.Committed)),
            ]),
            new("Dispatcher", [
                new("线程", $"{runtime.ThreadId}"),
                new("队列延迟", $"{runtime.Latency:0.0} ms"),
            ]),
        ];
    }

    private static string FormatBytes(long bytes)
    {
        const double Mb = 1024.0 * 1024;
        const double Gb = Mb * 1024;
        return bytes >= Gb ? $"{bytes / Gb:0.00} GB" : $"{bytes / Mb:0.0} MB";
    }

    private static string YesNo(bool value)
    {
        return value ? "是" : "否";
    }

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

    private readonly record struct RuntimeStatus(
        int Gen0, int Gen1, int Gen2,
        long ManagedHeap, long Committed, long Allocated, double AllocatedRate, double LastPause, double PausePercentage,
        long WorkingSet, long Private, long PeakWorkingSet,
        int ThreadId, double Latency);

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
