using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;

using InkCanvasNext;

namespace LightBoard;

public partial class MainWindow
{
    private RadioButton? colorRadio;
    private RadioButton? thicknessRadio;

    private DrawingAttributes pen = DrawingAttributesNext.CreateDefault( );

    private Size? selectionBorderSize;
    private bool selectionBorderUpdating;

    private readonly TranslateTransform selectionBorderOffset = new( );
    private double selectionBorderX = double.NaN;
    private double selectionBorderY;

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

    private void HighlighterBoxClicked(object o, RoutedEventArgs e)
    {
        var attributes = HighlighterToggle.IsChecked == true ? CreateHighlighter(pen) : pen;
        CanvasNext.DrawingAttributes = CanvasNext.DrawingAttributes with
        {
            DrawingAttributes = attributes,
            Mode = InkCanvasNextMode.Ink,
            Stamp = StampAction.None
        };
    }

    private static DrawingAttributes CreateHighlighter(DrawingAttributes source)
    {
        var attributes = source.Clone( );
        attributes.Color = Colors.Yellow;
        attributes.Width = attributes.Height = 36;
        attributes.StylusTip = StylusTip.Ellipse;
        attributes.IgnorePressure = true;
        attributes.IsHighlighter = true;
        return attributes;
    }

    /// <summary>
    /// 曲线拟合直接写在笔触属性上：例行笔与当前工具同步，下一笔生效
    /// </summary>
    internal bool FitToCurve
    {
        get => pen.FitToCurve;
        set
        {
            pen.FitToCurve = value;

            var attributes = CanvasNext.DrawingAttributes.DrawingAttributes;
            if (attributes.FitToCurve == value)
            {
                return;
            }

            attributes.FitToCurve = value;
            CanvasNext.DrawingAttributes = CanvasNext.DrawingAttributes with { DrawingAttributes = attributes };
        }
    }

    private void CanvasNextDrawingAttributesChanged(object? o, EventArgs e)
    {
        RefreshToolChecks( );
        TrackToolIdentity( );
        UpdateRevertLabel( );
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

    private void RefreshToolChecks( )
    {
        var tool = CanvasNext.DrawingAttributes;
        var highlighter = tool.DrawingAttributes.IsHighlighter;
        // 荧光笔、工具模式与印章激活期间单指不落画笔，画笔单选不勾选
        var penHidden = highlighter || IsToolMode(tool.Mode) || tool.Stamp != StampAction.None;

        HighlighterToggle?.IsChecked = highlighter;
        CloneButton?.IsChecked = tool.Stamp == StampAction.Clone;
        PasteButton?.IsChecked = tool.Stamp == StampAction.Paste;
        colorRadio?.IsChecked = !penHidden;
        thicknessRadio?.IsChecked = !penHidden;
        EraseAreaRadio?.IsChecked = tool.Mode == InkCanvasNextMode.EraseArea;
        EraseStrokeRadio?.IsChecked = tool.Mode == InkCanvasNextMode.EraseStroke;
        SelectRadio?.IsChecked = tool.Mode == InkCanvasNextMode.Select;
        LineRadio?.IsChecked = tool.Mode == InkCanvasNextMode.Line;
        CircleRadio?.IsChecked = tool.Mode == InkCanvasNextMode.Circle;
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

            // 渲染变换只影响合成，不触发测量与排列；不足半个 DIP 的抖动不写
            if (Math.Abs(x - selectionBorderX) < 0.5 && Math.Abs(y - selectionBorderY) < 0.5)
            {
                return;
            }

            selectionBorderX = x;
            selectionBorderY = y;
            selectionBorderOffset.X = x;
            selectionBorderOffset.Y = y;
        }
        finally
        {
            selectionBorderUpdating = false;
        }
    }

    private double toolbarScale = LightBoardSettings.ToolbarScaleDefault;

    private void ApplyToolbarScale( )
    {
        toolbarScale = SettingsStore.Current.EffectiveToolbarScale;
        if (toolbarScale == 1)
        {
            return;
        }

        var transform = new ScaleTransform(toolbarScale, toolbarScale);
        transform.Freeze( );

        LeftBorder.LayoutTransform = transform;
        CenterBorder.LayoutTransform = transform;
        RightBorder.LayoutTransform = transform;
        SelectionBorder.LayoutTransform = transform;

        // 缓存的是旧倍率下量得的尺寸
        selectionBorderSize = null;
    }

    /// <summary>
    /// 底侧边缘提示按钮的让位：工具栏高度是本地尺寸，屏幕高度要乘倍率
    /// </summary>
    internal static double EdgeHintGap(double barHeight, double barMarginBottom, double scale)
    {
        return barHeight * scale + barMarginBottom + 9;
    }
}
