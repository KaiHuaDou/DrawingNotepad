using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;

namespace InkCanvasNext;

/// <summary>
/// 画布编辑工具模式。
/// </summary>
public enum InkCanvasNextMode
{
    /// <summary>
    /// 使用默认笔触绘制墨迹。
    /// </summary>
    Ink,
    /// <summary>
    /// 按笔画擦除墨迹。
    /// </summary>
    EraseStroke,
    /// <summary>
    /// 按区域擦除墨迹。
    /// </summary>
    EraseArea,
    /// <summary>
    /// 选择墨迹笔画。
    /// </summary>
    Select,
    /// <summary>
    /// 绘制直线。
    /// </summary>
    Line,
    /// <summary>
    /// 绘制圆形。
    /// </summary>
    Circle,
    /// <summary>
    /// 使用高亮笔触绘制墨迹。
    /// </summary>
    Highlighter
}

/// <summary>
/// 盖章动作（克隆选区或粘贴剪贴板墨迹）。
/// </summary>
public enum StampAction
{
    /// <summary>
    /// 无盖章动作。
    /// </summary>
    None,
    /// <summary>
    /// 克隆选区墨迹。
    /// </summary>
    Clone,
    /// <summary>
    /// 粘贴剪贴板中的墨迹。
    /// </summary>
    Paste
}

/// <summary>
/// 鼠标滚轮动作模式。
/// </summary>
public enum MouseWheelAction
{
    /// <summary>
    /// 滚动画布。
    /// </summary>
    Scroll,
    /// <summary>
    /// 缩放画布。
    /// </summary>
    Zoom,
    /// <summary>
    /// 忽略鼠标滚轮。
    /// </summary>
    None
}

