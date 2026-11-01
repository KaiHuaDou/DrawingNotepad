using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace LightBoard;

internal sealed record HelpNote(FrameworkElement Target, string Text);

internal sealed class HelpVisual : FrameworkElement
{
    private const double ArrowGap = 16;
    private const double ArrowHead = 8;
    private const double ArrowSpread = 0.42;
    private const double EdgeMargin = 12;
    private const double LabelFontSize = 15;
    private const double LabelGap = 6;
    private const double LabelHeight = 26;
    private const double LabelPadding = 9;
    private const double LabelRadius = 6;
    private const double NoteFontSize = 17;
    private const double NoteLineHeight = 30;
    private const double NotePaddingX = 22;
    private const double NotePaddingY = 14;
    private const double RowGap = 6;
    private const double SampleIntervalMs = 100;

    private static readonly Pen ArrowPen = MakePen("#B3E6E6E6", 1.5);
    private static readonly Typeface Font = new("Segoe UI");

    private static readonly Brush LabelBrush = MakeBrush("#E61A1F2A");
    private static readonly Pen LabelPen = MakePen("#33FFFFFF", 1);
    private static readonly Brush NoteBrush = MakeBrush("#CC1A1F2A");
    private static readonly Brush TextBrush = MakeBrush("#FFE6E6E6");
    private readonly HelpNote[][] clusters;
    private readonly string[] noteLines;
    private readonly FrameworkElement root;
    private readonly DispatcherTimer sampleTimer;
    private Rect[] lastTargets = [];

    internal HelpVisual(FrameworkElement root, HelpNote[][] clusters, string[] noteLines)
    {
        this.root = root;
        this.clusters = clusters;
        this.noteLines = noteLines;

        // 箭头的落点是工具栏上的按钮，标注层要盖在工具栏之上；压暗层另在 HelpLayer 里，留在工具栏之下
        Panel.SetZIndex(this, 64);

        SizeChanged += (_, _) => InvalidateVisual( );

        // 控件位置没有对应事件：工具栏展开、收起与选区工具条都靠位移变化，只能定点采样重绘
        sampleTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(SampleIntervalMs),
            DispatcherPriority.Normal,
            (_, _) => Refresh( ),
            Dispatcher.CurrentDispatcher
        );

        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                Refresh( );
                sampleTimer.Start( );
            }
            else
            {
                sampleTimer.Stop( );
            }
        };
    }

    protected override void OnRender(DrawingContext dc)
    {
        var dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        DrawNote(dc, dip);

        foreach (var cluster in clusters)
        {
            DrawCluster(dc, cluster, dip);
        }
    }

    private static void Arrow(DrawingContext dc, Point from, Point to)
    {
        dc.DrawLine(ArrowPen, from, to);

        var angle = Math.Atan2(to.Y - from.Y, to.X - from.X);
        Head(dc, to, angle - ArrowSpread);
        Head(dc, to, angle + ArrowSpread);
    }

    private static void Head(DrawingContext dc, Point tip, double angle)
    {
        dc.DrawLine(ArrowPen, tip, new Point(tip.X - ArrowHead * Math.Cos(angle), tip.Y - ArrowHead * Math.Sin(angle)));
    }

    private static SolidColorBrush MakeBrush(string hex)
    {
        var brush = new SolidColorBrush((Color) ColorConverter.ConvertFromString(hex));
        brush.Freeze( );
        return brush;
    }

    private static Pen MakePen(string hex, double thickness)
    {
        var pen = new Pen(MakeBrush(hex), thickness);
        pen.Freeze( );
        return pen;
    }

    private static FormattedText Text(string s, double size, double dip)
    {
        return new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Font, size, TextBrush, dip);
    }

    private void DrawCluster(DrawingContext dc, HelpNote[] notes, double dip)
    {
        var viewport = new Rect(RenderSize);
        var items = new List<(Rect Target, FormattedText Text)>( );
        foreach (var note in notes)
        {
            var target = TargetRect(note.Target);
            if (target.IsEmpty || !viewport.Contains(target))
            {
                continue;
            }

            items.Add((target, Text(note.Text, LabelFontSize, dip)));
        }

        if (items.Count == 0)
        {
            return;
        }

        items.Sort((a, b) => a.Target.Left.CompareTo(b.Target.Left));
        var top = items.Min(item => item.Target.Top);
        var rowRight = new List<double>( );
        var chips = new List<(Rect Chip, Rect Target, FormattedText Text)>( );
        foreach (var item in items)
        {
            var width = Math.Ceiling(item.Text.Width) + LabelPadding * 2;
            var left = Math.Clamp(
                item.Target.Left + item.Target.Width / 2 - width / 2,
                EdgeMargin,
                Math.Max(EdgeMargin, RenderSize.Width - EdgeMargin - width)
            );

            // 相邻标签挤不进同一行时向上叠一行；箭头先画，与同排标签重叠的部分被标签盖住
            var row = 0;
            while (row < rowRight.Count && left < rowRight[row] + LabelGap)
            {
                row++;
            }

            if (row == rowRight.Count)
            {
                rowRight.Add(0);
            }

            rowRight[row] = left + width;
            var bottom = top - ArrowGap - row * (LabelHeight + RowGap);
            chips.Add((new Rect(left, bottom - LabelHeight, width, LabelHeight), item.Target, item.Text));
        }

        foreach (var (chip, target, _) in chips)
        {
            Arrow(dc, new Point(chip.Left + chip.Width / 2, chip.Bottom), new Point(target.Left + target.Width / 2, target.Top - 2));
        }

        foreach (var (chip, _, text) in chips)
        {
            dc.DrawRoundedRectangle(LabelBrush, LabelPen, chip, LabelRadius, LabelRadius);
            dc.DrawText(text, new Point(chip.Left + LabelPadding, chip.Top + (chip.Height - text.Height) / 2));
        }
    }

    private void DrawNote(DrawingContext dc, double dip)
    {
        if (noteLines.Length == 0)
        {
            return;
        }

        var texts = noteLines.Select(line => Text(line, NoteFontSize, dip)).ToArray( );
        var width = texts.Max(text => text.Width) + NotePaddingX * 2;
        var height = texts.Length * NoteLineHeight + NotePaddingY * 2;
        var box = new Rect((RenderSize.Width - width) / 2, RenderSize.Height * 0.3, width, height);
        dc.DrawRoundedRectangle(NoteBrush, LabelPen, box, 12, 12);

        var y = box.Top + NotePaddingY;
        foreach (var text in texts)
        {
            dc.DrawText(text, new Point(box.Left + (width - text.Width) / 2, y));
            y += NoteLineHeight;
        }
    }

    private void Refresh( )
    {
        var targets = clusters
            .SelectMany(cluster => cluster)
            .Select(note => TargetRect(note.Target))
            .ToArray( );
        if (targets.SequenceEqual(lastTargets))
        {
            return;
        }

        lastTargets = targets;
        InvalidateVisual( );
    }

    // 被折叠、隐藏或移出视口的控件不标注
    private Rect TargetRect(FrameworkElement target)
    {
        if (!target.IsVisible || target.ActualWidth <= 0 || target.ActualHeight <= 0)
        {
            return Rect.Empty;
        }

        var rect = target.TransformToAncestor(root).TransformBounds(new Rect(target.RenderSize));
        if (rect.Height < target.ActualHeight / 2)
        {
            return Rect.Empty;
        }

        var origin = TransformToAncestor(root).Transform(new Point(0, 0));
        rect.Offset(-origin.X, -origin.Y);
        return rect;
    }
}

