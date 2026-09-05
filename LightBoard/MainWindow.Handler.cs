using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
            To = isChecked ? ActualHeight - 32 : 48,
            Duration = TimeSpan.FromSeconds(0.1),
            EasingFunction = new CubicEase( ) { EasingMode = EasingMode.EaseInOut }
        };

        TimeText.Visibility = isChecked ? Visibility.Collapsed : Visibility.Visible;
        heightAnimation.Completed += (_, _) => PagePreviewsBox.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;

        RightBorder.BeginAnimation(HeightProperty, heightAnimation);
    }

#pragma warning disable IDE0060

    private void CanvasNextCanRedoChanged(object o, DependencyPropertyChangedEventArgs e)
    {
        RedoButton.IsEnabled = CanvasNext.CanRedo;
    }

    private void CanvasNextCanUndoChanged(object o, DependencyPropertyChangedEventArgs e)
    {
        UndoButton.IsEnabled = CanvasNext.CanUndo;
    }

#pragma warning restore IDE0060

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
        dirty = true;

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

        if (mode == InkCanvasNextMode.Highlighter)
        {
            ExitHighlighter( );
        }

        if (IsToolMode(mode))
        {
            mode = InkCanvasNextMode.Ink;
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
            if (!CanvasNext.HasClipboardStrokes)
            {
                PasteButton.IsChecked = false;
                return;
            }

            CloneButton.IsChecked = false;
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

    private void ShapeToggleClick(object o, RoutedEventArgs e)
    {
        ExitStamp( );

        if (o is not ToggleButton toggle)
        {
            return;
        }

        if (mode == InkCanvasNextMode.Highlighter)
        {
            ExitHighlighter( );
        }

        if (toggle.IsChecked == true)
        {
            if (ReferenceEquals(toggle, LineToggle))
            {
                mode = InkCanvasNextMode.Line;
                CircleToggle.IsChecked = false;
            }
            else
            {
                mode = InkCanvasNextMode.Circle;
                LineToggle.IsChecked = false;
            }
        }
        else
        {
            mode = InkCanvasNextMode.Ink;
        }

        SyncToolState( );
    }

    private void ThicknessRadioClick(object o, RoutedEventArgs e)
    {
        ExitStamp( );

        if (o is not RadioButton { MinWidth: double thickness } radio)
        {
            return;
        }

        if (mode == InkCanvasNextMode.Highlighter)
        {
            ExitHighlighter( );
        }

        if (IsToolMode(mode))
        {
            mode = InkCanvasNextMode.Ink;
        }

        thicknessRadio = radio;
        pen = pen with { Width = thickness };
        SyncToolState( );
    }

    private void ToolRadioChecked(object o, RoutedEventArgs e)
    {
        ExitStamp( );

        if (o is not RadioButton radio)
        {
            return;
        }

        if (mode == InkCanvasNextMode.Highlighter)
        {
            ExitHighlighter( );
        }

        mode = radio.Tag switch
        {
            "\uED60" => InkCanvasNextMode.EraseArea,
            "\uED61" => InkCanvasNextMode.EraseStroke,
            "\uEF20" => InkCanvasNextMode.Select,
            _ => InkCanvasNextMode.Ink
        };

        SyncToolState( );
    }

    private void TransparentModeClick(object o, RoutedEventArgs e)
    {
        var mode = (TransparentModeButton.Tag as string) == "\uE729";
        var blackBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
        var borderBrush = new SolidColorBrush(Color.FromArgb(128, 0x2E, 0x2E, 0x2E));

        CanvasNext.Background = mode ? Brushes.Transparent : blackBrush;
        TimeText.Visibility = mode || AllPageToogle.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
        LeftBorder.Background = mode ? blackBrush : borderBrush;
        CenterBorder.Background = mode ? blackBrush : borderBrush;
        RightBorder.Background = mode ? blackBrush : borderBrush;
        TransparentModeButton.Tag = mode ? "\uE7C3" : "\uE729";
    }

    private void UndoButtonClick(object o, RoutedEventArgs e)
    {
        CanvasNext.Undo( );
    }
}
