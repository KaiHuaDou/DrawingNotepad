using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LightBoard;

public partial class MainWindow
{
    private Size? selectionBorderSize;
    private bool selectionBorderUpdating;

    private readonly TranslateTransform selectionBorderOffset = new( );
    private double selectionBorderX = double.NaN;
    private double selectionBorderY;

    private Size MeasureSelectionBorder( )
    {
        SelectionBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = SelectionBorder.DesiredSize;
        // DesiredSize 已含倍率；ActualWidth/ActualHeight 是放大前的尺寸，需补偿到父空间
        if (size.Width <= 0)
        {
            size.Width = SelectionBorder.ActualWidth * toolbarScale;
        }

        if (size.Height <= 0)
        {
            size.Height = SelectionBorder.ActualHeight * toolbarScale;
        }

        return size;
    }

    /// <summary>
    /// 把选区工具栏吸附到选区包围盒左下角（旋转手柄绘于选区正上方）；下方放不下则移到选区上方/ 随拖动/平移/缩放/滚动实时更新。选区完全移出可视区时隐藏；位置双向夹紧到可视区
    /// </summary>
    private void UpdateSelectionBorderPosition( )
    {
        if (selectionBorderUpdating)
        {
            return;
        }

        selectionBorderUpdating = true;
        try
        {
            if (SelectionBorder.Parent is not UIElement parent)
            {
                return;
            }

            var bounds = CanvasNext.GetSelectionScreenBounds(parent);
            var viewport = CanvasNext.GetCanvasViewportBounds(parent);
            if (bounds is not Rect b || viewport is not Rect vp)
            {
                SelectionBorder.Visibility = Visibility.Collapsed;
                return;
            }

            // 只保留选区在可视区内的部分；完全不可见则隐藏工具栏（避免贴边/残影）
            var visible = b;
            visible.Intersect(vp);
            if (visible.IsEmpty)
            {
                SelectionBorder.Visibility = Visibility.Collapsed;
                return;
            }

            SelectionBorder.Visibility = Visibility.Visible;

            var size = selectionBorderSize ??= MeasureSelectionBorder( );
            var w = size.Width;
            var h = size.Height;

            // 吸附到选区可见部分左下角；下方放不下则移到选区上方
            var x = visible.Left;
            var y = visible.Bottom + InkCanvasNext.InkCanvasNext.SelectionToolbarGap;
            if (y + h > vp.Bottom)
            {
                y = visible.Top - h - InkCanvasNext.InkCanvasNext.SelectionToolbarGap;
            }

            // 双向夹紧到可视区，保证工具栏完全可见
            x = Math.Clamp(x, vp.Left, Math.Max(vp.Left, vp.Right - w));
            y = Math.Clamp(y, vp.Top, Math.Max(vp.Top, vp.Bottom - h));

            // 渲染变换只影响合成，不触发测量与排列；不足半个 DIP 的抖动不写
            if (Math.Abs(x - selectionBorderX) < 0.5 && Math.Abs(y - selectionBorderY) < 0.5)
            {
                return;
            }

            selectionBorderX = x;
            selectionBorderY = y;
            selectionBorderOffset.X = x;
            selectionBorderOffset.Y = y;
        }
        finally
        {
            selectionBorderUpdating = false;
        }
    }

    private double toolbarScale = LightBoardSettings.ToolbarScaleDefault;

    private void ApplyToolbarScale( )
    {
        toolbarScale = SettingsStore.Current.EffectiveToolbarScale;
        if (toolbarScale == 1)
        {
            return;
        }

        var transform = new ScaleTransform(toolbarScale, toolbarScale);
        transform.Freeze( );

        LeftBorder.LayoutTransform = transform;
        CenterBorder.LayoutTransform = transform;
        RightBorder.LayoutTransform = transform;
        SelectionBorder.LayoutTransform = transform;

        // 缓存的是旧倍率下量得的尺寸
        selectionBorderSize = null;
    }

