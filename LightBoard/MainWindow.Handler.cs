using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Animation;

using InkCanvasNext;

using Ookii.Dialogs.Wpf;

namespace LightBoard;

public partial class MainWindow
{
    private static readonly Brush MajorGridBrush = (Application.Current.FindResource("MajorGridBrush") as Brush)!;

    private void AboutClick(object o, RoutedEventArgs e)
    {
        using TaskDialog dialog = new( )
        {
            WindowTitle = "轻白板",
            MainInstruction = $"轻白板 / LightBoard v{BuildInfo.Version}",
            MainIcon = TaskDialogIcon.Information,
            Content =
            $"""
            OpenCV: {(BuildInfo.WithOpenCv ? "启用" : "未启用")}
            架构: {BuildInfo.Architecture}
            运行时: {(BuildInfo.SelfContained ? "自包含" : "框架依赖")}
            提交: {BuildInfo.Commit}
            源代码: <a href="https://github.com/KaiHuaDou/DrawingNotepad/">https://github.com/KaiHuaDou/DrawingNotepad/</a>
            发布版本: <a href="https://github.com/KaiHuaDou/DrawingNotepad/releases/">https://github.com/KaiHuaDou/DrawingNotepad/releases/</a>
            """,
            EnableHyperlinks = true,
        };
        dialog.HyperlinkClicked += (o, e) => Process.Start(new ProcessStartInfo(e.Href) { UseShellExecute = true });
        dialog.Buttons.Add(new TaskDialogButton(ButtonType.Ok));
        dialog.ShowDialog( );
    }

