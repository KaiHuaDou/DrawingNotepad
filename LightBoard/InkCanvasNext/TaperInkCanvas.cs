using System;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input.StylusPlugIns;

namespace InkCanvasNext;

// InkCanvas 子类：笔锋开启时用 TaperDynamicRenderer 走实时插件通道，笔画收集完成后改写末端压力
// 关闭笔锋或荧光笔时保留原生 DynamicRenderer
// WPF 把鼠标、触摸、笔统一送进 StylusPlugIn 通道（鼠标是设备号 0 的原始笔报文），三者共用同一条湿墨路径
internal sealed class TaperInkCanvas : InkCanvas
{
    private TaperParameters taper = new( );
    private InputArbiter arbiter = null!;
    private DynamicRenderer? nativeRenderer;
    private bool configured;
    private bool continues;

    /// <summary>
    /// 配置笔锋；在画布构造时调用一次
    /// </summary>
    internal void Configure(TaperParameters options, InputArbiter inputArbiter)
    {
        taper = options;
        arbiter = inputArbiter;
        nativeRenderer = new FilteredDynamicRenderer(inputArbiter);
        DynamicRenderer = nativeRenderer;
        configured = true;

        ObserveDefaultDrawingAttributes( );
    }

    internal void InstallPressurePlugin(PenParameters pen)
    {
        StylusPlugIns.Insert(0, new DevicePressureStylusPlugIn(arbiter, pen));
    }

    /// <summary>
    /// 压力插件是否位于实时渲染器之前；干墨与湿墨都要看到改写后的压力
    /// </summary>
    internal bool PressurePluginFirst => StylusPlugIns.Count > 0
                && StylusPlugIns[0] is DevicePressureStylusPlugIn
                && DynamicRenderer is { } renderer
                && StylusPlugIns.IndexOf(renderer) > 0;

    /// <summary>
    /// 订阅当前笔触属性的变化；替换实例后再次调用
    /// </summary>
    internal void ObserveDefaultDrawingAttributes( )
    {
        if (!configured)
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
        ClearWet( );

        // 重挂当前渲染器触发 OnRemoved/OnAdded：任何渲染器都会销毁在途湿墨与尾部视觉
        if (DynamicRenderer is { } renderer)
        {
            DynamicRenderer = null;
            DynamicRenderer = renderer;
        }
    }

    /// <summary>
    /// 只清湿墨/尾部视觉，不动渲染器；进入 Pan/PanZoom/MultiDraw 等手势时调用
    /// </summary>
    internal void ClearWet( )
    {
        TaperRenderer?.ClearTailAfterStroke( );
    }

    /// <summary>
    /// 提交在绘笔画：切换编辑模式会丢弃在绘笔画，释放笔捕获才整笔收集
    /// 没有在绘笔画时为空操作；continues 为 true 时收笔处不渐细，末端由后续笔画续接
    /// </summary>
    internal void CommitStroke(bool continues = false)
    {
        if (EditingMode != InkCanvasEditingMode.Ink || !IsStylusCaptured)
        {
            return;
        }

        this.continues = continues;

        try
        {
            ReleaseStylusCapture( );
        }
        finally
        {
            this.continues = false;
        }
    }

    /// <summary>
    /// 当前安装的自绘实时渲染器
    /// </summary>
    internal TaperDynamicRenderer? TaperRenderer { get; private set; }

    internal bool UsesNativeRenderer => ReferenceEquals(DynamicRenderer, nativeRenderer);

    protected override void OnStrokeCollected(InkCanvasStrokeCollectedEventArgs e)
    {
        if (arbiter.StrokeKind != InputKind.Stylus)
        {
            Taper.Apply(e.Stroke, Taper.Effective(taper, e.Stroke.DrawingAttributes, continues));
        }

        base.OnStrokeCollected(e);

        // 干墨已在集合里，同步清尾；不要等 OnStylusUp 的 Background
        if (TaperRenderer is not null && ReferenceEquals(DynamicRenderer, TaperRenderer))
        {
            TaperRenderer.ClearTailAfterStroke( );
        }
    }

    /// <summary>
    /// 笔触属性实例被整体替换时重新订阅，并按新属性重选渲染器
    /// </summary>
    protected override void OnDefaultDrawingAttributesReplaced(DrawingAttributesReplacedEventArgs e)
    {
        base.OnDefaultDrawingAttributesReplaced(e);

        e.PreviousDrawingAttributes.AttributeChanged -= DefaultDrawingAttributesChanged;

        if (configured)
        {
            DefaultDrawingAttributes.AttributeChanged -= DefaultDrawingAttributesChanged;
            DefaultDrawingAttributes.AttributeChanged += DefaultDrawingAttributesChanged;
        }

        SyncRenderer( );
    }

    private void SyncRenderer( )
    {
        if (!configured)
        {
            return;
        }

        // 鼠标/笔/触摸都走插件通道，只有原生渲染器画不了的压力或笔尖形状才回退
        var useTaper = taper.Enabled
            && taper.Wet
            && !DefaultDrawingAttributes.IgnorePressure
            && !DefaultDrawingAttributes.IsHighlighter
            && DefaultDrawingAttributes.Color.A == byte.MaxValue
            && DefaultDrawingAttributes.StylusTip == StylusTip.Ellipse;

        var target = useTaper
            ? TaperRenderer ??= new TaperDynamicRenderer(taper, arbiter)
            : nativeRenderer;

        // 替换渲染器时 ActiveEditingMode 不会重新触发 InkCollectionBehavior 的启停，这里对齐一次
        target?.Enabled = ActiveEditingMode == InkCanvasEditingMode.Ink;

        if (DynamicRenderer != target)
        {
            DynamicRenderer = target;
        }
    }
}
