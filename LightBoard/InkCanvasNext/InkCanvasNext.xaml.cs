using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasNext;

/// <summary>
/// 支持绘制、擦除、选择、直线、圆形、高亮等模式的墨迹画布控件
/// </summary>
public partial class InkCanvasNext : UserControl
{
    private readonly SelectionController selection;
    private readonly SelectionVisual selectionVisual;

    internal TaperInkCanvas InnerCanvas { get; private set; } = null!;

    /// <summary>
    /// 初始化 InkCanvasNext 控件并装配内部画布与选择控制器
    /// </summary>
    /// <param name="canvasSize">内层墨迹画布尺寸；文档背景页定位在此画布内</param>
    /// <param name="initialOffset">初始视口左上角相对内容原点的偏移（视口单位）</param>
    /// <param name="options">构造期选项；为 null 时取默认值</param>
    public InkCanvasNext(Size canvasSize, Point initialOffset, InkCanvasNextOptions? options = null)
    {
        InitializeComponent( );
        InitializeCommands( );

        this.options = options ?? new( );
        CreateInnerCanvas( );

        history = new RingBuffer<IHistoryChange>(this.options.MaxHistoryCount);

        InnerCanvas.Width = canvasSize.Width;
        InnerCanvas.Height = canvasSize.Height;
        CanvasGrid.Width = canvasSize.Width;
        CanvasGrid.Height = canvasSize.Height;

        eraser = new Eraser(InnerCanvas, EraserFeedback, EraserFeedbackLabel, this.options.Eraser);
        EraserDiameter = this.options.Eraser.Diameter;

        MousePointer = new MouseTouchDevice(this);

        selectionVisual = new SelectionVisual(SelectionLayer, canvasSize);
        selection = new SelectionController(this, selectionVisual);

        DrawingAttributes = new DrawingAttributesNext { DrawingAttributes = InnerCanvas.DefaultDrawingAttributes };

        InnerCanvas.Strokes.StrokesChanged += OnStrokesChanged;

        Strokes = InnerCanvas.Strokes;

        CurrentView = new View(1.0, initialOffset.X, initialOffset.Y);

        CanvasScroll.ScrollChanged += (_, _) =>
        {
            // 手势中改画布尺寸会让排列与位图缓存失效：余量补足留到手势结束，手势中只由 setter 保视口覆盖
            if (State == TouchState.Idle)
            {
                EnsureCanvasFill(CurrentView);
            }

            RaiseViewOrSelectionChanged( );
        };
        CanvasScale.Changed += (_, _) => RaiseViewOrSelectionChanged( );

        edgeScrollTimer.Interval = TimeSpan.FromMilliseconds(EdgeScrollTickMilliseconds);
        edgeScrollTimer.Tick += EdgeScrollTick;

        InnerCanvas.Children.Add(multiTouchCanvas);

        Unloaded += InkCanvasNextUnloaded;
    }

    private void InkCanvasNextUnloaded(object o, RoutedEventArgs e)
    {
        ResetTouchState( );
        UnhookMultiTouchFrame( );
    }

    // InkCanvas 不写进 XAML：x:Class 与命名空间同名，XAML 编译器生成的元素类型引用在类内无法解析
    private void CreateInnerCanvas( )
    {
        InnerCanvas = new TaperInkCanvas
        {
            Background = Brushes.Transparent,
            UseCustomCursor = true
        };

        Stylus.SetIsPressAndHoldEnabled(InnerCanvas, false);
        Panel.SetZIndex(InnerCanvas, 36); // 与 XAML 的 SelectionLayer(37)、ShapePreviewLayer(38) 衔接
        InnerCanvas.DefaultDrawingAttributes = DrawingAttributesNext.CreateDefault( );
        InnerCanvas.LostTouchCapture += CanvasLostTouchCapture;
        InnerCanvas.PreviewMouseDown += CanvasPreviewMouseDown;
        InnerCanvas.PreviewMouseMove += CanvasPreviewMouseMove;
        InnerCanvas.PreviewMouseUp += CanvasPreviewMouseUp;
        InnerCanvas.PreviewTouchDown += CanvasPreviewTouchDown;
        InnerCanvas.PreviewTouchMove += CanvasPreviewTouchMove;
        InnerCanvas.PreviewTouchUp += CanvasPreviewTouchUp;
        InnerCanvas.TouchLeave += CanvasTouchLeave;
        CanvasGrid.Children.Insert(1, InnerCanvas);

        InnerCanvas.Configure(options.Taper);
    }

    /// <summary>
    /// 协作对象回调用
    /// </summary>
    internal void RaiseSelectionChanged( )
    {
        SelectionChanged?.Invoke(this, EventArgs.Empty);

        // 选区变化决定删除、剪切、复制的可用性，命令的 CanExecute 要跟着重算
        CommandManager.InvalidateRequerySuggested( );
    }

