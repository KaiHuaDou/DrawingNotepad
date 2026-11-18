using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Ink;
using System.Windows.Media;

using DependencyPropertyGenerator;

namespace InkCanvasNext;

/// <summary>
/// 画布当前的工具设置：编辑模式、盖章动作与笔触属性
/// </summary>
public sealed record DrawingAttributesNext
{
    /// <summary>
    /// 编辑模式，默认 Ink
    /// </summary>
    public InkCanvasNextMode Mode { get; init; } = InkCanvasNextMode.Ink;

    /// <summary>
    /// 盖章动作，默认 None
    /// </summary>
    public StampAction Stamp { get; init; } = StampAction.None;

    private DrawingAttributes attributes = CreateDefault( );

    /// <summary>
    /// 笔触属性；读写都是副本
    /// </summary>
    public DrawingAttributes DrawingAttributes
    {
        get => attributes.Clone( );
        init => attributes = value is null ? CreateDefault( ) : value.Clone( );
    }

    /// <summary>
    /// 默认笔触属性
    /// </summary>
    internal static DrawingAttributes CreateDefault( )
    {
        return new DrawingAttributes
        {
            Color = InkCanvasNext.DefaultPenColor,
            Width = 3,
            Height = 3,
            StylusTip = StylusTip.Ellipse
        };
    }

    /// <summary>
    /// 笔触属性逐字段比较
    /// </summary>
    internal static bool AttributesEqual(DrawingAttributes left, DrawingAttributes right)
    {
        return left.Color == right.Color
            && left.Width == right.Width
            && left.Height == right.Height
            && left.StylusTip == right.StylusTip
            && left.StylusTipTransform.Equals(right.StylusTipTransform)
            && left.FitToCurve == right.FitToCurve
            && left.IgnorePressure == right.IgnorePressure
            && left.IsHighlighter == right.IsHighlighter;
    }

    /// <summary>
    /// 按值比较
    /// </summary>
    public bool Equals(DrawingAttributesNext? other)
    {
        return other is not null
            && Mode == other.Mode
            && Stamp == other.Stamp
            && AttributesEqual(attributes, other.attributes);
    }

    /// <summary>
    /// 按值计算哈希
    /// </summary>
    public override int GetHashCode( )
    {
        var hash = new HashCode( );
        hash.Add(Mode);
        hash.Add(Stamp);
        hash.Add(attributes.Color);
        hash.Add(attributes.Width);
        hash.Add(attributes.Height);
        hash.Add(attributes.StylusTip);
        hash.Add(attributes.StylusTipTransform);
        hash.Add(attributes.FitToCurve);
        hash.Add(attributes.IgnorePressure);
        hash.Add(attributes.IsHighlighter);
        return hash.ToHashCode( );
    }
}

[DependencyProperty<DrawingAttributesNext>(
    "DrawingAttributes",
    DefaultValueExpression = "new DrawingAttributesNext( )",
    DefaultBindingMode = DefaultBindingMode.TwoWay,
    Coerce = true,
    PropertyXmlDocumentation =
    """
    <summary>
    当前工具设置
    </summary>
    """
)]
public partial class InkCanvasNext
{
    /// <summary>
    /// 普通画笔默认颜色
    /// </summary>
    public static Color DefaultPenColor { get; } = Color.FromRgb(0xE6, 0xE6, 0xE6);

    /// <summary>
    /// 工具设置变化时触发
    /// </summary>
    public event EventHandler? DrawingAttributesChanged;

    private partial DrawingAttributesNext CoerceDrawingAttributes(DrawingAttributesNext? value)
    {
        var current = DrawingAttributes;
        var normalized = value is null ? new DrawingAttributesNext( ) : Normalize(value);
        return normalized.Equals(current) ? current : normalized;
    }

    partial void OnDrawingAttributesChanged(DrawingAttributesNext oldValue, DrawingAttributesNext newValue)
    {
        ApplyDrawingAttributes(newValue);
        DrawingAttributesChanged?.Invoke(this, EventArgs.Empty);
    }

    private static DrawingAttributesNext Normalize(DrawingAttributesNext value)
    {
        var mode = Enum.IsDefined(value.Mode) ? value.Mode : InkCanvasNextMode.Ink;
        var stamp = Enum.IsDefined(value.Stamp) ? value.Stamp : StampAction.None;

        if (mode is not (InkCanvasNextMode.Ink or InkCanvasNextMode.Select))
        {
            stamp = StampAction.None;
        }

        return mode == value.Mode && stamp == value.Stamp
            ? value
            : value with { Mode = mode, Stamp = stamp };
    }

    private void ApplyDrawingAttributes(DrawingAttributesNext value)
    {
        ApplyEditingMode(value.Mode);

        var attributes = value.DrawingAttributes;
        if (DrawingAttributesNext.AttributesEqual(InnerCanvas.DefaultDrawingAttributes, attributes))
        {
            return;
        }

        InnerCanvas.DefaultDrawingAttributes = attributes;
        InnerCanvas.ObserveDefaultDrawingAttributes( );
    }
}
