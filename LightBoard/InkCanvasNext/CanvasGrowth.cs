using System;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;

namespace InkCanvasNext;

/// <summary>
/// 画布向左或向上扩展、内容随之平移时的事件参数
/// </summary>
public sealed class InkCanvasContentShiftedEventArgs(Vector offset, StrokeCollection strokes) : EventArgs
{
    /// <summary>
    /// 内容在内容坐标中的平移量
    /// </summary>
    public Vector Offset { get; } = offset;

    /// <summary>
    /// 被平移的墨迹集合，宿主据此判断内容归属
    /// </summary>
    public StrokeCollection Strokes { get; } = strokes;
}

/// <summary>
/// 画布四边需要扩展的内容量，单位内容 DIP
/// </summary>
internal readonly record struct CanvasGrowth(double Left, double Top, double Right, double Bottom)
{
    // 初始视图距原点恰好一屏，视口略大于主屏时按整屏检查会立即平移内容
    private const double OriginPrefetchRatio = 0.5;

    // 余量与阈值相等即足够，相减的浮点误差不应改变判定
    private const double FillTolerance = 1e-6;

    internal bool IsEmpty => Left == 0 && Top == 0 && Right == 0 && Bottom == 0;

    internal bool IsFinite => double.IsFinite(Left)
        && double.IsFinite(Top)
        && double.IsFinite(Right)
        && double.IsFinite(Bottom);

    /// <summary>
    /// 左/上扩展会平移内容，右/下只改尺寸
    /// </summary>
    internal bool NeedsRebase => Left != 0 || Top != 0;

    /// <summary>
    /// 左/上扩展对应的内容平移量
    /// </summary>
    internal Vector Shift => new(Left, Top);

    /// <summary>
    /// 容纳指定包围盒所需的最小扩展
    /// </summary>
    internal static CanvasGrowth ToContain(Rect bounds, Size canvas)
    {
        return new CanvasGrowth(
            Math.Max(0, -bounds.Left),
            Math.Max(0, -bounds.Top),
            Math.Max(0, bounds.Right - canvas.Width),
            Math.Max(0, bounds.Bottom - canvas.Height));
    }

    /// <summary>
    /// 空闲视口四边需要的补足量：左/上按半屏检查，右/下按一屏检查
    /// </summary>
    internal static CanvasGrowth FromView(
        View view,
        Size viewport,
        Size canvas,
        bool prefetchRightBottom,
        bool prefetchLeftTop)
    {
        var screenWidth = viewport.Width / view.Scale;
        var screenHeight = viewport.Height / view.Scale;
        var endWidthThreshold = prefetchRightBottom ? screenWidth : 0;
        var endHeightThreshold = prefetchRightBottom ? screenHeight : 0;
        var originWidthThreshold = prefetchLeftTop ? screenWidth * OriginPrefetchRatio : 0;
        var originHeightThreshold = prefetchLeftTop ? screenHeight * OriginPrefetchRatio : 0;

        return new CanvasGrowth(
            view.OffsetX / view.Scale < originWidthThreshold ? screenWidth : 0,
            view.OffsetY / view.Scale < originHeightThreshold ? screenHeight : 0,
            FillStep(canvas.Width - (view.OffsetX + viewport.Width) / view.Scale, endWidthThreshold, screenWidth),
            FillStep(canvas.Height - (view.OffsetY + viewport.Height) / view.Scale, endHeightThreshold, screenHeight));
    }

    /// <summary>
    /// 把负数收成 0；调用前需确认所有分量有限
    /// </summary>
    internal CanvasGrowth Clamped( )
    {
        return new CanvasGrowth(
            Math.Max(0, Left),
            Math.Max(0, Top),
            Math.Max(0, Right),
            Math.Max(0, Bottom));
    }

    /// <summary>
    /// 余量不足阈值时按整屏步进补足所需内容量，最多四屏
    /// </summary>
    private static double FillStep(double available, double threshold, double step)
    {
        if (step <= 0 || !double.IsFinite(available) || available >= threshold - FillTolerance)
        {
            return 0;
        }

        return Math.Clamp(Math.Ceiling((threshold - available) / step), 1, 4) * step;
    }
}

