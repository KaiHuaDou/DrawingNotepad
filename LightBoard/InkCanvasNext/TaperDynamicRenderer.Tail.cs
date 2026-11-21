using System;
using System.Threading;
using System.Windows.Media;
using System.Windows.Threading;

using MediaGeometry = System.Windows.Media.Geometry;

namespace InkCanvasNext;

internal sealed record TaperTailUpdate(MediaGeometry? Geometry, Brush? Brush, int Generation);

// 尾部视觉：末尾 Taper.Points 点单独成层，随报文重建后整层替换
internal sealed partial class TaperDynamicRenderer
{
    private ContainerVisual? root;
    private ContainerVisual? tailRoot;
    private DrawingVisual? tailVisual;

    private TaperTailUpdate? pendingTail;
    private int tailQueued;

    /// <summary>
    /// 尾部视觉是否已挂到 DynamicRenderer.RootVisual；用于集成测试
    /// </summary>
    internal bool HasTailLayer => Volatile.Read(ref tailVisual) is not null;

    private void EnsureTailLayer( )
    {
        if (tailVisual is not null)
        {
            return;
        }

        if (applicationDispatcher is null)
        {
            return;
        }

        if (!applicationDispatcher.CheckAccess( ))
        {
            applicationDispatcher.BeginInvoke(DispatcherPriority.Send, new Action(EnsureTailLayer));
            return;
        }

        if (RootVisual is not ContainerVisual container)
        {
            return;
        }

        root = container;
        tailRoot = new ContainerVisual( );
        tailVisual = new DrawingVisual( );
        tailRoot.Children.Add(tailVisual);
        container.Children.Add(tailRoot);
    }

    private void RemoveTailLayer( )
    {
        if (applicationDispatcher?.CheckAccess( ) == false)
        {
            applicationDispatcher.BeginInvoke(DispatcherPriority.Send, new Action(RemoveTailLayer));
            return;
        }

        if (root is not null && tailRoot is not null)
        {
            root.Children.Remove(tailRoot);
        }

        root = null;
        tailRoot = null;
        tailVisual = null;
    }

    private void PostTail(MediaGeometry? geometry, Brush? brush, int expectedGeneration)
    {
        var visual = Volatile.Read(ref tailVisual);
        if (visual is null)
        {
            return;
        }

        if (visual.Dispatcher.CheckAccess( ))
        {
            if (Volatile.Read(ref generation) == expectedGeneration)
            {
                DrawTail(geometry, brush);
            }

            return;
        }

        // 渲染线程只保留最新一份尾部几何，UI 线程忙时丢弃中间更新
        Volatile.Write(ref pendingTail, new TaperTailUpdate(geometry, brush, expectedGeneration));
        if (Interlocked.Exchange(ref tailQueued, 1) != 0)
        {
            return;
        }

        visual.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(FlushTail));
    }

    private void FlushTail( )
    {
        Interlocked.Exchange(ref tailQueued, 0);

        var update = Interlocked.Exchange(ref pendingTail, null);
        if (update is null || Volatile.Read(ref generation) != update.Generation)
        {
            return;
        }

        DrawTail(update.Geometry, update.Brush);
    }

    /// <summary>
    /// 干墨提交后同步清尾：OnStrokeCollected 调用；不依赖 Background 优先级
    /// </summary>
    internal void ClearTailAfterStroke( )
    {
        // 让排队中的尾部更新失效
        Interlocked.Increment(ref generation);
        Interlocked.Exchange(ref pendingTail, null);

        var visual = Volatile.Read(ref tailVisual);
        if (visual is null)
        {
            return;
        }

        if (visual.Dispatcher.CheckAccess( ))
        {
            DrawTail(null, null);
            return;
        }

        visual.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(( ) => DrawTail(null, null)));
    }

    private void DrawTail(MediaGeometry? geometry, Brush? brush)
    {
        if (tailVisual is null)
        {
            return;
        }

        // 原生 DynamicRenderer 把颜色 alpha 放进容器 Opacity，尾部视觉与它保持一致
        tailVisual.Opacity = (attributes?.Color.A ?? byte.MaxValue) / 255.0;

        using var context = tailVisual.RenderOpen( );
        if (geometry is not null && brush is not null)
        {
            context.DrawGeometry(brush, null, geometry);
        }
    }
}
