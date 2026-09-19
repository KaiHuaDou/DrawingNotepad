using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

using InkCanvasNext;

using Ookii.Dialogs.Wpf;

namespace LightBoard;

public partial class MainWindow
{
    private void AboutClick(object o, RoutedEventArgs e)
    {
        using TaskDialog dialog = new( )
        {
            WindowTitle = "轻白板",
            MainInstruction = "轻白板 / LightBoard v1.0.0 RC",
            MainIcon = TaskDialogIcon.Information,
            Content =
            """
            源代码: <a href="https://github.com/KaiHuaDou/DrawingNotepad/">https://github.com/KaiHuaDou/DrawingNotepad/</a>        
            发布版本: <a href="https://github.com/KaiHuaDou/DrawingNotepad/releases/">https://github.com/KaiHuaDou/DrawingNotepad/releases/</a>
            """,
            EnableHyperlinks = true,
        };
        dialog.HyperlinkClicked += (o, e) => Process.Start(new ProcessStartInfo(e.Href) { UseShellExecute = true });
        dialog.Buttons.Add(new TaskDialogButton(ButtonType.Ok));
        dialog.ShowDialog( );
    }

    private void AllPageToogleClick(object o, RoutedEventArgs e)
    {
        if (o is not ToggleButton { IsChecked: bool isChecked })
        {
            return;
        }

        var heightAnimation = new DoubleAnimation
        {
            From = RightBorder.ActualHeight,
            To = isChecked ? ActualHeight - 10 : 55,
            Duration = TimeSpan.FromSeconds(0.1),
            EasingFunction = new CubicEase( ) { EasingMode = EasingMode.EaseInOut }
        };

        var widthAnimation = new DoubleAnimation
        {
            From = RightBorder.ActualWidth,
            To = isChecked ? RightBorder.ActualWidth + 64 : RightBorder.ActualWidth - 64,
            Duration = TimeSpan.FromSeconds(0.1),
            EasingFunction = new CubicEase( ) { EasingMode = EasingMode.EaseInOut }
        };

        TimeText.Visibility = isChecked ? Visibility.Collapsed : Visibility.Visible;

        if (isChecked)
        {
            // RightBorder.Background = Application.Current.FindResource("ContainerBrushSolid") as Brush;
        }
        else
        {
            PagePreviewsBox.Visibility = Visibility.Collapsed;
        }

        heightAnimation.Completed += (_, _) =>
        {
            if (isChecked)
            {
                PagePreviewsBox.Visibility = Visibility.Visible;
            }
            else
            {
               // RightBorder.Background = Application.Current.FindResource("ContainerBrush") as Brush;
            }
        };

        RightBorder.BeginAnimation(HeightProperty, heightAnimation);
        RightBorder.BeginAnimation(WidthProperty, widthAnimation);
    }

    private void CanvasNextCanRedoChanged(object o, DependencyPropertyChangedEventArgs e)
    {
        RedoButton.IsEnabled = CanvasNext.CanRedo;
    }

    private void CanvasNextCanUndoChanged(object o, DependencyPropertyChangedEventArgs e)
    {
        UndoButton.IsEnabled = CanvasNext.CanUndo;
    }

    private void CanvasNextSelectionChanged(object o, EventArgs e)
    {
        if (CanvasNext.Mode != InkCanvasNextMode.Select || CanvasNext.SelectedCount == 0)
        {
            SelectionBorder.Visibility = Visibility.Collapsed;
            return;
        }

        SelectionBorder.Visibility = Visibility.Visible;
        UpdateSelectionBorderPosition( );
    }

