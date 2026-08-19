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
            MainInstruction = "轻白板 / LightBoard 26H4 Beta",
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
            To = isChecked ? ActualHeight - 20 : 57,
            Duration = TimeSpan.FromSeconds(0.1),
            EasingFunction = new CubicEase( ) { EasingMode = EasingMode.EaseInOut }
        };

        RightBorder.BeginAnimation(Border.HeightProperty, heightAnimation);
        TimeText.Visibility = isChecked ? Visibility.Collapsed : Visibility.Visible;
        PagePreviewsBox.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;
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

    private void CanvasNextStrokesChanged(object o, EventArgs e)
    {
        dirty = true;

        if (CanvasNext.Mode != InkCanvasNextMode.Select || CanvasNext.SelectedStrokes.Count == 0)
        {
            SelectionBorder.Visibility = Visibility.Collapsed;
        }
    }

    private void CanvasNextSelectionChanged(object o, EventArgs e)
    {
        if (CanvasNext.Mode != InkCanvasNextMode.Select || CanvasNext.SelectedStrokes.Count == 0)
        {
            SelectionBorder.Visibility = Visibility.Collapsed;
            return;
        }

        if (SelectionBorder.Parent is UIElement parent)
        {
            SelectionBorder.Visibility = Visibility.Visible;
            var position = Mouse.GetPosition(parent);
            SelectionBorder.Margin = new Thickness(position.X, position.Y, 0, 0);
        }
    }

    private void CloneClick(object o, RoutedEventArgs e)
    {
        CanvasNext.CloneSelected( );
    }

    private void CollapseExpandClick(object o, RoutedEventArgs e)
    {
        var flag = CollapseExpandButton.IsChecked == true;
        CollapseExpandIcon.Text = flag ? "\uE70E" : "\uE70D";

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
            From = flag ? 0 : RightBorder.ActualWidth - 52,
            To = flag ? RightBorder.ActualWidth - 52 : 0,
            Duration = TimeSpan.FromSeconds(0.1),
            EasingFunction = ease
        };

        LeftTransform.BeginAnimation(TranslateTransform.XProperty, animationLeft);
        CenterTransform.BeginAnimation(TranslateTransform.YProperty, animationCenter);
        RightTransform.BeginAnimation(TranslateTransform.XProperty, animationRight);
    }

    private void ColorRadioChecked(object o, RoutedEventArgs e)
    {
        if (o is not RadioButton { Background: SolidColorBrush brush })
        {
            return;
        }

        CanvasNext.Mode = InkCanvasNextMode.Ink;
        CanvasNext.DefaultDrawingAttributes.Color = brush.Color;
    }

    private void CopyClick(object o, RoutedEventArgs e)
    {
        CanvasNext.CopySelected( );
    }

    private void CutClick(object o, RoutedEventArgs e)
    {
        CanvasNext.CutSelected( );
        SelectionBorder.Visibility = Visibility.Collapsed;
    }

    private void DeleteClick(object o, RoutedEventArgs e)
    {
        CanvasNext.DeleteSelected( );
        SelectionBorder.Visibility = Visibility.Collapsed;
    }

    private void EraseAll(object o, RoutedEventArgs e)
    {
        CanvasNext.ClearMultiTouchVisuals( );
        CanvasNext.Strokes.Clear( );
    }

    private void HighLighterBoxClicked(object o, RoutedEventArgs e)
    {
        CanvasNext.DefaultDrawingAttributes.IsHighlighter = HighLighterToggle.IsChecked ?? false;
    }

    private void PasteClick(object o, RoutedEventArgs e)
    {
        CanvasNext.Paste( );
    }

    private void RedoButtonClick(object o, RoutedEventArgs e)
    {
        CanvasNext.Redo( );
    }

    private void UndoButtonClick(object o, RoutedEventArgs e)
    {
        CanvasNext.Undo( );
    }

    private void ThicknessRadioClick(object o, RoutedEventArgs e)
    {
        if (o is not RadioButton { MinWidth: double thickness })
        {
            return;
        }

        CanvasNext.DefaultDrawingAttributes.Width = CanvasNext.DefaultDrawingAttributes.Height = thickness;
    }

    private void ToolRadioChecked(object o, RoutedEventArgs e)
    {
        if (o is not RadioButton { Tag: string tag })
        {
            return;
        }

        CanvasNext.Mode = tag switch
        {
            "\uED60" => InkCanvasNextMode.EraseArea,
            "\uED61" => InkCanvasNextMode.EraseStroke,
            "\uEF20" => InkCanvasNextMode.Select,
            _ => CanvasNext.Mode,
        };
    }

    private void TransparentModeClick(object o, RoutedEventArgs e)
    {
        var mode = TransparentModeButton.IsChecked == true;
        var blackBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
        var borderBrush = new SolidColorBrush(Color.FromArgb(128, 0x2E, 0x2E, 0x2E));

        CanvasNext.Background = mode ? Brushes.Transparent : blackBrush;
        TimeText.Visibility = mode || AllPageToogle.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
        LeftBorder.Background = mode ? blackBrush : borderBrush;
        CenterBorder.Background = mode ? blackBrush : borderBrush;
        RightBorder.Background = mode ? blackBrush : borderBrush;
        TransparentModeText.Text = mode ? "\uE7C3" : "\uE729";
    }
}
