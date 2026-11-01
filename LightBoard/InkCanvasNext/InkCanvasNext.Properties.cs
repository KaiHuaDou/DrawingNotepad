using System;
using System.Windows;
using System.Windows.Ink;

using DependencyPropertyGenerator;

namespace InkCanvasNext;

// 依赖项属性注册、公共事件与 CLR 属性包装
[DependencyProperty<bool>(
    "CanRedo",
    IsReadOnly = true,
    PropertyXmlDocumentation =
    """
    <summary>
    获取是否可以重做
    </summary>
    """
)]
[DependencyProperty<bool>(
    "CanUndo",
    IsReadOnly = true,
    PropertyXmlDocumentation =
    """
    <summary>
    获取是否可以撤销
    </summary>
    """
)]
[DependencyProperty<bool>(
    "IsReadOnly",
    PropertyXmlDocumentation =
    """
    <summary>
    获取或设置是否只读；只读时绘制、擦除与选择均不响应
    </summary>
    """
)]
[DependencyProperty<bool>(
    "UseDefaultCommands",
    PropertyXmlDocumentation =
    """
    <summary>
    获取或设置是否启用默认命令；为 false 时画布不注册快捷键，命令可用性仍按画布状态重查
    </summary>
    """
)]
[DependencyProperty<InkCanvasNextMode>(
    "Mode",
    DefaultValue = InkCanvasNextMode.Ink,
    PropertyXmlDocumentation =
    """
    <summary>
    获取或设置画布编辑工具模式
    </summary>
    """
)]
[DependencyProperty<double>(
    "EraserDiameter",
    DefaultValueExpression = "EraserParameters.DiameterDefault",
    PropertyXmlDocumentation =
    """
    <summary>
    获取或设置橡皮擦的直径，视口 DIP
    </summary>
    """
)]
[DependencyProperty<StampAction>(
    "StampAction",
    DefaultValue = StampAction.None,
    PropertyXmlDocumentation =
    """
    <summary>
    当前盖章模式（克隆或粘贴）；激活期间单指在抬手位置落章，多指手势照常
    </summary>
    """
)]

public partial class InkCanvasNext
{
    /// <summary>
    /// 选区工具栏与选区之间的间距，视口 DIP
    /// </summary>
    public const double SelectionToolbarGap = 8;

    // 生成器对无默认值的引用类型一律追加 ?，与 Strokes、DefaultDrawingAttributes
    // “构造后必非空”的对外声明不符，二者保持手工注册
    /// <summary>
    /// 标识 <see cref="DefaultDrawingAttributes"/> 依赖项属性
    /// </summary>
    public static readonly DependencyProperty DefaultDrawingAttributesProperty =
        DependencyProperty.Register(
            nameof(DefaultDrawingAttributes),
            typeof(DrawingAttributes),
            typeof(InkCanvasNext),
            new PropertyMetadata(null, OnDefaultDrawingAttributesChanged, CoerceDefaultDrawingAttributes));

    /// <summary>
    /// 标识 <see cref="Strokes"/> 依赖项属性
    /// </summary>
    public static readonly DependencyProperty StrokesProperty =
        DependencyProperty.Register(
            nameof(Strokes),
            typeof(StrokeCollection),
            typeof(InkCanvasNext),
            new PropertyMetadata(null, OnStrokesPropertyChanged, CoerceStrokes));

    /// <summary>
    /// <see cref="CanRedo"/> 变化时触发
    /// </summary>
    public event EventHandler<DependencyPropertyChangedEventArgs>? CanRedoChanged;

    /// <summary>
    /// <see cref="CanUndo"/> 变化时触发
    /// </summary>
    public event EventHandler<DependencyPropertyChangedEventArgs>? CanUndoChanged;

    /// <summary>
    /// 墨迹集合发生笔画增删时触发；选区整体变换不触发
    /// </summary>
    public event EventHandler<InkCanvasStrokesChangedEventArgs>? StrokesChanged;

    /// <summary>
    /// 选中笔画集合变化时触发
    /// </summary>
    public event EventHandler? SelectionChanged;

    /// <summary>
    /// 视口（滚动/缩放）或选区包围盒变化，用于外部工具栏跟随
    /// </summary>
    public event EventHandler? ViewOrSelectionChanged;

    /// <summary>
    /// 边缘提示的显示状态变化，宿主据此刷新自己绘制的提示
    /// </summary>
    public event EventHandler? EdgeHintsChanged;

    /// <summary>
    /// 获取或设置默认笔触属性；赋 null 时保留当前值
    /// </summary>
    public DrawingAttributes DefaultDrawingAttributes
    {
        get => (DrawingAttributes) GetValue(DefaultDrawingAttributesProperty);
        set => SetValue(DefaultDrawingAttributesProperty, value);
    }

    /// <summary>
    /// 获取或设置墨迹集合；赋 null 时替换为空集合
    /// </summary>
    public StrokeCollection Strokes
    {
        get => (StrokeCollection) GetValue(StrokesProperty);
        set => SetValue(StrokesProperty, value);
    }
}
