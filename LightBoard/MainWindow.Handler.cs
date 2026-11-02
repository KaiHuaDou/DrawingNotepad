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
    private static readonly Brush ContainerBrush = (Application.Current.FindResource("ContainerBrush") as Brush)!;
    private static readonly Brush ContainerBrushSolid = (Application.Current.FindResource("ContainerBrushSolid") as Brush)!;
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

    private void ApplyBackground( )
    {
        var transparent = TransparentBackgroundRadio.IsChecked == true;
#if OPENCV
        if (VideoBackgroundRadio.IsChecked == true)
        {
            StartVideoStand( );
        }
        else if (videoPage is not null)
        {
            // 改选其他背景：视频停止，本页改用所选背景
            videoPage = null;
            StopVideoStand( );
        }
#endif

        RefreshVideoBackground( );

        TimeText.Visibility = transparent || AllPageToggle.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
        SwitchOutButton.Visibility = transparent ? Visibility.Collapsed : Visibility.Visible;
        LeftBorder.Background = transparent ? ContainerBrushSolid : ContainerBrush;
        CenterBorder.Background = transparent ? ContainerBrushSolid : ContainerBrush;
        RightBorder.Background = transparent ? ContainerBrushSolid : ContainerBrush;
        PassThroughBorder.Visibility = transparent ? Visibility.Visible : Visibility.Collapsed;
        Topmost = transparent;
    }

    // 背景单选对应的画布底色
    private Brush BackgroundBrush( )
    {
        return WhiteBackgroundRadio.IsChecked == true ? Brushes.White
            : BlackBackgroundRadio.IsChecked == true ? Brushes.Black
            : TransparentBackgroundRadio.IsChecked == true ? Brushes.Transparent
            : MajorGridBrush;
    }

    // 视频背景仅在含 OpenCV 的构建中存在；nocv 构建的画布底色只随背景单选走
#if !OPENCV
    private void RefreshVideoBackground( )
    {
        CanvasNext.Background = BackgroundBrush( );
    }
#endif

    private void BackgroundRadioClick(object o, RoutedEventArgs e)
    {
        if (o is not RadioButton { IsChecked: true })
        {
            return;
        }

#if OPENCV
        // 组互斥在 Click 之前已取消其余单选，最近勾选的全局背景只能在这里记录
        if (!ReferenceEquals(o, VideoBackgroundRadio))
        {
            globalBackgroundRadio = (RadioButton) o;
        }
#endif

        ApplyBackground( );
    }

    private void CloneClick(object o, RoutedEventArgs e)
    {
        if (CloneButton.IsChecked == true)
        {
            if (!CanvasNext.HasSelection)
            {
                CloneButton.IsChecked = false;
                return;
            }

            PasteButton.IsChecked = false;
            CanvasNext.StampAction = StampAction.Clone;
        }
        else if (CanvasNext.StampAction == StampAction.Clone)
        {
            ExitStamp( );
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
        ExitStamp( );

        if (o is not RadioButton { Background: SolidColorBrush brush } radio)
        {
            return;
        }

        // 形状模式下点当前已选中的颜色即取消形状回画笔；点其他颜色仅换笔保持形状
        if (IsShapeMode(Mode) && ReferenceEquals(radio, colorRadio))
        {
            Mode = InkCanvasNextMode.Ink;
            SyncToolState( );
            return;
        }

        if (Mode == InkCanvasNextMode.Highlighter)
        {
            ExitHighlighter( );
        }

        if (IsToolMode(Mode))
        {
            Mode = InkCanvasNextMode.Ink;
        }

        colorRadio = radio;
        pen = pen with { Color = brush.Color };
        SyncToolState( );
    }

    private void DebugLayerClick(object o, RoutedEventArgs e)
    {
        var isChecked = DebugLayerMenu.IsChecked;
        DebugLayer.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;
    }

    private void EraseAll(object o, RoutedEventArgs e)
    {
        ExitStamp( );

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

        ExitStamp( );

        // 记录点击前的模式：用于识别"再点当前已激活的模式"（回退到画笔） 不能在退出高亮后再判断：从高亮态点模式 radio 属于新勾选，radio 也会被选中。
        var wasMode = Mode;

        if (wasMode == InkCanvasNextMode.Highlighter)
        {
            ExitHighlighter( );
        }

        var mode = radio.Tag switch
        {
            "\uE73C" => InkCanvasNextMode.Line,
            "\uEA3A" => InkCanvasNextMode.Circle,
            "\uED60" => InkCanvasNextMode.EraseArea,
            "\uED61" => InkCanvasNextMode.EraseStroke,
            "\uEF20" => InkCanvasNextMode.Select,
            _ => InkCanvasNextMode.Ink
        };

        Mode = wasMode == mode ? InkCanvasNextMode.Ink : mode;
        SyncToolState( );
    }

    private void MoreToggleClick(object o, RoutedEventArgs e)
    {
        ApplyMoreToggle(MoreToggle.IsChecked == true);
    }

    private void ApplyMoreToggle(bool expanded)
    {
        MoreToggle.IsChecked = expanded;

        var heightAnimation = new DoubleAnimation
        {
            From = CenterBorder.ActualHeight,
            To = expanded ? (CenterBorder.ActualHeight - 10) * 2 + 10 : (CenterBorder.ActualHeight - 10) / 2 + 10,
            Duration = TimeSpan.FromSeconds(0.1),
            EasingFunction = new CubicEase( ) { EasingMode = expanded ? EasingMode.EaseIn : EasingMode.EaseOut }
        };

        CenterBorder.BeginAnimation(HeightProperty, heightAnimation);
    }

    private void PasteClick(object o, RoutedEventArgs e)
    {
        if (PasteButton.IsChecked == true)
        {
            if (!Clipboard.ContainsData(StrokeCollection.InkSerializedFormat))
            {
                PasteButton.IsChecked = false;
                return;
            }

            CloneButton.IsChecked = false;

            // 激活粘贴印章时退出其他模式回画笔；剪贴板为空时粘贴未生效，不扰动当前模式
            if (Mode == InkCanvasNextMode.Highlighter)
            {
                ExitHighlighter( );
            }

            Mode = InkCanvasNextMode.Ink;
            SyncToolState( );
            CanvasNext.StampAction = StampAction.Paste;
        }
        else if (CanvasNext.StampAction == StampAction.Paste)
        {
            ExitStamp( );
        }
    }

    private void SettingsClick(object o, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow { Owner = this };
        dialog.ShowDialog( );
    }

    private void ThicknessRadioClick(object o, RoutedEventArgs e)
    {
        ExitStamp( );

        if (o is not RadioButton { MinWidth: double thickness } radio)
        {
            return;
        }

        // 形状模式下点当前已选中的粗细即取消形状回画笔；点其他粗细仅换笔保持形状
        if (IsShapeMode(Mode) && ReferenceEquals(radio, thicknessRadio))
        {
            Mode = InkCanvasNextMode.Ink;
            SyncToolState( );
            return;
        }

        if (Mode == InkCanvasNextMode.Highlighter)
        {
            ExitHighlighter( );
        }

        if (IsToolMode(Mode))
        {
            Mode = InkCanvasNextMode.Ink;
        }

        thicknessRadio = radio;
        pen = pen with { Width = thickness };
        SyncToolState( );
    }

    private void TimerMenuClick(object o, RoutedEventArgs e)
    {
        if (timerWindow?.IsLoaded == true)
        {
            timerWindow.Activate( );
            return;
        }

        timerWindow = new TimerWindow( );
        timerWindow.Show( );
    }
}