public partial class InkCanvasNext
{
    /// <summary>
    /// 输入手势中累积的左/上平移量，等回空闲时一次执行
    /// </summary>
    private Vector pendingShift;

    /// <summary>
    /// 把画布扩展到容纳全部笔画；没有笔画或笔画不越界时不变
    /// </summary>
    public void EnsureStrokesFit( )
    {
        FitBounds(InnerCanvas.Strokes.GetBounds( ));
    }

    private void FitBounds(Rect bounds)
    {
        var shift = EnsureBoundsFit(bounds);
        if (shift.X != 0 || shift.Y != 0)
        {
            CompensateView(shift);
        }
    }

    /// <summary>
    /// 把画布扩展到容纳指定包围盒，返回内容平移量；
    /// 输入手势中出现的左/上越界先登记，等回空闲时一次执行
    /// </summary>
    private Vector EnsureBoundsFit(Rect bounds)
    {
        if (bounds.IsEmpty || !IsFinite(bounds))
        {
            return default;
        }

        var growth = CanvasGrowth.ToContain(bounds, CanvasSize);
        if (CanRebase)
        {
            return ApplyGrowth(growth);
        }

        // 右/下只改尺寸，不动内容坐标，可以在手势中立即执行
        pendingShift = new Vector(
            Math.Max(pendingShift.X, growth.Left),
            Math.Max(pendingShift.Y, growth.Top));

        return ApplyGrowth(growth with { Left = 0, Top = 0 });
    }

    /// <summary>
    /// 结算手势期间登记的左/上平移，返回平移量
    /// 调用时机仍在手势状态的迁出结算中，这里不受 <see cref="CanRebase"/> 门控
    /// </summary>
    private Vector ApplyPendingRebase( )
    {
        if (pendingShift == default)
        {
            return default;
        }

        var shift = ApplyGrowth(new CanvasGrowth(pendingShift.X, pendingShift.Y, 0, 0));
        pendingShift = default;
        return shift;
    }

    /// <summary>
    /// 一次应用四边扩展；右/下只改尺寸，左/上才平移内容与历史
    /// </summary>
    private Vector ApplyGrowth(CanvasGrowth growth)
    {
        if (!growth.IsFinite)
        {
            return default;
        }

        growth = growth.Clamped( );
        if (growth.IsEmpty)
        {
            return default;
        }

        InnerCanvas.Width += growth.Left + growth.Right;
        InnerCanvas.Height += growth.Top + growth.Bottom;
        CanvasGrid.Width = InnerCanvas.Width;
        CanvasGrid.Height = InnerCanvas.Height;
        selectionVisual.Resize(new Size(InnerCanvas.Width, InnerCanvas.Height));

        if (!growth.NeedsRebase)
        {
            return default;
        }

        var shift = growth.Shift;
        ShiftContent(shift);
        return shift;
    }

    /// <summary>
    /// 内容坐标整体平移：墨迹、文档背景、撤销矩阵与选区一起跟随
    /// 逐笔平移会重建几何与位图缓存，调用点集中在空闲时的整屏扩展
    /// </summary>
    private void ShiftContent(Vector shift)
    {
        var matrix = new Matrix(1, 0, 0, 1, shift.X, shift.Y);
        InnerCanvas.Strokes.Transform(matrix, false);

        if (DocumentHost.Child is not null)
        {
            DocumentHost.Margin = new Thickness(
                DocumentHost.Margin.Left + shift.X,
                DocumentHost.Margin.Top + shift.Y,
                0,
                0);
        }

        for (var i = 0; i < history.Count; i++)
        {
            history[i].Translate(shift);
        }

        if (selection.HasSelection)
        {
            selection.RecomputeBounds( );
            selection.Invalidate( );
        }

        ContentShifted?.Invoke(this, new InkCanvasContentShiftedEventArgs(shift, InnerCanvas.Strokes));
    }

    private static bool IsFinite(Rect rect)
    {
        return double.IsFinite(rect.X)
            && double.IsFinite(rect.Y)
            && double.IsFinite(rect.Width)
            && double.IsFinite(rect.Height);
    }
}
