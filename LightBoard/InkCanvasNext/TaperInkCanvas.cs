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
    private DynamicRenderer? nativeRenderer;
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
        ClearWet( );

        // 重新挂一次自定义渲染器，触发它清掉在途笔画与尾部视觉
        if (TaperRenderer is not null && ReferenceEquals(DynamicRenderer, TaperRenderer))
        {
            DynamicRenderer = null;
            DynamicRenderer = TaperRenderer;
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
    /// 当前安装的自绘实时渲染器
    /// </summary>
    internal TaperDynamicRenderer? TaperRenderer { get; private set; }

    internal bool UsesNativeRenderer => ReferenceEquals(DynamicRenderer, nativeRenderer);

    protected override void OnStrokeCollected(InkCanvasStrokeCollectedEventArgs e)
    {
        Taper.Apply(e.Stroke, Taper.Effective(taper, e.Stroke.DrawingAttributes));

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

        if (configured && taper.Enabled)
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
            ? TaperRenderer ??= new TaperDynamicRenderer(taper)
            : nativeRenderer;

        // 替换渲染器时 ActiveEditingMode 不会重新触发 InkCollectionBehavior 的启停，这里对齐一次
        target?.Enabled = ActiveEditingMode == InkCanvasEditingMode.Ink;

        if (DynamicRenderer != target)
        {
            DynamicRenderer = target;
        }
    }
}
