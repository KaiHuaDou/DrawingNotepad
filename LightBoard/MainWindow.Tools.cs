using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;

using InkCanvasNext;

namespace LightBoard;

// 工具状态：画笔预设、颜色/粗细、荧光笔、曲线拟合与形状/橡皮/选择模式、粘贴与克隆印章
public partial class MainWindow
{
    private RadioButton? colorRadio;
    private RadioButton? thicknessRadio;

    private DrawingAttributes pen = DrawingAttributesNext.CreateDefault( );

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

    private void CloneClick(object o, RoutedEventArgs e)
    {
        if (CloneButton.IsChecked == true)
        {
            // 无选区时不改动印章：已有粘贴保持激活
            if (CanvasNext.HasSelection)
            {
                CanvasNext.DrawingAttributes = CanvasNext.DrawingAttributes with { Stamp = StampAction.Clone };
            }
            else
            {
                RefreshToolChecks( );
            }
        }
        else if (CanvasNext.DrawingAttributes.Stamp == StampAction.Clone)
        {
            CanvasNext.DrawingAttributes = CanvasNext.DrawingAttributes with { Stamp = StampAction.None };
        }
        else
        {
            RefreshToolChecks( );
        }
    }

    private void ColorRadioChecked(object o, RoutedEventArgs e)
    {
        if (o is not RadioButton { Background: SolidColorBrush brush } radio)
        {
            return;
        }

        var tool = CanvasNext.DrawingAttributes;

        // 形状模式下点当前已选中的颜色即取消形状回画笔；点其他颜色仅换笔保持形状
        if (IsShapeMode(tool.Mode) && ReferenceEquals(radio, colorRadio))
        {
            CanvasNext.DrawingAttributes = tool with
            {
                Mode = InkCanvasNextMode.Ink,
                Stamp = StampAction.None,
                DrawingAttributes = pen
            };
            return;
        }

        // 退出荧光笔时从普通笔预设起改，避免带上高亮的 36 DIP 与黄色
        var attributes = tool.DrawingAttributes.IsHighlighter ? pen.Clone( ) : tool.DrawingAttributes;
        attributes.Color = brush.Color;
        attributes.IsHighlighter = false;
        attributes.IgnorePressure = false;
        pen = attributes;

        colorRadio = radio;
        CanvasNext.DrawingAttributes = tool with
        {
            DrawingAttributes = attributes,
            Stamp = StampAction.None,
            Mode = IsToolMode(tool.Mode) ? InkCanvasNextMode.Ink : tool.Mode
        };
    }

    private void EraseAll(object o, RoutedEventArgs e)
    {
        CanvasNext.DrawingAttributes = CanvasNext.DrawingAttributes with { Stamp = StampAction.None };

        CanvasNext.ClearMultiTouchVisuals( );
        CanvasNext.Strokes.Clear( );
    }

    // 工具（擦除/选择）与形状（线/圆）是同一排他单选组（EditGroup）：
    // 点选即激活；再点当前已激活的选项回退到画笔
    private void ModeRadioChecked(object o, RoutedEventArgs e)
    {
        if (o is not RadioButton radio)
        {
            return;
        }

        // 再点当前已激活的模式即回画笔；荧光笔期间模式停在绘制，点任何模式都算新激活
        var wasMode = CanvasNext.DrawingAttributes.Mode;

        var mode = radio.Tag switch
        {
            "\uE73C" => InkCanvasNextMode.Line,
            "\uEA3A" => InkCanvasNextMode.Circle,
            "\uED60" => InkCanvasNextMode.EraseArea,
            "\uED61" => InkCanvasNextMode.EraseStroke,
            "\uEF20" => InkCanvasNextMode.Select,
            _ => InkCanvasNextMode.Ink
        };

        CanvasNext.DrawingAttributes = CanvasNext.DrawingAttributes with
        {
            Mode = wasMode == mode ? InkCanvasNextMode.Ink : mode,
            Stamp = StampAction.None,
            DrawingAttributes = pen
        };
    }

    private void PasteClick(object o, RoutedEventArgs e)
    {
        if (PasteButton.IsChecked == true)
        {
            if (Clipboard.ContainsData(StrokeCollection.InkSerializedFormat))
            {
                // 激活粘贴印章时退出其他模式回画笔
                CanvasNext.DrawingAttributes = CanvasNext.DrawingAttributes with
                {
                    DrawingAttributes = pen,
                    Stamp = StampAction.Paste,
                    Mode = InkCanvasNextMode.Ink
                };
            }
            else
            {
                RefreshToolChecks( );
            }
        }
        else if (CanvasNext.DrawingAttributes.Stamp == StampAction.Paste)
        {
            CanvasNext.DrawingAttributes = CanvasNext.DrawingAttributes with { Stamp = StampAction.None };
        }
        else
        {
            RefreshToolChecks( );
        }
    }

    private void ThicknessRadioClick(object o, RoutedEventArgs e)
    {
        if (o is not RadioButton { MinWidth: double thickness } radio)
        {
            return;
        }

        var tool = CanvasNext.DrawingAttributes;

        // 形状模式下点当前已选中的粗细即取消形状回画笔；点其他粗细仅换笔保持形状
        if (IsShapeMode(tool.Mode) && ReferenceEquals(radio, thicknessRadio))
        {
            CanvasNext.DrawingAttributes = tool with
            {
                Mode = InkCanvasNextMode.Ink,
                Stamp = StampAction.None,
                DrawingAttributes = pen
            };
            return;
        }

        // 退出荧光笔时从普通笔预设起改，避免带上高亮的 36 DIP 与黄色
        var attributes = tool.DrawingAttributes.IsHighlighter ? pen.Clone( ) : tool.DrawingAttributes;
        attributes.Width = attributes.Height = thickness;
        attributes.IsHighlighter = false;
        attributes.IgnorePressure = false;
        pen = attributes;

        thicknessRadio = radio;
        CanvasNext.DrawingAttributes = tool with
        {
            DrawingAttributes = attributes,
            Stamp = StampAction.None,
            Mode = IsToolMode(tool.Mode) ? InkCanvasNextMode.Ink : tool.Mode
        };
    }
}
