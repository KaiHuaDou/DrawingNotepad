using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using InkCanvasNext;

namespace LightBoard;

public partial class MainWindow
{
    private sealed record PenProfile(Color Color, double Width, bool IsHighlighter);

    private static readonly PenProfile HighlighterProfile = new(Colors.Yellow, 36, true);

    private RadioButton? colorRadio;
    private ToolSnapshot? highlighterBackup;
    // 显式状态机：Mode 是唯一真值来源
    private InkCanvasNextMode mode = InkCanvasNextMode.Ink;

    private PenProfile pen = new(Color.FromRgb(0xE6, 0xE6, 0xE6), 3, false);
    private Size? selectionBorderSize;
    private bool selectionBorderUpdating;
    private RadioButton? thicknessRadio;

    // 进入 Highlighter 前保存的工具状态（pen 在高亮期间不变，无需保存）；退出时整体还原
    private sealed record ToolSnapshot(InkCanvasNextMode Mode, RadioButton? ColorRadio, RadioButton? ThicknessRadio);

    private static bool IsToolMode(InkCanvasNextMode m)
    {
        return m switch
        {
            InkCanvasNextMode.EraseStroke or InkCanvasNextMode.EraseArea or InkCanvasNextMode.Select => true,
            _ => false
        };
    }

    private void CommitCanvas( )
    {
        var p = mode == InkCanvasNextMode.Highlighter ? HighlighterProfile : pen;
        var drawingAttr = CanvasNext.DefaultDrawingAttributes;
        drawingAttr.Color = p.Color;
        drawingAttr.Width = drawingAttr.Height = p.Width;
        drawingAttr.IsHighlighter = p.IsHighlighter;
        CanvasNext.Mode = mode == InkCanvasNextMode.Highlighter ? InkCanvasNextMode.Ink : mode;
    }

    private void EnterHighlighter( )
    {
        highlighterBackup = new ToolSnapshot(mode, colorRadio, thicknessRadio);
        mode = InkCanvasNextMode.Highlighter;
        SyncToolState( );
    }

    private void ExitHighlighter( )
    {
        if (highlighterBackup is not null)
        {
            mode = highlighterBackup.Mode;
            colorRadio = highlighterBackup.ColorRadio;
            thicknessRadio = highlighterBackup.ThicknessRadio;
        }

        // Line/Circle 是 Pen 的附加，退出 Highlighter 时不恢复
        if (mode is InkCanvasNextMode.Line or InkCanvasNextMode.Circle)
        {
            mode = InkCanvasNextMode.Ink;
        }
    }

    private void ExitStamp( )
    {
        CanvasNext.StampAction = StampAction.None;

        // XAML 加载期可能先于按钮创建触发（DefaultThicknessRadio 的 Checked 事件），需判空
        CloneButton?.IsChecked = false;
        PasteButton?.IsChecked = false;
    }

    private void HighLighterBoxClicked(object o, RoutedEventArgs e)
    {
        ExitStamp( );

        if (HighLighterToggle.IsChecked == true)
        {
            EnterHighlighter( );
        }
        else
        {
            ExitHighlighter( );
        }
    }
    private Size MeasureSelectionBorder( )
    {
        SelectionBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = SelectionBorder.DesiredSize;
        if (size.Width <= 0)
        {
            size.Width = SelectionBorder.ActualWidth;
        }

        if (size.Height <= 0)
        {
            size.Height = SelectionBorder.ActualHeight;
        }

        return size;
    }

    // 状态机唯一出口：从 Mode 推导 UI 勾选与画布模式
    private void SyncToolState( )
    {
        var hightlighter = mode == InkCanvasNextMode.Highlighter;
        HighLighterToggle?.IsChecked = hightlighter;

        if (hightlighter)
        {
            colorRadio?.IsChecked = false;
            thicknessRadio?.IsChecked = false;
            EraseAreaRadio?.IsChecked = false;
            EraseStrokeRadio?.IsChecked = false;
            SelectRadio?.IsChecked = false;
            LineToggle?.IsChecked = false;
            CircleToggle?.IsChecked = false;
        }
        else
        {
            colorRadio?.IsChecked = true;
            thicknessRadio?.IsChecked = true;
            EraseAreaRadio?.IsChecked = mode == InkCanvasNextMode.EraseArea;
            EraseStrokeRadio?.IsChecked = mode == InkCanvasNextMode.EraseStroke;
            SelectRadio?.IsChecked = mode == InkCanvasNextMode.Select;
            LineToggle?.IsChecked = mode == InkCanvasNextMode.Line;
            CircleToggle?.IsChecked = mode == InkCanvasNextMode.Circle;
        }

        CommitCanvas( );
    }
    /// <summary>
    /// 把选区工具栏吸附到选区包围盒左下角（旋转手柄绘于选区正上方）；下方放不下则移到选区上方。
    /// 随拖动/平移/缩放/滚动实时更新。选区完全移出可视区时隐藏；位置双向夹紧到可视区。
    /// </summary>
    private void UpdateSelectionBorderPosition( )
    {
        if (selectionBorderUpdating)
        {
            return;
        }

        selectionBorderUpdating = true;
        try
        {
            if (SelectionBorder.Parent is not UIElement parent)
            {
                return;
            }

            var bounds = CanvasNext.GetSelectionScreenBounds(parent);
            var viewport = CanvasNext.GetCanvasViewportBounds(parent);
            if (bounds is not Rect b || viewport is not Rect vp)
            {
                SelectionBorder.Visibility = Visibility.Collapsed;
                return;
            }

            // 只保留选区在可视区内的部分；完全不可见则隐藏工具栏（避免贴边/残影）
            var visible = b;
            visible.Intersect(vp);
            if (visible.IsEmpty)
            {
                SelectionBorder.Visibility = Visibility.Collapsed;
                return;
            }

            SelectionBorder.Visibility = Visibility.Visible;

            var size = selectionBorderSize ??= MeasureSelectionBorder( );
            var w = size.Width;
            var h = size.Height;

            // 吸附到选区可见部分左下角；下方放不下则移到选区上方
            var x = visible.Left;
            var y = visible.Bottom + SelectionVisual.ToolbarGapFromSelection;
            if (y + h > vp.Bottom)
            {
                y = visible.Top - h - SelectionVisual.ToolbarGapFromSelection;
            }

            // 双向夹紧到可视区，保证工具栏完全可见
            x = Math.Clamp(x, vp.Left, Math.Max(vp.Left, vp.Right - w));
            y = Math.Clamp(y, vp.Top, Math.Max(vp.Top, vp.Bottom - h));

            SelectionBorder.Margin = new Thickness(x, y, 0, 0);
        }
        finally
        {
            selectionBorderUpdating = false;
        }
    }
}