    internal void RaiseViewOrSelectionChanged( )
    {
        // 手势中每帧都会走到这里：边缘提示要遍历全画布笔画，留到手势结束补一次
        if (State == TouchState.Idle)
        {
            UpdateEdgeHints( );
        }

        ViewOrSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 把选区包围盒（内容坐标）变换到 <paramref name="relativeTo"/> 坐标系返回；无选区或不可用返回 null
    /// 用于选区工具栏跟随定位
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
    /// 用于选区工具栏的可见性判断与位置夹紧
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

    private static object CoerceStrokes(DependencyObject d, object value)
    {
        return value ?? new StrokeCollection( );
    }

    partial void OnIsReadOnlyChanged(bool oldValue, bool newValue)
    {
        ResetTouchState( );
        ApplyEditingMode(DrawingAttributes.Mode);
    }

    partial void OnUseDefaultCommandsChanged(bool oldValue, bool newValue)
    {
        Focusable = newValue;
    }

    private static void OnStrokesPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        (d as InkCanvasNext)?.ApplyStrokes((StrokeCollection) e.NewValue);
    }

    private void ApplyEditingMode(InkCanvasNextMode mode)
    {
        if (State != TouchState.Idle)
        {
            // 手势接管期间切换工具：放弃进行中的形状，避免抬手时把过时形状提交；
            // 橡皮反馈平时只在触摸尾声刷新，工具变化必须立即按新判定隐藏
            CancelShape( );
            UpdateAreaEraser( );
            return;
        }

        ApplyModeToEditing(mode);
    }

    private void ApplyModeToEditing(InkCanvasNextMode mode)
    {
        // 切换工具即中断形状绘制（鼠标路径同样经过状态机，形状取消依赖此处）
        CancelShape( );

        if (IsReadOnly)
        {
            InnerCanvas.EditingMode = InkCanvasEditingMode.None;
            ConfigureSelectMode(false);
            return;
        }

        // 盖章期间单指只落章：原生收笔一律关闭（触屏上仅 Handled 掉触摸事件压不住手写笔管线）
        if (Snapshot.IsStamp)
        {
            InnerCanvas.EditingMode = InkCanvasEditingMode.None;
            return;
        }

        // InkCanvas 内置光标在 Ink 模式下随 DrawingAttributes 实变重建（GDI 构造，单次 0.7~2 ms），
        // 进出高亮各触发 3 次实变；UseCustomCursor 后属性实变降到 10~30 µs（探针实测）
        switch (mode)
        {
            case InkCanvasNextMode.Ink:
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
            case InkCanvasNextMode.Select:
                InnerCanvas.EditingMode = InkCanvasEditingMode.None;
                ConfigureSelectMode(mode == InkCanvasNextMode.Select);
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

    private void ApplyStrokes(StrokeCollection strokes)
    {
        // 命中测试器挂在旧集合上，手势先在旧集合上结束
        ResetTouchState( );

        InnerCanvas.Strokes.StrokesChanged -= OnStrokesChanged;
        InnerCanvas.Strokes = strokes;
        InnerCanvas.Strokes.StrokesChanged += OnStrokesChanged;

        selection.SyncCanvasStrokes(strokes);
        selection.Clear( );
        DrawingAttributes = DrawingAttributes with { Stamp = StampAction.None };

        ClearHistory( );
        EnsureStrokesFit( );
        UpdateEdgeHints( );
    }

    /// <summary>
    /// 设置文档背景页面；页面为 null 或盒子无效时移除页面。与当前显示一致时不变。
    /// fitBox 为背景页在内容坐标中的 Uniform 适配盒子，决定 DocumentHost 的位置与尺寸，页面由布局居中等比适配；
    /// 页面与盒子成对提供，均取自同一页的背景引用
    /// </summary>
    public void SetDocumentPage(ImageSource? page, Rect? fitBox)
    {
        if (page is null || fitBox is not { Width: > 0, Height: > 0 })
        {
            if (DocumentHost.Child is null)
            {
                return;
            }

            DocumentHost.Child = null;
            DocumentHost.Visibility = Visibility.Collapsed;
            return;
        }

        var box = fitBox.Value;
        if (DocumentHost.Child is Image shown
            && ReferenceEquals(shown.Source, page)
            && DocumentHost.Width == box.Width
            && DocumentHost.Height == box.Height
            && DocumentHost.Margin.Left == box.X
            && DocumentHost.Margin.Top == box.Y)
        {
            return;
        }

        DocumentHost.Margin = new Thickness(box.X, box.Y, 0, 0);
        DocumentHost.Width = box.Width;
        DocumentHost.Height = box.Height;
        DocumentHost.Child = new Image { Source = page, Stretch = Stretch.Uniform };
        DocumentHost.Visibility = Visibility.Visible;

        ExtendCanvas(box.Right - InnerCanvas.Width, box.Bottom - InnerCanvas.Height);
    }
}