    private void CollapseExpandClick(object o, RoutedEventArgs e)
    {
        var flag = CollapseExpandButton.IsChecked == true;
        CollapseExpandButton.Tag = flag ? "\uE70E" : "\uE70D";

        if (flag && AllPageToggle.IsChecked == true)
        {
            AllPageToggle.IsChecked = false;
            ApplyPagePanel(false);
        }

        // 折叠后屏幕上保留折叠与上页按钮区(含两侧内衬),其余推到屏幕外;
        // 位移按折叠后的布局宽计算,与收起动画并行时终点才对准保留区
        var chrome = RightBorder.Padding.Left * 2 + RightBorder.BorderThickness.Left * 2;
        var keptWidth = RightBorder.Padding.Left + RightBorder.BorderThickness.Left
            + CollapseExpandButton.Margin.Left + CollapseExpandButton.ActualWidth
            + PrevPageButton.Margin.Left + PrevPageButton.ActualWidth
            + RightBorder.Padding.Right + RightBorder.BorderThickness.Right;
        // 三个目标值取自本地空间（工具栏被 LayoutTransform 放大前的尺寸），位移本身按屏幕像素生效
        // From 取 Transform 当前值:快速连点时上一动画仍在 Hold,读端点值会造成跳变
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var animationLeft = new DoubleAnimation
        {
            From = LeftTransform.X,
            To = flag ? -LeftBorder.ActualWidth * toolbarScale : 0,
            Duration = TimeSpan.FromSeconds(0.1),
            EasingFunction = ease
        };
        var animationCenter = new DoubleAnimation
        {
            From = CenterTransform.Y,
            To = flag ? CenterBorder.ActualHeight * toolbarScale : 0,
            Duration = TimeSpan.FromSeconds(0.1),
            EasingFunction = ease
        };
        var animationRight = new DoubleAnimation
        {
            From = RightTransform.X,
            To = flag ? (PageButtonsRow.ActualWidth + chrome - keptWidth) * toolbarScale : 0,
            Duration = TimeSpan.FromSeconds(0.1),
            EasingFunction = ease
        };
        // 展台工具栏钉在 CenterBorder 上方，随折叠位移一起滑动
        var animationCameraBar = new DoubleAnimation
        {
            From = CameraBarTransform.Y,
            To = flag ? CenterBorder.ActualHeight * toolbarScale : 0,
            Duration = TimeSpan.FromSeconds(0.1),
            EasingFunction = ease
        };

        LeftTransform.BeginAnimation(TranslateTransform.XProperty, animationLeft);
        CenterTransform.BeginAnimation(TranslateTransform.YProperty, animationCenter);
        RightTransform.BeginAnimation(TranslateTransform.XProperty, animationRight);
        CameraBarTransform.BeginAnimation(TranslateTransform.YProperty, animationCameraBar);
    }

    private void MoreToggleClick(object o, RoutedEventArgs e)
    {
        ApplyMoreToggle(MoreToggle.IsChecked == true);
    }

    private void ApplyMoreToggle(bool expanded)
    {
        MoreToggle.IsChecked = expanded;
        MoreToggle.Tag = expanded ? "\uE70D" : "\uE70E";

        var heightAnimation = new DoubleAnimation
        {
            From = CenterBorder.ActualHeight,
            To = expanded ? CenterBorder.ActualHeight * 2 : CenterBorder.ActualHeight / 2,
            Duration = TimeSpan.FromSeconds(0.1),
            EasingFunction = new CubicEase( ) { EasingMode = expanded ? EasingMode.EaseIn : EasingMode.EaseOut }
        };

        CenterBorder.BeginAnimation(HeightProperty, heightAnimation);
    }

    /// <summary>
    /// 底侧边缘提示按钮的让位：工具栏高度是本地尺寸，屏幕高度要乘倍率
    /// </summary>
    internal static double EdgeHintGap(double barHeight, double barMarginBottom, double scale)
    {
        return barHeight * scale + barMarginBottom + 9;
    }
}
