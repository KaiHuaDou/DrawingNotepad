using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using InkCanvasNext;

namespace LightBoard;

internal sealed record PenProfile(
    Color Color,
    double Width,
    bool IsHighlighter
);

internal sealed record HighlighterReturn(
    InkCanvasNextMode Mode,
    RadioButton? ColorRadio,
    RadioButton? ThicknessRadio
);

public partial class MainWindow
{
    internal InkCanvasNextMode Mode { get; private set; } = InkCanvasNextMode.Ink;

    private StampAction stamp = StampAction.None;

    private PenProfile pen = new(Color.FromRgb(0xE6, 0xE6, 0xE6), 3, false);
    private static readonly PenProfile HighlighterProfile = new(Colors.Yellow, 36, true);

    private RadioButton? colorRadio;
    private RadioButton? thicknessRadio;
    private HighlighterReturn? highlighterReturn;

    private Size? selectionBorderSize;
    private bool selectionBorderUpdating;

    private static bool IsToolMode(InkCanvasNextMode m)
    {
        return m switch
        {
            InkCanvasNextMode.EraseStroke or InkCanvasNextMode.EraseArea or InkCanvasNextMode.Select => true,
            _ => false
        };
    }

    private static bool IsShapeMode(InkCanvasNextMode m)
    {
        return m is InkCanvasNextMode.Line or InkCanvasNextMode.Circle;
    }

    private void EnterHighlighter( )
    {
        if (Mode == InkCanvasNextMode.Highlighter)
        {
            return;
        }

        highlighterReturn = new(Mode, colorRadio, thicknessRadio);
        Mode = InkCanvasNextMode.Highlighter;
    }

    private void ExitHighlighter( )
    {
        if (highlighterReturn is { } saved)
        {
            Mode = saved.Mode;
            colorRadio = saved.ColorRadio;
            thicknessRadio = saved.ThicknessRadio;
        }

        // Line/Circle 是 Pen 的附加，退出 Highlighter 时不恢复
        if (IsShapeMode(Mode))
        {
            Mode = InkCanvasNextMode.Ink;
        }
    }

    private void ExitStamp( )
    {
        stamp = StampAction.None;
        SyncToolState( );
    }

    private void HighlighterBoxClicked(object o, RoutedEventArgs e)
    {
        stamp = StampAction.None;

        if (HighlighterToggle.IsChecked == true)
        {
            EnterHighlighter( );
        }
        else
        {
            ExitHighlighter( );
        }

        SyncToolState( );
    }

    private Size MeasureSelectionBorder( )
    {
        SelectionBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = SelectionBorder.DesiredSize;
        // DesiredSize 已含倍率；ActualWidth/ActualHeight 是放大前的尺寸，需补偿到父空间
        if (size.Width <= 0)
        {
            size.Width = SelectionBorder.ActualWidth * toolbarScale;
        }

        if (size.Height <= 0)
        {
            size.Height = SelectionBorder.ActualHeight * toolbarScale;
        }

        return size;
    }

    // 工具态唯一写入口：印章归一 → 画布模式与印章 → 笔属性 → 控件勾选
    private void SyncToolState( )
    {
        if (Mode is not (InkCanvasNextMode.Ink or InkCanvasNextMode.Select))
        {
            stamp = StampAction.None;
        }

        var highlighter = Mode == InkCanvasNextMode.Highlighter;
        var profile = highlighter ? HighlighterProfile : pen;

        // 印章先写：OnModeChanged 的编辑模式判定要读到当前的 StampAction
        CanvasNext.StampAction = stamp;
        CanvasNext.Mode = highlighter ? InkCanvasNextMode.Ink : Mode;

        var attrs = CanvasNext.DefaultDrawingAttributes;
        attrs.Color = profile.Color;
        attrs.Width = attrs.Height = profile.Width;
        attrs.IsHighlighter = profile.IsHighlighter;

        HighlighterToggle?.IsChecked = highlighter;
        CloneButton?.IsChecked = stamp == StampAction.Clone;
        PasteButton?.IsChecked = stamp == StampAction.Paste;

        if (highlighter)
        {
            colorRadio?.IsChecked = false;
            thicknessRadio?.IsChecked = false;
            EraseAreaRadio?.IsChecked = false;
            EraseStrokeRadio?.IsChecked = false;
            SelectRadio?.IsChecked = false;
            LineRadio?.IsChecked = false;
            CircleRadio?.IsChecked = false;
        }
        else
        {
            // 印章激活期间单指落章而非绘制，画笔单选与工具模式同样不勾选
            var penHidden = IsToolMode(Mode) || stamp != StampAction.None;
            colorRadio?.IsChecked = !penHidden;
            thicknessRadio?.IsChecked = !penHidden;
            EraseAreaRadio?.IsChecked = Mode == InkCanvasNextMode.EraseArea;
            EraseStrokeRadio?.IsChecked = Mode == InkCanvasNextMode.EraseStroke;
            SelectRadio?.IsChecked = Mode == InkCanvasNextMode.Select;
            LineRadio?.IsChecked = Mode == InkCanvasNextMode.Line;
            CircleRadio?.IsChecked = Mode == InkCanvasNextMode.Circle;
        }
    }

    /// <summary>
    /// 把选区工具栏吸附到选区包围盒左下角（旋转手柄绘于选区正上方）；下方放不下则移到选区上方/ 随拖动/平移/缩放/滚动实时更新。选区完全移出可视区时隐藏；位置双向夹紧到可视区
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
            var y = visible.Bottom + InkCanvasNext.InkCanvasNext.SelectionToolbarGap;
            if (y + h > vp.Bottom)
            {
                y = visible.Top - h - InkCanvasNext.InkCanvasNext.SelectionToolbarGap;
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
