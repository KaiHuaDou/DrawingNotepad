using System;
using System.Windows;
using System.Windows.Ink;

using DependencyPropertyGenerator;

namespace InkCanvasNext;

/// <summary>
/// 依赖项属性注册、公共事件与 CLR 属性包装。
/// </summary>
[DependencyProperty<bool>("CanRedo", IsReadOnly = true)]
[DependencyProperty<bool>("CanUndo", IsReadOnly = true)]
[DependencyProperty<InkCanvasNextMode>("Mode", DefaultValue = InkCanvasNextMode.Ink)]
[DependencyProperty<double>(
    "EraserDiameter",
    DefaultValueExpression = "EraserDiameterDefault",
    PropertyXmlDocumentation =
    """
    <summary>
    获取或设置橡皮擦的直径（像素）
    </summary>
    """
)]
[DependencyProperty<MouseWheelAction>("MouseWheelAction", DefaultValue = MouseWheelAction.Scroll)]
[DependencyProperty<StampAction>(
    "StampAction",
    DefaultValue = StampAction.None,
    PropertyXmlDocumentation =
    """
    <summary>
    当前盖章模式（克隆/粘贴）。激活期间单指只落章、原生收笔关闭，多指手势照常
    </summary>
    """
)]

public partial class InkCanvasNext
{
    // 生成器对无默认值的引用类型一律追加 ?，与 Strokes、DefaultDrawingAttributes
    // “构造后必非空”的对外契约不符，二者保持手工注册
    public static readonly DependencyProperty DefaultDrawingAttributesProperty =
        DependencyProperty.Register(
            nameof(DefaultDrawingAttributes),
            typeof(DrawingAttributes),
            typeof(InkCanvasNext),
            new PropertyMetadata(OnDefaultDrawingAttributesChanged));

    public static readonly DependencyProperty StrokesProperty =
        DependencyProperty.Register(
            nameof(Strokes),
            typeof(StrokeCollection),
            typeof(InkCanvasNext),
            new PropertyMetadata(OnStrokesPropertyChanged));

    public event EventHandler<DependencyPropertyChangedEventArgs>? CanRedoChanged;

    public event EventHandler<DependencyPropertyChangedEventArgs>? CanUndoChanged;

    public event EventHandler<InkCanvasStrokesChangedEventArgs>? StrokesChanged;

    public event EventHandler? SelectionChanged;

    /// <summary>
    /// 视口（滚动/缩放）或选区包围盒变化，用于外部工具栏跟随。
    /// </summary>
    public event EventHandler? ViewOrSelectionChanged;

    public DrawingAttributes DefaultDrawingAttributes
    {
        get => (DrawingAttributes) GetValue(DefaultDrawingAttributesProperty);
        set => SetValue(DefaultDrawingAttributesProperty, value);
    }

    public StrokeCollection Strokes
    {
        get => (StrokeCollection) GetValue(StrokesProperty);
        set => SetValue(StrokesProperty, value);
    }

    /// <summary>
    /// 获取或设置画布缩放比例，范围 0.1 ~ 10。
    /// </summary>
    public double CurrentScale
    {
        get => CurrentView.Scale;
        set
        {
            var scale = Math.Clamp(value, MinScale, MaxScale);
            canvasScaleTransform.ScaleX = canvasScaleTransform.ScaleY = scale;
            eraser.Scale = scale;
        }
    }

    public double OffsetX
    {
        get => CanvasScroll.HorizontalOffset;
        set => CanvasScroll.ScrollToHorizontalOffset(value);
    }

    public double OffsetY
    {
        get => CanvasScroll.VerticalOffset;
        set => CanvasScroll.ScrollToVerticalOffset(value);
    }

    /// <summary>
    /// 获取当前视口尺寸（DIP）。
    /// </summary>
    public Size ViewportSize => new(CanvasScroll.ViewportWidth, CanvasScroll.ViewportHeight);
}
