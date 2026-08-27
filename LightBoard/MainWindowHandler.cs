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
            From = flag ? 0 : RightBorder.ActualWidth - 85,
            To = flag ? RightBorder.ActualWidth - 85 : 0,
            Duration = TimeSpan.FromSeconds(0.1),
            EasingFunction = ease
        };

        LeftTransform.BeginAnimation(TranslateTransform.XProperty, animationLeft);
        CenterTransform.BeginAnimation(TranslateTransform.YProperty, animationCenter);
        RightTransform.BeginAnimation(TranslateTransform.XProperty, animationRight);
    }

    private void ColorRadioChecked(object o, RoutedEventArgs e)
    {
        if (o is not RadioButton { Background: SolidColorBrush brush } radio)
        {
            return;
        }

        if (mode == InkCanvasNextMode.Highlighter)
        {
            ExitHighlighter( );
        }

        // 点击颜色权当切换回 Pen（普通 Ink，不含 Line/Circle）
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

    private sealed record PenProfile(Color Color, double Width, bool IsHighlighter);

    private PenProfile pen = new(Color.FromRgb(0xE6, 0xE6, 0xE6), 3, false);
    private static readonly PenProfile highlighterProfile = new(Colors.Yellow, 36, true);

    // 显式状态机：Mode 是唯一真值来源
    private InkCanvasNextMode mode = InkCanvasNextMode.Ink;

    private RadioButton? colorRadio;
    private RadioButton? thicknessRadio;

    // 进入 Highlighter 前保存的工具状态（pen 在高亮期间不变，无需保存）；退出时整体还原
    private sealed record ToolSnapshot(InkCanvasNextMode Mode, RadioButton? ColorRadio, RadioButton? ThicknessRadio);
    private ToolSnapshot? highlighterBackup;

    private static bool IsToolMode(InkCanvasNextMode m)
    {
        return m switch
        {
            InkCanvasNextMode.EraseStroke or InkCanvasNextMode.EraseArea or InkCanvasNextMode.Select => true,
            _ => false
        };
    }

    private void HighLighterBoxClicked(object o, RoutedEventArgs e)
    {
        if (HighLighterToggle.IsChecked == true)
        {
            EnterHighlighter( );
        }
        else
        {
            ExitHighlighter( );
        }
    }

    private void EnterHighlighter( )
    {
        highlighterBackup = new ToolSnapshot(mode, colorRadio, thicknessRadio);
        mode = InkCanvasNextMode.Highlighter;
        SyncToolState( );
    }

    private void ExitHighlighter( )
    {
        if (highlighterBackup is not null)
        {
            mode = highlighterBackup.Mode;
            colorRadio = highlighterBackup.ColorRadio;
            thicknessRadio = highlighterBackup.ThicknessRadio;
        }

        // Line/Circle 是 Pen 的附加，退出 Highlighter 时不恢复
        if (mode is InkCanvasNextMode.Line or InkCanvasNextMode.Circle)
        {
            mode = InkCanvasNextMode.Ink;
        }
    }

    // 状态机唯一出口：从 Mode 推导 UI 勾选与画布模式
    private void SyncToolState( )
    {
        var hl = mode == InkCanvasNextMode.Highlighter;
        HighLighterToggle?.IsChecked = hl;

        if (hl)
        {
            // Highlighter 模式下除 Highlighter 本身外其余一律不选中
            colorRadio?.IsChecked = false;
            thicknessRadio?.IsChecked = false;
            EraseAreaRadio?.IsChecked = false;
            EraseStrokeRadio?.IsChecked = false;
            SelectRadio?.IsChecked = false;
            LineToggle?.IsChecked = false;
            CircleToggle?.IsChecked = false;
        }
        else
        {
            colorRadio?.IsChecked = true;
            thicknessRadio?.IsChecked = true;
            EraseAreaRadio?.IsChecked = mode == InkCanvasNextMode.EraseArea;
            EraseStrokeRadio?.IsChecked = mode == InkCanvasNextMode.EraseStroke;
            SelectRadio?.IsChecked = mode == InkCanvasNextMode.Select;
            LineToggle?.IsChecked = mode == InkCanvasNextMode.Line;
            CircleToggle?.IsChecked = mode == InkCanvasNextMode.Circle;
        }

        CommitCanvas( );
    }

    private void CommitCanvas( )
    {
        var p = mode == InkCanvasNextMode.Highlighter ? highlighterProfile : pen;
        var drawingAttr = CanvasNext.DefaultDrawingAttributes;
        drawingAttr.Color = p.Color;
        drawingAttr.Width = drawingAttr.Height = p.Width;
        drawingAttr.IsHighlighter = p.IsHighlighter;
        CanvasNext.Mode = mode == InkCanvasNextMode.Highlighter ? InkCanvasNextMode.Ink : mode;
    }

    private void MoreToggleClick(object o, RoutedEventArgs e)
    {
        var isChecked = MoreToggle.IsChecked == true;
        var heightAnimation = new DoubleAnimation
        {
            From = CenterBorder.ActualHeight,
            To = isChecked ? CenterBorder.ActualHeight * 2 : CenterBorder.ActualHeight / 2,
            Duration = TimeSpan.FromSeconds(0.1),
            EasingFunction = new CubicEase( ) { EasingMode = EasingMode.EaseInOut }
        };

        CenterBorder.BeginAnimation(HeightProperty, heightAnimation);
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
        if (o is not RadioButton { MinWidth: double thickness } radio)
        {
            return;
        }

        if (mode == InkCanvasNextMode.Highlighter)
        {
            ExitHighlighter( );
        }

        // 点击粗细权当切换回 Pen（普通 Ink，不含 Line/Circle）
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
        if (o is not RadioButton radio)
        {
            return;
        }

        if (mode == InkCanvasNextMode.Highlighter)
        {
            ExitHighlighter( );
        }

        // 切出 Pen：Line/Circle 作为 Pen 附加，此时已取消（Mode 直接落为工具模式）
        mode = radio.Tag switch
        {
            "\uED60" => InkCanvasNextMode.EraseArea,
            "\uED61" => InkCanvasNextMode.EraseStroke,
            "\uEF20" => InkCanvasNextMode.Select,
            _ => InkCanvasNextMode.Ink
        };

        SyncToolState( );
    }

    private void ShapeToggleClick(object o, RoutedEventArgs e)
    {
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
            // Line/Circle 是 Pen 的附加且互斥：启用即切到 Pen 对应子模式
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
            // 关闭形状回到普通 Pen
            mode = InkCanvasNextMode.Ink;
        }

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
}
