using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace LightBoard;

// 窗口尺寸交给 SizeToContent 跟随内容，这里只驱动秒位的展开与收起
public partial class TimerWindow
{
    private static readonly TimeSpan ResizeDuration = TimeSpan.FromMilliseconds(100);

    private bool resizePending;

    /// <summary>
    /// 同一事件批内只排一次重排；时钟生成挪出点击链路，规避同拍先撤再加的竞态
    /// </summary>
    private void ResizeToContent( )
    {
        if (resizePending)
        {
            return;
        }

        resizePending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(ApplyResize));
    }

    private void ApplyResize( )
    {
        resizePending = false;

        var showSeconds = !viewModel.IsIdle;

        // 全屏时数字区不受窗口约束，秒位按内容取宽
        if (WindowState != WindowState.Normal)
        {
            SecondsPanel.BeginAnimation(WidthProperty, null);
            SecondsPanel.Width = showSeconds ? double.NaN : 0;
            return;
        }

        // 动画进行中 Width 读数即动画当前值；自动宽度时取排版后的实际宽度接力
        var fromSeconds = double.IsFinite(SecondsPanel.Width) ? SecondsPanel.Width : SecondsPanel.ActualWidth;

        SecondsPanel.BeginAnimation(WidthProperty, null);

        var toSeconds = showSeconds ? Math.Ceiling(NaturalSecondsWidth( )) : 0;

        // 基值直接落到目标，动画以 Stop 收尾：结束或被打断后读到的都是目标宽度
        SecondsPanel.Width = toSeconds;

        if (Math.Abs(toSeconds - fromSeconds) <= 0.5)
        {
            return;
        }

        var animation = new DoubleAnimation(fromSeconds, toSeconds, ResizeDuration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop,
        };

        SecondsPanel.BeginAnimation(WidthProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    /// <summary>秒区按自身内容排版所需的宽度</summary>
    private double NaturalSecondsWidth( )
    {
        var width = SecondsPanel.Width;

        SecondsPanel.Width = double.NaN;

        // 子级按上次约束缓存 DesiredSize，同约束再量会拿回旧值；整棵子树先打脏再量
        InvalidateMeasureTree(SecondsPanel);
        SecondsPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var desired = SecondsPanel.DesiredSize.Width;
        SecondsPanel.Width = width;

        return desired;
    }

    private static void InvalidateMeasureTree(DependencyObject node)
    {
        if (node is UIElement element)
        {
            element.InvalidateMeasure( );
        }

        var count = VisualTreeHelper.GetChildrenCount(node);

        for (var i = 0; i < count; i++)
        {
            InvalidateMeasureTree(VisualTreeHelper.GetChild(node, i));
        }
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);

        ResizeToContent( );
    }
}
