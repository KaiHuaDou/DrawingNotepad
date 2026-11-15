using System;

namespace InkCanvasNext;

/// <summary>
/// 画布上完成的一次工具操作
/// </summary>
public enum InkCanvasOperation
{
    /// <summary>
    /// 一笔书写完成
    /// </summary>
    Stroke,
    /// <summary>
    /// 一次擦除手势完成
    /// </summary>
    Erase
}

public sealed class InkCanvasOperationEventArgs(InkCanvasOperation operation, DrawingAttributesNext drawingAttributes) : EventArgs
{
    /// <summary>
    /// 完成的操作类型
    /// </summary>
    public InkCanvasOperation Operation { get; } = operation;

    /// <summary>
    /// 完成这次操作时的工具设置
    /// </summary>
    public DrawingAttributesNext DrawingAttributes { get; } = drawingAttributes;
}

public partial class InkCanvasNext
{
    /// <summary>
    /// 一次工具操作完成时触发：落笔收笔，或一次擦除手势结束；撤销与重做的重放不触发
    /// </summary>
    public event EventHandler<InkCanvasOperationEventArgs>? OperationCompleted;

    private void RaiseOperationCompleted(InkCanvasOperation operation)
    {
        OperationCompleted?.Invoke(this, new(operation, DrawingAttributes));
    }
}