    private void CanvasNextStrokesChanged(object o, InkCanvasStrokesChangedEventArgs e)
    {
        Dirty = true;

        if (CanvasNext.Mode != InkCanvasNextMode.Select || CanvasNext.SelectedCount == 0)
        {
            SelectionBorder.Visibility = Visibility.Collapsed;
            return;
        }

        UpdateSelectionBorderPosition( );
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

        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var animationLeft = new DoubleAnimation
        {
            From = flag ? 0 : -LeftBorder.ActualWidth,
            To = flag ? -LeftBorder.ActualWidth : 0,
            Duration = TimeSpan.FromSeconds(0.1),
            EasingFunction = ease
        };
        var animationCenter = new DoubleAnimation
        {
            From = flag ? 0 : CenterBorder.ActualHeight,
            To = flag ? CenterBorder.ActualHeight : 0,
            Duration = TimeSpan.FromSeconds(0.1),
            EasingFunction = ease
        };
        var animationRight = new DoubleAnimation
        {
            From = flag ? 0 : RightBorder.ActualWidth - 87,
            To = flag ? RightBorder.ActualWidth - 87 : 0,
            Duration = TimeSpan.FromSeconds(0.1),
            EasingFunction = ease
        };

        LeftTransform.BeginAnimation(TranslateTransform.XProperty, animationLeft);
        CenterTransform.BeginAnimation(TranslateTransform.YProperty, animationCenter);
        RightTransform.BeginAnimation(TranslateTransform.XProperty, animationRight);
    }

    private void ColorRadioChecked(object o, RoutedEventArgs e)
    {
        ExitStamp( );

        if (o is not RadioButton { Background: SolidColorBrush brush } radio)
        {
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

    private void CopyClick(object o, RoutedEventArgs e)
    {
        CanvasNext.CopySelected( );
    }

    private void CutClick(object o, RoutedEventArgs e)
    {
        ExitStamp( );

        CanvasNext.CutSelected( );
        SelectionBorder.Visibility = Visibility.Collapsed;
    }

    private void DeleteClick(object o, RoutedEventArgs e)
    {
        ExitStamp( );

        CanvasNext.DeleteSelected( );
        SelectionBorder.Visibility = Visibility.Collapsed;
    }

    private void EraseAll(object o, RoutedEventArgs e)
    {
        ExitStamp( );

        CanvasNext.ClearMultiTouchVisuals( );
        CanvasNext.Strokes.Clear( );
    }

    private void MainWindowKeyUp(object o, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ExitStamp( );
        }
    }

    private void MoreToggleClick(object o, RoutedEventArgs e)
    {
        var isChecked = MoreToggle.IsChecked == true;

        var heightAnimation = new DoubleAnimation
        {
            From = CenterBorder.ActualHeight,
            To = isChecked ? (CenterBorder.ActualHeight - 10) * 2 + 10 : (CenterBorder.ActualHeight - 10) / 2 + 10,
            Duration = TimeSpan.FromSeconds(0.1),
            EasingFunction = new CubicEase( ) { EasingMode = EasingMode.EaseInOut }
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
    private void RedoButtonClick(object o, RoutedEventArgs e)
    {
        CanvasNext.Redo( );
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

        // 记录点击前的模式：用于识别"再点当前已激活的模式"（回退到画笔）。
        // 不能在退出高亮后再判断：从高亮态点模式 radio 属于新勾选，radio 也会被选中。
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

    private void ThicknessRadioClick(object o, RoutedEventArgs e)
    {
        ExitStamp( );

        if (o is not RadioButton { MinWidth: double thickness } radio)
        {
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

    private static readonly Brush ContainerBrush = (Application.Current.FindResource("ContainerBrush") as Brush)!;
    private static readonly Brush ContainerBrushSolid = (Application.Current.FindResource("ContainerBrushSolid") as Brush)!;
    private static readonly Brush MajorGridBrush = (Application.Current.FindResource("MajorGridBrush") as Brush)!;
    private static readonly Brush CanvasNextBackgroundBrush = MajorGridBrush;

    private void TransparentModeClick(object o, RoutedEventArgs e)
    {
        var mode = (TransparentModeButton.Tag as string) == "\uE729";

        CanvasNext.Background = mode ? Brushes.Transparent : CanvasNextBackgroundBrush;
        TimeText.Visibility = mode || AllPageToogle.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
        LeftBorder.Background = mode ? ContainerBrushSolid : ContainerBrush;
        CenterBorder.Background = mode ? ContainerBrushSolid : ContainerBrush;
        RightBorder.Background = mode ? ContainerBrushSolid : ContainerBrush;
        PassThroughBorder.Visibility = mode ? Visibility.Visible : Visibility.Collapsed;
        TransparentModeButton.Tag = mode ? "\uE7C3" : "\uE729";
        Topmost = mode;
    }

    private void UndoButtonClick(object o, RoutedEventArgs e)
    {
        CanvasNext.Undo( );
    }
}
