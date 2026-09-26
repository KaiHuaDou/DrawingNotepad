using System;
using System.Windows;
using System.Windows.Ink;

namespace InkCanvasNext;

/// <summary>
/// 依赖项属性注册、公共事件与 CLR 属性包装。
/// </summary>
public partial class InkCanvasNext
{
    private static readonly DependencyPropertyKey CanRedoPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CanRedo),
            typeof(bool),
            typeof(InkCanvasNext),
            new PropertyMetadata(false));

    private static readonly DependencyPropertyKey CanUndoPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CanUndo),
            typeof(bool),
            typeof(InkCanvasNext),
            new PropertyMetadata(false));

    public static readonly DependencyProperty CanRedoProperty = CanRedoPropertyKey.DependencyProperty;

    public static readonly DependencyProperty CanUndoProperty = CanUndoPropertyKey.DependencyProperty;

    public static readonly DependencyProperty DefaultDrawingAttributesProperty =
        DependencyProperty.Register(
            nameof(DefaultDrawingAttributes),
            typeof(DrawingAttributes),
            typeof(InkCanvasNext),
            new PropertyMetadata(OnDefaultDrawingAttributesChanged));

    public static readonly DependencyProperty ModeProperty =
        DependencyProperty.Register(
            nameof(Mode),
            typeof(InkCanvasNextMode),
            typeof(InkCanvasNext),
            new PropertyMetadata(InkCanvasNextMode.Ink, OnModeChanged));

    public static readonly DependencyProperty EraserDiameterProperty =
        DependencyProperty.Register(
            nameof(EraserDiameter),
            typeof(double),
            typeof(InkCanvasNext),
            new PropertyMetadata(Eraser.DefaultDiameter));

    public static readonly DependencyProperty MouseWheelActionProperty =
        DependencyProperty.Register(
            nameof(MouseWheelAction),
            typeof(MouseWheelAction),
            typeof(InkCanvasNext),
            new PropertyMetadata(MouseWheelAction.Scroll));

    public static readonly DependencyProperty StrokesProperty =
        DependencyProperty.Register(
            nameof(Strokes),
            typeof(StrokeCollection),
            typeof(InkCanvasNext),
            new PropertyMetadata(OnStrokesPropertyChanged));

    public static readonly DependencyProperty StampActionProperty =
        DependencyProperty.Register(
            nameof(StampAction),
            typeof(StampAction),
            typeof(InkCanvasNext),
            new PropertyMetadata(StampAction.None, OnStampActionChanged));

    public event EventHandler<DependencyPropertyChangedEventArgs>? CanRedoChanged;

    public event EventHandler<DependencyPropertyChangedEventArgs>? CanUndoChanged;

    public event EventHandler<InkCanvasStrokesChangedEventArgs>? StrokesChanged;

    public event EventHandler? SelectionChanged;

    /// <summary>
    /// 视口（滚动/缩放）或选区包围盒变化，用于外部工具栏跟随。
    /// </summary>
    public event EventHandler? ViewOrSelectionChanged;

    public bool CanRedo
    {
        get => (bool) GetValue(CanRedoProperty);
        private set => SetValue(CanRedoPropertyKey, value);
    }

    public bool CanUndo
    {
        get => (bool) GetValue(CanUndoProperty);
        private set => SetValue(CanUndoPropertyKey, value);
    }

    public DrawingAttributes DefaultDrawingAttributes
    {
        get => (DrawingAttributes) GetValue(DefaultDrawingAttributesProperty);
        set => SetValue(DefaultDrawingAttributesProperty, value);
    }

    public InkCanvasNextMode Mode
    {
        get => (InkCanvasNextMode) GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    /// <summary>
    /// 获取或设置橡皮擦的直径（像素）。
    /// </summary>
    public double EraserDiameter
    {
        get => (double) GetValue(EraserDiameterProperty);
        set => SetValue(EraserDiameterProperty, value);
    }

    public MouseWheelAction MouseWheelAction
    {
        get => (MouseWheelAction) GetValue(MouseWheelActionProperty);
        set => SetValue(MouseWheelActionProperty, value);
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

    /// <summary>
    /// 当前盖章模式（克隆/粘贴）。激活期间单指只落章、原生收笔关闭，多指手势照常。
    /// </summary>
    public StampAction StampAction
    {
        get => (StampAction) GetValue(StampActionProperty);
        set => SetValue(StampActionProperty, value);
    }
}
