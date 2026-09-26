using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;

namespace LightBoard;

public partial class MainWindow
{
    private void AllPageToggleClick(object o, RoutedEventArgs e)
    {
        if (o is not ToggleButton { IsChecked: bool isChecked })
        {
            return;
        }

        ApplyPagePanel(isChecked);
    }

    // 页面列表面板的开合本体：事件处理器与折叠联动共用
    private void ApplyPagePanel(bool isChecked)
    {
        TimeText.Visibility = isChecked ? Visibility.Collapsed : Visibility.Visible;
        PagePreviewsBox.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;
        RightBorder.Background = isChecked ? ContainerBrushSolid : ContainerBrush;

        var chrome = RightBorder.Padding.Left * 2 + RightBorder.BorderThickness.Left * 2;
        var fromWidth = RightBorder.ActualWidth;
        var fromHeight = RightBorder.ActualHeight;

        if (isChecked)
        {
            // 列宽在布局后才有值:恒值动画锁住当前尺寸补一次布局,测量瞬间面板不跳宽
            RightBorder.BeginAnimation(WidthProperty, HoldAnimation(fromWidth));
            RightBorder.BeginAnimation(HeightProperty, HoldAnimation(fromHeight));
            RightBorder.UpdateLayout( );
        }

        // 列宽与模板内衬的复合需求难以从外部拆解,容器生成后直接做无约束测量拿列表需求宽
        PagePreviewsBox.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var toWidth = Math.Max(PageButtonsRow.ActualWidth, isChecked ? PagePreviewsBox.DesiredSize.Width : 0) + chrome;
        var toHeight = isChecked
            ? ActualHeight - RightBorder.Margin.Bottom
            : PageButtonsRow.ActualHeight + chrome;

        var widthAnimation = new DoubleAnimation
        {
            From = fromWidth,
            To = toWidth,
            Duration = TimeSpan.FromSeconds(0.1),
            EasingFunction = new CubicEase( ) { EasingMode = EasingMode.EaseOut }
        };
        widthAnimation.Completed += (_, _) =>
        {
            RightBorder.BeginAnimation(WidthProperty, null);
            RightBorder.Width = double.NaN;
        };

        var heightAnimation = new DoubleAnimation
        {
            From = fromHeight,
            To = toHeight,
            Duration = TimeSpan.FromSeconds(0.1),
            EasingFunction = new CubicEase( ) { EasingMode = EasingMode.EaseOut }
        };
        // 写回按当前 IsChecked 判定:连点时旧动画的 Completed 被替换吞掉,回调必须指向最终状态
        heightAnimation.Completed += (_, _) =>
        {
            RightBorder.BeginAnimation(HeightProperty, null);
            RightBorder.Height = AllPageToggle.IsChecked == true ? ActualHeight - RightBorder.Margin.Bottom : double.NaN;
        };

        RightBorder.BeginAnimation(WidthProperty, widthAnimation);
        RightBorder.BeginAnimation(HeightProperty, heightAnimation);
    }

    // 供测试宿主退订静态 PageChanged 事件，避免跨用例持有窗口引用
    internal void OnPageChanged(object? sender, EventArgs e)
    {
        var target = App.PageIndex;

        UpdatePageUI( );
        PagePreviewsBox.SelectedIndex = target;

        ExitStamp( );
        SelectionBorder.Visibility = Visibility.Collapsed;

        CanvasNext.ResetTouchState( );
        CanvasNext.Strokes = App.CurrentPage.Strokes;
        CanvasNext.CurrentScale = App.CurrentPage.Scale;
        CanvasNext.OffsetX = App.CurrentPage.OffsetX;
        CanvasNext.OffsetY = App.CurrentPage.OffsetY;
        CanvasNext.SwapHistory(out _, App.CurrentPage.History);

        CanvasNext.SetDocumentPage(App.Document?.GetPage(target), App.DocumentBox);
    }

    private void PagePreviewsBoxSelectionChanged(object o, SelectionChangedEventArgs e)
    {
        if (PagePreviewsBox.SelectedIndex >= 0 && PagePreviewsBox.SelectedIndex != App.PageIndex)
        {
            SaveCurrentViewToPage( );
            App.SwitchPage(PagePreviewsBox.SelectedIndex);
        }
    }

    private void PrevPage(object o, RoutedEventArgs e)
    {
        if (App.PageIndex > 0)
        {
            SaveCurrentViewToPage( );
            App.SwitchPage(App.PageIndex - 1);
        }
    }

    private void NewNextPage(object o, RoutedEventArgs e)
    {
        if (App.PageIndex < App.Pages.Count - 1)
        {
            SaveCurrentViewToPage( );
            App.SwitchPage(App.PageIndex + 1);
        }
        else
        {
            SaveCurrentViewToPage( );
            App.NewPage( );
        }
    }

    private void SaveCurrentViewToPage( )
    {
        var page = App.CurrentPage;
        CanvasNext.SwapHistory(out var snapshot, null);
        page.History = snapshot;
        page.Scale = CanvasNext.CurrentScale;
        page.OffsetX = CanvasNext.OffsetX;
        page.OffsetY = CanvasNext.OffsetY;

        // 缩略图只在过期时重建（未改动的页翻过去再翻回来不再重算）
        page.RefreshPreview( );
    }

    private void UpdatePageUI( )
    {
        (AllPageToggle.Content as TextBlock)?.Text = $"{App.PageIndex + 1}/{App.Pages.Count}";
        NewNextPageButton.Tag = App.PageIndex < App.Pages.Count - 1 ? "\uE72A" : "\uE710";
        PrevPageButton.IsEnabled = App.PageIndex > 0;
    }

    private static DoubleAnimation HoldAnimation(double value)
    {
        return new DoubleAnimation
        {
            From = value,
            To = value,
            Duration = TimeSpan.FromSeconds(0.1)
        };
    }
}
