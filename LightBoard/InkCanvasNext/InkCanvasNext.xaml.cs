using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;

namespace InkCanvasNext;

public enum InkCanvasNextMode
{
    Ink,
    EraseStroke,
    EraseArea,
    Select,
    Line,
    Circle,
    Highlighter
}

public enum StampAction
{
    None,
    Clone,
    Paste
}

public enum MouseWheelAction
{
    Scroll,
    Zoom,
    None
}
public partial class InkCanvasNext : UserControl
{
#pragma warning disable IDE1006

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

#pragma warning restore IDE1006

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
            new PropertyMetadata(50.0));

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

    private readonly SelectionController selection;

    public InkCanvasNext( )
    {
        InitializeComponent( );

        eraser = new Eraser(Canvas, EraserFeedback);

        CanvasGrid.LayoutTransform = canvasScaleTransform;

        selection = new SelectionController(this, new SelectionVisual(SelectionLayer));

        Canvas.Strokes.StrokesChanged += OnStrokesChanged;

        Strokes = Canvas.Strokes;
        DefaultDrawingAttributes = Canvas.DefaultDrawingAttributes;

        prevMode = InkCanvasNextMode.Ink;

#if DEBUG
        const double DistanceThresholdFactor = 0.9;
#else
        const double DistanceThresholdFactor = 0.1;
#endif

        var distanceThreshold = DistanceThresholdFactor * SystemParameters.WorkArea.Width;
        distanceThreshold2 = distanceThreshold * distanceThreshold;

        CanvasScroll.ScrollToHorizontalOffset(8192);
        CanvasScroll.ScrollToVerticalOffset(8192);
        CanvasScroll.ScrollChanged += (_, _) => RaiseViewOrSelectionChanged( );
        canvasScaleTransform.Changed += (_, _) => RaiseViewOrSelectionChanged( );

        Canvas.Children.Add(multiTouchCanvas);

        SetupShapePreview( );
    }

    public event EventHandler<DependencyPropertyChangedEventArgs>? CanRedoChanged;

    public event EventHandler<DependencyPropertyChangedEventArgs>? CanUndoChanged;

    public event EventHandler<InkCanvasStrokesChangedEventArgs>? StrokesChanged;

    public event EventHandler? SelectionChanged;

    /// <summary>视口（滚动/缩放）或选区包围盒变化，用于外部工具栏跟随。</summary>
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

    private const double MinScale = 0.1;
    private const double MaxScale = 10.0;

    public double CurrentScale
    {
        get => currentScale;
        set
        {
            var clamped = Math.Clamp(value, MinScale, MaxScale);
            currentScale = clamped;
            canvasScaleTransform.ScaleX = canvasScaleTransform.ScaleY = clamped;
            eraser.Scale = clamped;
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

    /// <summary>当前盖章模式（克隆/粘贴）；由外部按钮 toggle，本控件只读消费。</summary>
    public StampAction StampAction { get; set; } = StampAction.None;

#pragma warning disable CA1822 // 无实例依赖，但类名与命名空间同名，静态访问在外部会与命名空间冲突，保持实例属性
    /// <summary>当前剪贴板是否含可粘贴的墨迹数据。</summary>
    public bool HasClipboardStrokes => Clipboard.ContainsData(StrokeCollection.InkSerializedFormat);
#pragma warning restore CA1822

    /// <summary>
    /// 把选区包围盒（内容坐标）变换到 <paramref name="relativeTo"/> 坐标系返回；无选区或不可用返回 null。
    /// 用于选区工具栏跟随定位。
    /// </summary>
    public Rect? GetSelectionScreenBounds(Visual relativeTo)
    {
        var bounds = selection.Bounds;
        if (bounds.IsEmpty)
        {
            return null;
        }

        try
        {
            var transform = Canvas.TransformToVisual(relativeTo);
            return new Rect(transform.Transform(bounds.TopLeft), transform.Transform(bounds.BottomRight));
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// 返回画布当前可见视口在 <paramref name="relativeTo"/> 坐标系下的矩形；
    /// 用于选区工具栏的可见性判断与位置夹紧。
    /// </summary>
    public Rect? GetCanvasViewportBounds(UIElement relativeTo)
    {
        try
        {
            var topLeft = CanvasScroll.TranslatePoint(new Point(0, 0), relativeTo);
            var bottomRight = CanvasScroll.TranslatePoint(
                new Point(CanvasScroll.ViewportWidth, CanvasScroll.ViewportHeight),
                relativeTo);
            return new Rect(topLeft, bottomRight);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>供 internal 协作对象（SelectionController 等）回调，外部消费者请订阅对应事件。</summary>
    internal void RaiseSelectionChanged( )
    {
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void RaiseViewOrSelectionChanged( )
    {
        ViewOrSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void OnDefaultDrawingAttributesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is DrawingAttributes attributes)
        {
            (d as InkCanvasNext)?.Canvas.DefaultDrawingAttributes = attributes;
        }
    }

    private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        (d as InkCanvasNext)?.ApplyEditingMode((InkCanvasNextMode) e.NewValue);
    }

    private static void OnStrokesPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        (d as InkCanvasNext)?.ApplyStrokes(e.NewValue as StrokeCollection);
    }

    private void ApplyEditingMode(InkCanvasNextMode mode)
    {
        prevMode = mode;
        if (state != TouchState.Idle)
        {
            return;
        }

        ApplyModeToEditing(mode);
    }

    /// <summary>当前工具是否需要在 EvalDraw/Draw 期间抢先捕获触点（区域擦除需要，Ink 走原生不需要）。
    /// 手势层（SetState）经由这一唯一接缝感知工具差异，勿在状态机内直接引用具体模式。</summary>
    private bool WantsPreemptiveDrawCapture( )
    {
        return Mode == InkCanvasNextMode.EraseArea;
    }

    private void ApplyModeToEditing(InkCanvasNextMode mode)
    {
        switch (mode)
        {
            case InkCanvasNextMode.Ink:
            case InkCanvasNextMode.Highlighter:
                Canvas.EditingMode = InkCanvasEditingMode.Ink;
                ConfigureSelectMode(false);
                break;
            case InkCanvasNextMode.EraseStroke:
                Canvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
                ConfigureSelectMode(false);
                break;
            case InkCanvasNextMode.EraseArea:
            case InkCanvasNextMode.Line:
            case InkCanvasNextMode.Circle:
                Canvas.EditingMode = InkCanvasEditingMode.None;
                ConfigureSelectMode(false);
                break;
            case InkCanvasNextMode.Select:
                // 关键：关闭 WPF 内置选择，改用自绘选择层
                Canvas.EditingMode = InkCanvasEditingMode.None;
                ConfigureSelectMode(true);
                break;
        }
    }

    /// <summary>切换选择视觉的激活状态；离开 Select 时清空选型并取消进行中的手势。</summary>
    private void ConfigureSelectMode(bool inSelect)
    {
        if (inSelect)
        {
            selection.Invalidate( );
        }
        else
        {
            CancelSelectionGesture( );
            selection.Clear( );
        }
    }

    private void ApplyStrokes(StrokeCollection? strokes)
    {
        var newStrokes = strokes ?? [];

        Canvas.Strokes.StrokesChanged -= OnStrokesChanged;
        Canvas.Strokes = newStrokes;
        Canvas.Strokes.StrokesChanged += OnStrokesChanged;

        Strokes = newStrokes;

        selection.Clear( );
        StampAction = StampAction.None;

        ClearHistory( );
    }

    public void SetDocumentPage(ImageSource? page)
    {
        DocumentHost.Child = page is null ? null : new Image { Source = page };
        DocumentHost.Visibility = page is null ? Visibility.Collapsed : Visibility.Visible;
    }
}
