using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Input.StylusPlugIns;

namespace InkCanvasNext;

// InkCanvas 子类：笔锋开启时接管湿墨显示，笔画收集完成后改写末端压力
// 关闭时什么都不装，保持原生绘制路径
// 湿墨通道与 InkCanvas 的收笔类处理器对齐：
// 笔走冒泡虚方法（收笔的笔分支不改 Handled，虚方法必然执行），
// 鼠标走预览虚方法（收笔的鼠标分支会把冒泡 MouseDown 标记 Handled，冒泡虚方法会被跳过），
// 预览先于冒泡类处理器执行，湿墨恰好落在收集开始之前与提交之后
internal sealed class TaperInkCanvas : InkCanvas
{
    // 无笔服务机器的鼠标笔画没有设备号，用不会与笔设备冲突的占位值
    private const int MouseWetId = -1;

    private TaperParameters taper = new( );
    private DynamicRenderer? nativeRenderer;
    private bool wetAttached;

    /// <summary>
    /// 配置笔锋；在画布构造时调用一次
    /// </summary>
    internal void Configure(TaperParameters options)
    {
        taper = options;
        nativeRenderer = DynamicRenderer;

        if (!options.Enabled)
        {
            return;
        }

        if (options.Wet)
        {
            Wet = new WetInk(options);
            AttachWet( );
            Loaded += (_, _) => AttachWet( );
            Unloaded += (_, _) => DetachWet( );
            LostStylusCapture += (_, _) => Wet.Abort( );
            LostMouseCapture += (_, _) => Wet.Clear(MouseWetId);
        }

        DefaultDrawingAttributes.AttributeChanged += (_, _) => SyncRenderer( );
        SyncRenderer( );
    }

    /// <summary>
    /// 清空湿墨显示；触摸状态重置时调用
    /// </summary>
    internal void AbortWetInk( )
    {
        Wet?.Abort( );
    }

    internal WetInk? Wet { get; private set; }

    internal bool UsesNativeRenderer => DynamicRenderer is not null;

    // 荧光笔等忽略压力的笔由原生渲染器显示在绘笔画
    private WetInk? DrawingWet => DefaultDrawingAttributes.IgnorePressure ? null : Wet;

    protected override void OnStrokeCollected(InkCanvasStrokeCollectedEventArgs e)
    {
        Taper.Apply(e.Stroke, Taper.Effective(taper, e.Stroke.DrawingAttributes));

        base.OnStrokeCollected(e);
    }

    protected override void OnStylusDown(StylusDownEventArgs e)
    {
        base.OnStylusDown(e);

        if (DrawingWet is not { } wet || !IsWetStylus(e.StylusDevice))
        {
            return;
        }

        wet.Begin(e.StylusDevice.Id, DefaultDrawingAttributes, e.GetStylusPoints(this));
    }

    protected override void OnStylusMove(StylusEventArgs e)
    {
        base.OnStylusMove(e);

        if (Wet?.Active != true)
        {
            return;
        }

        if (EditingMode != InkCanvasEditingMode.Ink)
        {
            // 手势接管把编辑模式压成 None，收笔已被丢弃，湿墨同步放弃
            Wet.Abort( );
            return;
        }

        Wet.Add(e.StylusDevice.Id, e.GetStylusPoints(this));
    }

    protected override void OnStylusUp(StylusEventArgs e)
    {
        base.OnStylusUp(e);

        Wet?.Clear(e.StylusDevice.Id);
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

        Wet?.Clear(MouseWetId);
    }

    // 反手持笔走 EditingModeInverted（默认按笔擦除），不收笔也不显示湿墨
    private bool IsWetStylus(StylusDevice device)
    {
        var mode = device.Inverted ? EditingModeInverted : EditingMode;

        return mode == InkCanvasEditingMode.Ink;
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
        var takeOver = DrawingWet is not null;
        var target = takeOver ? null : nativeRenderer;

        if (DynamicRenderer != target)
        {
            DynamicRenderer = target;
        }

        if (!takeOver)
        {
            Wet?.Abort( );
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
