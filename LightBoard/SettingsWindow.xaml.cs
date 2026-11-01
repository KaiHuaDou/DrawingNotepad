using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using Ookii.Dialogs.Wpf;

namespace LightBoard;

public partial class SettingsWindow : Window
{
    private readonly SettingsEditor editor;

    public SettingsWindow( )
    {
        MaxHeight = SystemParameters.PrimaryScreenHeight * 3 / 4;

        InitializeComponent( );
        editor = new SettingsEditor(SettingsStore.Current);
        DataContext = editor;
    }

    private void ResetClick(object o, RoutedEventArgs e)
    {
        editor.Reset( );
    }

    private void StepDownClick(object o, RoutedEventArgs e)
    {
        if (o is Button { Tag: SettingsField field })
        {
            field.Step(-1);
        }
    }

    private void StepUpClick(object o, RoutedEventArgs e)
    {
        if (o is Button { Tag: SettingsField field })
        {
            field.Step(1);
        }
    }

    private void OkClick(object o, RoutedEventArgs e)
    {
        if (editor.HasErrors)
        {
            FocusFirstInvalid( );
            return;
        }

        var updated = editor.Build( );
        if (updated != SettingsStore.Current)
        {
            try
            {
                SettingsStore.Save(updated);
            }
            catch (Exception ex)
            {
                App.LogException(ex);
                App.ShowException(ex, "无法写入设置");
                return;
            }

            ShowRestartNotice( );
        }

        Close( );
    }

    private void FocusFirstInvalid( )
    {
        foreach (var box in FindTextBoxes(this))
        {
            if (box.DataContext is SettingsField { HasErrors: true })
            {
                box.Focus( );
                box.BringIntoView( );
                return;
            }
        }
    }

    private static IEnumerable<TextBox> FindTextBoxes(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBox box)
            {
                yield return box;
            }

            foreach (var descendant in FindTextBoxes(child))
            {
                yield return descendant;
            }
        }
    }

    private static void ShowRestartNotice( )
    {
        using TaskDialog dialog = new( )
        {
            WindowTitle = "轻白板",
            MainInstruction = "设置已保存",
            MainIcon = TaskDialogIcon.Information,
            Content = "重启轻白板后按新设置运行。",
        };
        dialog.Buttons.Add(new TaskDialogButton(ButtonType.Ok));
        dialog.ShowDialog( );
    }
}