public partial class MainWindow
{
    private bool moreExpandedByHelp;

    private HelpVisual CreateHelpVisual( )
    {
        // 掌擦触点数在设置中二选一：并拢平移的可用指位与掌擦起点随之变化
        var (panText, eraseText) = SettingsStore.Current.PalmEraseFingers == InkCanvasNext.InkCanvasNext.PalmEraseFingersMin
            ? ("三指", "四指")
            : ("三/四指", "五指");

        return new HelpVisual(
            MainGrid,
            [
                [
                    new(ExitButton, "关闭"),
                    new(WindowMinimizeButton, "最小化"),
                    new(MoreButton, "文件"),
                    new(BackgroundButton, "背景"),
                    new(SwitchOutButton, "切换窗口"),
                ],
                [
                    // 主工具栏两行同组：标签都排在工具栏上方，下排按钮的箭头跨过上排
                    new(DefaultThicknessRadio, "粗细"),
                    new(LineRadio, "直线"),
                    new(CircleRadio, "圆"),
                    new(SelectRadio, "选择"),
                    new(PasteButton, "粘贴"),
                    new(DefaultColorRadio, "颜色（点选即绘图）"),
                    new(HighlighterToggle, "荧光笔（仅黄色）"),
                    new(EraseAreaRadio, "橡皮"),
                    new(EraseAllButton, "清屏"),
                    new(UndoButton, "撤销"),
                    new(RedoButton, "重做"),
                    new(MoreToggle, "展开 / 收起"),
                ],
                [
                    new(CollapseExpandButton, "收起工具栏"),
                    new(MenuButton, "菜单"),
                    new(PrevPageButton, "上一页"),
                    new(AllPageToggle, "页码（点击预览）"),
                    new(NewNextPageButton, "下一页"),
                ],
                [
                    new(CopyButton, "复制"),
                    new(CutButton, "剪切"),
                    new(DeleteButton, "删除"),
                    new(CloneButton, "克隆（点击后在目标位置图章）"),
                ],
            ],
            [
                $"单指绘图，两指平移缩放，{panText}平移",
                $"{eraseText}及以上橡皮擦；手指间最大距离超过阈值时转多指书写",
                "已选中的按钮再次点击可取消",
            ]
        );
    }

    private void HelpClick(object o, RoutedEventArgs e)
    {
        var isChecked = HelpMenu.IsChecked;

        // 标注覆盖主工具栏两行，展开后才有上排按钮；关闭帮助时还原打开前的展开状态
        if (isChecked)
        {
            moreExpandedByHelp = MoreToggle.IsChecked != true;
            if (moreExpandedByHelp)
            {
                ApplyMoreToggle(true);
            }
        }
        else if (moreExpandedByHelp)
        {
            moreExpandedByHelp = false;
            if (MoreToggle.IsChecked == true)
            {
                ApplyMoreToggle(false);
            }
        }

        HelpLayer.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;
    }
}
