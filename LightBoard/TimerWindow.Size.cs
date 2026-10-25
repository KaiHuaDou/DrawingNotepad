using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace LightBoard;

public partial class TimerWindow
{
    private static readonly TimeSpan ResizeDuration = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// 非客户区尺寸，首次渲染后量得
    /// </summary>
    private Size chrome;

    private Size sizeTarget;
    private int resizeGeneration;
    private bool sizeReady;
    private bool fullscreenTransition;
    private bool resizePending;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        if (sizeReady)
        {
            return;
        }

        var content = ContentSize( );
        chrome = new Size(ActualWidth - content.Width, ActualHeight - content.Height);

        SizeToContent = SizeToContent.Manual;
        Width = ActualWidth;
        Height = ActualHeight;

        sizeTarget = new Size(Math.Ceiling(Width), Math.Ceiling(Height));
        sizeReady = true;
    }

    /// <summary>窗口与秒区的宽度同步过渡；秒区收窄即被裁掉，动画结束时才设为收起</summary>
    private void ResizeToContent( )
    {
        if (!sizeReady)
        {
            // 首次渲染前没有动画在飞，直接落位
            SecondsPanel.BeginAnimation(WidthProperty, null);
            SecondsPanel.Width = state != TimerState.Idle ? double.NaN : 0;
            return;
        }

        // 合并同一事件批内的重复进入，并且时钟生成挪出点击/点击链路，规避同拍先撤再加的时钟竞态
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

        var showSeconds = state != TimerState.Idle;

        if (WindowState != WindowState.Normal)
        {
            SecondsPanel.BeginAnimation(WidthProperty, null);
            SecondsPanel.Width = showSeconds ? double.NaN : 0;
            return;
        }

        // 动画进行中 Width 读数即动画当前值，从当前值续动画才不被 Actual 的滞后拉回去
        var fromSeconds = SecondsPanel.Width;

        // 量秒区自然宽度前先停掉进行中的宽度动画
        SecondsPanel.BeginAnimation(WidthProperty, null);

        var toSeconds = showSeconds ? Math.Ceiling(NaturalSecondsWidth( )) : 0;

        SecondsPanel.Width = toSeconds;

        var content = ContentSize( );
        var target = new Size(
            Math.Ceiling(content.Width + chrome.Width),
            Math.Ceiling(content.Height + chrome.Height)
        );

        // 进出全屏的这一拍直接切到目标尺寸，不播过渡动画
        if (fullscreenTransition)
        {
            fullscreenTransition = false;
            resizeGeneration++;    // 作废未完成动画的写回
            BeginAnimation(WidthProperty, null);
            BeginAnimation(HeightProperty, null);

            sizeTarget = target;
            Width = target.Width;
            Height = target.Height;
            return;
        }

        if (Math.Abs(target.Width - sizeTarget.Width) <= 0.5
            && Math.Abs(target.Height - sizeTarget.Height) <= 0.5
            && Math.Abs(toSeconds - fromSeconds) <= 0.5)
        {
            return;
        }

        sizeTarget = target;

        var generation = ++resizeGeneration;

        SecondsPanel.Width = fromSeconds;

        AnimateSize(SecondsPanel, WidthProperty, fromSeconds, toSeconds, generation);

        // 窗口尺寸已到位就不拆不建时钟，零长度时钟只会制造竞态
        if (Math.Abs(target.Width - Width) > 0.5)
        {
            AnimateSize(this, WidthProperty, Width, target.Width, generation);
        }

        if (Math.Abs(target.Height - Height) > 0.5)
        {
            AnimateSize(this, HeightProperty, Height, target.Height, generation);
        }
    }

    /// <summary>秒区按自身内容排版所需的宽度</summary>
    private double NaturalSecondsWidth( )
    {
        var width = SecondsPanel.Width;

        SecondsPanel.Width = double.NaN;
        SecondsPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var desired = SecondsPanel.DesiredSize.Width;
        SecondsPanel.Width = width;

        return desired;
    }

    private void AnimateSize(UIElement element, DependencyProperty property, double from, double to, int generation)
    {
        var animation = new DoubleAnimation(from, to, ResizeDuration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };

        animation.Completed += (o, e) =>
        {
            // 被更晚的动画取代时，基值由那次动画负责写回
            if (generation != resizeGeneration)
            {
                return;
            }

            element.SetValue(property, to);
            element.BeginAnimation(property, null);
        };

        element.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private Size ContentSize( )
    {
        var content = (FrameworkElement) Content;

        // 子级按上次约束缓存 DesiredSize 且同约束则直接旁路，同约束的 Measure(∞) 拿回旧值；
        // 整棵子树先打脏标记再量，保证拿回自然尺寸
        InvalidateMeasureTree(content);
        content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return content.DesiredSize;
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
}
