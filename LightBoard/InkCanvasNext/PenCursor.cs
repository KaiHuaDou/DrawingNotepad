using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace InkCanvasNext;

/// <summary>
/// 手写笔悬停圆环：在视口覆盖层上按笔宽与缩放定位
/// 移动只改 TranslateTransform，不触发布局
/// </summary>
internal sealed class PenCursor(FrameworkElement mark, TranslateTransform offset)
{
    /// <summary>
    /// 圆环最小直径，视口 DIP
    /// </summary>
    internal const double MinDiameter = 6;

    private double shownDiameter = -1;

    internal bool IsVisible => mark.Visibility == Visibility.Visible;

    /// <summary>
    /// 显示圆环；位置为视口坐标，直径为视口 DIP
    /// </summary>
    internal void Show(Point position, double diameter)
    {
        var size = Math.Max(MinDiameter, diameter);

        if (size != shownDiameter)
        {
            shownDiameter = size;
            mark.Width = size;
            mark.Height = size;
        }

        offset.X = position.X - size / 2;
        offset.Y = position.Y - size / 2;
        mark.Visibility = Visibility.Visible;
    }

    internal void Hide( )
    {
        mark.Visibility = Visibility.Collapsed;
    }
}