    private void CloneClick(object o, RoutedEventArgs e)
    {
        if (CloneButton.IsChecked == true)
        {
            // 无选区时不改动印章：已有粘贴保持激活
            if (CanvasNext.HasSelection)
            {
                CanvasNext.DrawingAttributes = CanvasNext.DrawingAttributes with { Stamp = StampAction.Clone };
            }
            else
            {
                RefreshToolChecks( );
            }
        }
        else if (CanvasNext.DrawingAttributes.Stamp == StampAction.Clone)
        {
            CanvasNext.DrawingAttributes = CanvasNext.DrawingAttributes with { Stamp = StampAction.None };
        }
        else
        {
            RefreshToolChecks( );
        }
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

    private void ColorRadioChecked(object o, RoutedEventArgs e)
    {
        if (o is not RadioButton { Background: SolidColorBrush brush } radio)
        {
            return;
        }

        var tool = CanvasNext.DrawingAttributes;

        // 形状模式下点当前已选中的颜色即取消形状回画笔；点其他颜色仅换笔保持形状
        if (IsShapeMode(tool.Mode) && ReferenceEquals(radio, colorRadio))
        {
            CanvasNext.DrawingAttributes = tool with
            {
                Mode = InkCanvasNextMode.Ink,
                Stamp = StampAction.None,
                DrawingAttributes = pen
            };
            return;
        }

        // 退出荧光笔时从普通笔预设起改，避免带上高亮的 36 DIP 与黄色
        var attributes = tool.DrawingAttributes.IsHighlighter ? pen.Clone( ) : tool.DrawingAttributes;
        attributes.Color = brush.Color;
        attributes.IsHighlighter = false;
        attributes.IgnorePressure = false;
        pen = attributes;

        colorRadio = radio;
        CanvasNext.DrawingAttributes = tool with
        {
            DrawingAttributes = attributes,
            Stamp = StampAction.None,
            Mode = IsToolMode(tool.Mode) ? InkCanvasNextMode.Ink : tool.Mode
        };
    }

    private void DebugLayerClick(object o, RoutedEventArgs e)
    {
        var isChecked = DebugLayerMenu.IsChecked;
        DebugLayer.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;
    }

    private void EraseAll(object o, RoutedEventArgs e)
    {
        CanvasNext.DrawingAttributes = CanvasNext.DrawingAttributes with { Stamp = StampAction.None };

        CanvasNext.ClearMultiTouchVisuals( );
        CanvasNext.Strokes.Clear( );
    }

    // 工具（擦除/选择）与形状（线/圆）是同一排他单选组（EditGroup）：
    // 点选即激活；再点当前已激活的选项回退到画笔
    private void ModeRadioChecked(object o, RoutedEventArgs e)
    {
        if (o is not RadioButton radio)
        {
            return;
        }

        // 再点当前已激活的模式即回画笔；荧光笔期间模式停在绘制，点任何模式都算新激活
        var wasMode = CanvasNext.DrawingAttributes.Mode;

        var mode = radio.Tag switch
        {
            "\uE73C" => InkCanvasNextMode.Line,
            "\uEA3A" => InkCanvasNextMode.Circle,
            "\uED60" => InkCanvasNextMode.EraseArea,
            "\uED61" => InkCanvasNextMode.EraseStroke,
            "\uEF20" => InkCanvasNextMode.Select,
            _ => InkCanvasNextMode.Ink
        };

        CanvasNext.DrawingAttributes = CanvasNext.DrawingAttributes with
        {
            Mode = wasMode == mode ? InkCanvasNextMode.Ink : mode,
            Stamp = StampAction.None,
            DrawingAttributes = pen
        };
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

    private void PasteClick(object o, RoutedEventArgs e)
    {
        if (PasteButton.IsChecked == true)
        {
            if (Clipboard.ContainsData(StrokeCollection.InkSerializedFormat))
            {
                // 激活粘贴印章时退出其他模式回画笔
                CanvasNext.DrawingAttributes = CanvasNext.DrawingAttributes with
                {
                    DrawingAttributes = pen,
                    Stamp = StampAction.Paste,
                    Mode = InkCanvasNextMode.Ink
                };
            }
            else
            {
                RefreshToolChecks( );
            }
        }
        else if (CanvasNext.DrawingAttributes.Stamp == StampAction.Paste)
        {
            CanvasNext.DrawingAttributes = CanvasNext.DrawingAttributes with { Stamp = StampAction.None };
        }
        else
        {
            RefreshToolChecks( );
        }
    }

    private void SettingsClick(object o, RoutedEventArgs e)
    {
        if (settingsWindow?.IsLoaded == true)
        {
            settingsWindow.Activate( );
            return;
        }

        settingsWindow = new SettingsWindow { Owner = this };
        settingsWindow.Closed += (_, _) => FitToCurve = SettingsStore.Current.EffectiveFitToCurve;
        settingsWindow.Show( );
    }

    private void ThicknessRadioClick(object o, RoutedEventArgs e)
    {
        if (o is not RadioButton { MinWidth: double thickness } radio)
        {
            return;
        }

        var tool = CanvasNext.DrawingAttributes;

        // 形状模式下点当前已选中的粗细即取消形状回画笔；点其他粗细仅换笔保持形状
        if (IsShapeMode(tool.Mode) && ReferenceEquals(radio, thicknessRadio))
        {
            CanvasNext.DrawingAttributes = tool with
            {
                Mode = InkCanvasNextMode.Ink,
                Stamp = StampAction.None,
                DrawingAttributes = pen
            };
            return;
        }

        // 退出荧光笔时从普通笔预设起改，避免带上高亮的 36 DIP 与黄色
        var attributes = tool.DrawingAttributes.IsHighlighter ? pen.Clone( ) : tool.DrawingAttributes;
        attributes.Width = attributes.Height = thickness;
        attributes.IsHighlighter = false;
        attributes.IgnorePressure = false;
        pen = attributes;

        thicknessRadio = radio;
        CanvasNext.DrawingAttributes = tool with
        {
            DrawingAttributes = attributes,
            Stamp = StampAction.None,
            Mode = IsToolMode(tool.Mode) ? InkCanvasNextMode.Ink : tool.Mode
        };
    }

    private void TimerMenuClick(object o, RoutedEventArgs e)
    {
        if (timerWindow?.IsLoaded == true)
        {
            timerWindow.Activate( );
            return;
        }

        timerWindow = new TimerWindow { Owner = this };
        timerWindow.Show( );
    }

    private void LotteryMenuClick(object o, RoutedEventArgs e)
    {
        if (lotteryWindow?.IsLoaded == true)
        {
            lotteryWindow.Activate( );
            return;
        }

        lotteryWindow = new LotteryWindow { Owner = this };
        lotteryWindow.Show( );
    }
}
