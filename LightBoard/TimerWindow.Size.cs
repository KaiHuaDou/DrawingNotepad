using System;
using System.Windows;
using System.Windows.Media.Animation;

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
        var showSeconds = state != TimerState.Idle;

        if (!sizeReady || WindowState != WindowState.Normal)
        {
            SecondsPanel.BeginAnimation(WidthProperty, null);
            SecondsPanel.Width = showSeconds ? double.NaN : 0;
            return;
        }

        // ActualWidth 存在排版下限，收起后仍停在自然宽上；动画进行中 Width 读数即动画当前值
        var fromSeconds = SecondsPanel.Width;

        // 进行中的宽度动画会接管 Width，量尺寸前先停掉
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
        AnimateSize(this, WidthProperty, ActualWidth, target.Width, generation);
        AnimateSize(this, HeightProperty, ActualHeight, target.Height, generation);
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

        // 约束相同时 Measure 可能命中缓存拿回旧 DesiredSize，先作废脏标记再量
        content.InvalidateMeasure( );
        content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        return content.DesiredSize;
    }
}
