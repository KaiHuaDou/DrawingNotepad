using System;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Input.StylusPlugIns;

namespace InkCanvasNext;

// InkCanvas 子类：笔锋开启时用 TaperDynamicRenderer 走实时插件通道，笔画收集完成后改写末端压力
// 关闭笔锋或荧光笔时保留原生 DynamicRenderer；鼠标笔画没有插件通道，仍由 WetInk 自绘
internal sealed class TaperInkCanvas : InkCanvas
{
    // 无笔服务机器的鼠标笔画没有设备号，用不会与笔设备冲突的占位值
    private const int MouseWetId = -1;

    private TaperParameters taper = new( );
    private DynamicRenderer? nativeRenderer;
    private TaperDynamicRenderer? taperRenderer;
    private bool wetAttached;
    private bool configured;

    /// <summary>
    /// 配置笔锋；在画布构造时调用一次
    /// </summary>
    internal void Configure(TaperParameters options)
    {
        taper = options;
        nativeRenderer = DynamicRenderer;
        configured = true;

        if (!options.Enabled)
        {
            return;
        }

        if (options.Wet)
        {
            // 笔/触摸的湿墨由 TaperDynamicRenderer 负责；WetInk 只服务鼠标笔画
            Wet = new WetInk(options);
            AttachWet( );
            Loaded += (_, _) => AttachWet( );
            Unloaded += (_, _) => DetachWet( );
            LostMouseCapture += (_, _) => Wet.Clear(MouseWetId);
        }

        ObserveDefaultDrawingAttributes( );
    }

    /// <summary>
    /// 订阅当前笔触属性的变化；替换实例后再次调用
    /// </summary>
    internal void ObserveDefaultDrawingAttributes( )
    {
        if (!configured || !taper.Enabled)
        {
            return;
        }

        DefaultDrawingAttributes.AttributeChanged -= DefaultDrawingAttributesChanged;
        DefaultDrawingAttributes.AttributeChanged += DefaultDrawingAttributesChanged;
        SyncRenderer( );
    }

    private void DefaultDrawingAttributesChanged(object? o, EventArgs e)
    {
        SyncRenderer( );
    }

    /// <summary>
    /// 清空湿墨显示；触摸状态重置时调用
    /// </summary>
    internal void AbortWetInk( )
    {
        Wet?.Abort( );

        // 重新挂一次自定义渲染器，触发它清掉在途笔画与尾部视觉
        if (taperRenderer is not null && ReferenceEquals(DynamicRenderer, taperRenderer))
        {
            DynamicRenderer = null;
            DynamicRenderer = taperRenderer;
        }
    }

    internal WetInk? Wet { get; private set; }

    /// <summary>
    /// 当前安装的自绘实时渲染器；测试与调试用
    /// </summary>
    internal TaperDynamicRenderer? TaperRenderer => taperRenderer;

    internal bool UsesNativeRenderer => ReferenceEquals(DynamicRenderer, nativeRenderer);

    // 荧光笔等忽略压力的笔由原生渲染器显示在绘笔画；鼠标湿墨同样遵守这一条件
    private WetInk? DrawingWet => DefaultDrawingAttributes.IgnorePressure ? null : Wet;

    protected override void OnStrokeCollected(InkCanvasStrokeCollectedEventArgs e)
    {
        Taper.Apply(e.Stroke, Taper.Effective(taper, e.Stroke.DrawingAttributes));

        base.OnStrokeCollected(e);
    }

    /// <summary>
    /// 笔触属性实例被整体替换时重新订阅，并按新属性重选渲染器
    /// </summary>
    protected override void OnDefaultDrawingAttributesReplaced(DrawingAttributesReplacedEventArgs e)
    {
        base.OnDefaultDrawingAttributesReplaced(e);

        e.PreviousDrawingAttributes.AttributeChanged -= DefaultDrawingAttributesChanged;

        if (configured && taper.Enabled)
        {
            DefaultDrawingAttributes.AttributeChanged -= DefaultDrawingAttributesChanged;
            DefaultDrawingAttributes.AttributeChanged += DefaultDrawingAttributesChanged;
        }

        SyncRenderer( );
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);

        // 提升为笔事件的鼠标由笔管线绘制，这里只接无笔服务机器的纯鼠标
        if (DrawingWet is not { } wet || e.StylusDevice is not null || e.ChangedButton != MouseButton.Left
            || EditingMode != InkCanvasEditingMode.Ink)
        {
            return;
        }

        wet.Begin(MouseWetId, DefaultDrawingAttributes, MousePoints(e));
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);

        if (Wet?.Active != true)
        {
            return;
        }

        if (EditingMode != InkCanvasEditingMode.Ink)
        {
            Wet.Abort( );
            return;
        }

        Wet.Add(MouseWetId, MousePoints(e));
    }

    protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseUp(e);

        // 鼠标收笔在冒泡阶段；预览阶段立即清湿墨会留下最多一帧空白，延后到输入队列末尾
        Dispatcher.InvokeAsync(( ) => Wet?.Clear(MouseWetId), System.Windows.Threading.DispatcherPriority.Input);
    }

    // InkCanvas 的鼠标收集按 0.5 压力建点，湿墨保持同一份数据
    private StylusPointCollection MousePoints(MouseEventArgs e)
    {
        var position = e.GetPosition(this);
        var points = new StylusPointCollection(1) { new StylusPoint(position.X, position.Y) };

        return points;
    }

    private void SyncRenderer( )
    {
        if (!configured)
        {
            return;
        }

        // 笔/触摸有实时插件通道；鼠标没有，走 WetInk
        var useTaper = taper.Enabled
            && taper.Wet
            && !DefaultDrawingAttributes.IgnorePressure
            && !DefaultDrawingAttributes.IsHighlighter
            && DefaultDrawingAttributes.Color.A == byte.MaxValue
            && DefaultDrawingAttributes.StylusTip == StylusTip.Ellipse;

        var target = useTaper
            ? taperRenderer ??= new TaperDynamicRenderer(taper)
            : nativeRenderer;

        // 替换渲染器时 ActiveEditingMode 不会重新触发 InkCollectionBehavior 的启停，这里对齐一次
        target?.Enabled = ActiveEditingMode == InkCanvasEditingMode.Ink;

        if (DynamicRenderer != target)
        {
            DynamicRenderer = target;
        }
    }

    private void AttachWet( )
    {
        if (Wet is null || wetAttached)
        {
            return;
        }

        // 第二个参数只决定荧光笔分组；湿墨只在非荧光笔时绘制
        InkPresenter.AttachVisuals(Wet.Canvas, new DrawingAttributes( ));
        wetAttached = true;
    }

    private void DetachWet( )
    {
        Wet?.Abort( );

        if (!wetAttached || Wet is null)
        {
            return;
        }

        InkPresenter.DetachVisuals(Wet.Canvas);
        wetAttached = false;
    }
}