/// <summary>
/// 支持绘制、擦除、选择、直线、圆形、高亮等模式的墨迹画布控件。
/// </summary>
public partial class InkCanvasNext : UserControl
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

    /// <summary>
    /// 标识 CanRedo 依赖项属性。
    /// </summary>
    public static readonly DependencyProperty CanRedoProperty = CanRedoPropertyKey.DependencyProperty;

    /// <summary>
    /// 标识 CanUndo 依赖项属性。
    /// </summary>
    public static readonly DependencyProperty CanUndoProperty = CanUndoPropertyKey.DependencyProperty;

    /// <summary>
    /// 标识 DefaultDrawingAttributes 依赖项属性。
    /// </summary>
    public static readonly DependencyProperty DefaultDrawingAttributesProperty =
        DependencyProperty.Register(
            nameof(DefaultDrawingAttributes),
            typeof(DrawingAttributes),
            typeof(InkCanvasNext),
            new PropertyMetadata(OnDefaultDrawingAttributesChanged));

    /// <summary>
    /// 标识 Mode 依赖项属性。
    /// </summary>
    public static readonly DependencyProperty ModeProperty =
        DependencyProperty.Register(
            nameof(Mode),
            typeof(InkCanvasNextMode),
            typeof(InkCanvasNext),
            new PropertyMetadata(InkCanvasNextMode.Ink, OnModeChanged));

    /// <summary>
    /// 标识 EraserDiameter 依赖项属性。
    /// </summary>
    public static readonly DependencyProperty EraserDiameterProperty =
        DependencyProperty.Register(
            nameof(EraserDiameter),
            typeof(double),
            typeof(InkCanvasNext),
            new PropertyMetadata(Eraser.DefaultDiameter));

    /// <summary>
    /// 标识 MouseWheelAction 依赖项属性。
    /// </summary>
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
    private readonly SelectionVisual selectionVisual;

    /// <summary>
    /// 初始化 InkCanvasNext 控件并装配内部画布与选择控制器。
    /// </summary>
    /// <param name="canvasSize">内层墨迹画布尺寸；文档背景页定位在此画布内。</param>
    /// <param name="initialOffset">初始视口左上角相对内容原点的偏移（视口单位）。</param>
    public InkCanvasNext(Size canvasSize, Point initialOffset)
    {
        InitializeComponent( );

        InnerCanvas.Width = canvasSize.Width;
        InnerCanvas.Height = canvasSize.Height;

        eraser = new Eraser(InnerCanvas, EraserFeedback);

        CanvasGrid.LayoutTransform = canvasScaleTransform;

        selectionVisual = new SelectionVisual(SelectionLayer, canvasSize);
        selection = new SelectionController(this, selectionVisual);

        InnerCanvas.Strokes.StrokesChanged += OnStrokesChanged;

        Strokes = InnerCanvas.Strokes;
        DefaultDrawingAttributes = InnerCanvas.DefaultDrawingAttributes;

        prevMode = InkCanvasNextMode.Ink;

        var distanceThreshold = DistanceThresholdFactor * SystemParameters.WorkArea.Width;
        distanceThreshold2 = distanceThreshold * distanceThreshold;

        CurrentView = new View(1.0, initialOffset.X, initialOffset.Y);

        CanvasScroll.ScrollChanged += (_, _) => RaiseViewOrSelectionChanged( );
        canvasScaleTransform.Changed += (_, _) => RaiseViewOrSelectionChanged( );

        InnerCanvas.Children.Add(multiTouchCanvas);

        SetupShapePreview( );
    }

    /// <summary>
    /// CanRedo 值发生变化时触发。
    /// </summary>
    public event EventHandler<DependencyPropertyChangedEventArgs>? CanRedoChanged;

    /// <summary>
    /// CanUndo 值发生变化时触发。
    /// </summary>
    public event EventHandler<DependencyPropertyChangedEventArgs>? CanUndoChanged;

    /// <summary>
    /// 画布上的墨迹笔画集合发生变化时触发。
    /// </summary>
    public event EventHandler<InkCanvasStrokesChangedEventArgs>? StrokesChanged;

    /// <summary>
    /// 当前选区发生变化时触发。
    /// </summary>
    public event EventHandler? SelectionChanged;

    /// <summary>
    /// 视口（滚动/缩放）或选区包围盒变化，用于外部工具栏跟随。
    /// </summary>
    public event EventHandler? ViewOrSelectionChanged;

    /// <summary>
    /// 获取是否存在可重做的操作。
    /// </summary>
    public bool CanRedo
    {
        get => (bool) GetValue(CanRedoProperty);
        private set => SetValue(CanRedoPropertyKey, value);
    }

    /// <summary>
    /// 获取是否存在可撤销的操作。
    /// </summary>
    public bool CanUndo
    {
        get => (bool) GetValue(CanUndoProperty);
        private set => SetValue(CanUndoPropertyKey, value);
    }

    /// <summary>
    /// 获取或设置默认的墨迹绘制属性。
    /// </summary>
    public DrawingAttributes DefaultDrawingAttributes
    {
        get => (DrawingAttributes) GetValue(DefaultDrawingAttributesProperty);
        set => SetValue(DefaultDrawingAttributesProperty, value);
    }

    /// <summary>
    /// 获取或设置当前编辑工具模式。
    /// </summary>
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

    /// <summary>
    /// 获取或设置鼠标滚轮的响应方式。
    /// </summary>
    public MouseWheelAction MouseWheelAction
    {
        get => (MouseWheelAction) GetValue(MouseWheelActionProperty);
        set => SetValue(MouseWheelActionProperty, value);
    }

    /// <summary>
    /// 获取或设置画布上的墨迹笔画集合。
    /// </summary>
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

    /// <summary>
    /// 获取或设置画布的水平滚动偏移量。
    /// </summary>
    public double OffsetX
    {
        get => CanvasScroll.HorizontalOffset;
        set => CanvasScroll.ScrollToHorizontalOffset(value);
    }

    /// <summary>
    /// 获取或设置画布的垂直滚动偏移量。
    /// </summary>
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
    /// 当前盖章模式（克隆/粘贴）。
    /// </summary>
    public StampAction StampAction { get; set; } = StampAction.None;

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
            var transform = InnerCanvas.TransformToVisual(relativeTo);
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

    /// <summary>
    /// 供 internal 协作对象（SelectionController 等）回调，外部消费者请订阅对应事件。
    /// </summary>
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
            (d as InkCanvasNext)?.InnerCanvas.DefaultDrawingAttributes = attributes;
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
        if (State != TouchState.Idle)
        {
            // 手势接管期间切换工具：放弃进行中的形状，避免抬手时把过时形状提交
            CancelShape( );
            return;
        }

        ApplyModeToEditing(mode);
    }

    /// <summary>
    /// 当前工具是否需要在 EvalDraw/Draw 期间抢先捕获触点（区域擦除需要，Ink 走原生不需要）。
    /// 状态机感知工具差异有两处接缝：本方法与选区入口的 Mode/StampAction 判定。
    /// </summary>
    private bool WantsPreemptiveDrawCapture( )
    {
        return Mode == InkCanvasNextMode.EraseArea;
    }

    private void ApplyModeToEditing(InkCanvasNextMode mode)
    {
        // 切换工具即中断形状绘制（鼠标路径 state 恒为 Idle，形状取消依赖此处）
        CancelShape( );

        switch (mode)
        {
            case InkCanvasNextMode.Ink:
            case InkCanvasNextMode.Highlighter:
                InnerCanvas.EditingMode = InkCanvasEditingMode.Ink;
                ConfigureSelectMode(false);
                break;
            case InkCanvasNextMode.EraseStroke:
                InnerCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
                ConfigureSelectMode(false);
                break;
            case InkCanvasNextMode.EraseArea:
            case InkCanvasNextMode.Line:
            case InkCanvasNextMode.Circle:
                InnerCanvas.EditingMode = InkCanvasEditingMode.None;
                ConfigureSelectMode(false);
                break;
            case InkCanvasNextMode.Select:
                InnerCanvas.EditingMode = InkCanvasEditingMode.None;
                ConfigureSelectMode(true);
                break;
        }
    }

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

        InnerCanvas.Strokes.StrokesChanged -= OnStrokesChanged;
        InnerCanvas.Strokes = newStrokes;
        InnerCanvas.Strokes.StrokesChanged += OnStrokesChanged;

        Strokes = newStrokes;

        selection.Clear( );
        StampAction = StampAction.None;

        ClearHistory( );
        EnsureStrokesFit( );
    }

    /// <summary>
    /// 设置文档背景页面；页面为 null 或盒子无效时移除页面。
    /// fitBox 为背景页在内容坐标中的 Uniform 适配盒子，决定 DocumentHost 的位置与尺寸，页面由布局居中等比适配；
    /// 页面与盒子成对提供（App.Document 与 App.DocumentBox 同步赋值）。
    /// </summary>
    public void SetDocumentPage(ImageSource? page, Rect? fitBox)
    {
        if (page is null || fitBox is not { Width: > 0, Height: > 0 })
        {
            DocumentHost.Child = null;
            DocumentHost.Visibility = Visibility.Collapsed;
            return;
        }

        DocumentHost.Margin = new Thickness(fitBox.Value.X, fitBox.Value.Y, 0, 0);
        DocumentHost.Width = fitBox.Value.Width;
        DocumentHost.Height = fitBox.Value.Height;
        DocumentHost.Child = new Image { Source = page, Stretch = Stretch.Uniform };
        DocumentHost.Visibility = Visibility.Visible;
    }
}
