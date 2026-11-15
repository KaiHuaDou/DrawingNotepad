using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using Microsoft.Win32;

namespace LightBoard;

public partial class LotteryWindow : Window
{
    private const string TextFilter = "文本文件 (*.txt;*.csv)|*.txt;*.csv|所有文件 (*.*)|*.*";

    private readonly LotteryViewModel viewModel;

    public LotteryWindow( )
    {
        InitializeComponent( );

        MaxWidth = SystemParameters.WorkArea.Width - 40;
        MaxHeight = SystemParameters.WorkArea.Height - 40;
        Width = Math.Min(Width, MaxWidth);
        Height = Math.Min(Height, MaxHeight);

        viewModel = (LotteryViewModel) DataContext;
        viewModel.Restore(LotteryStore.Load( ));
        viewModel.PropertyChanged += ViewModelPropertyChanged;
    }

    protected override void OnClosed(EventArgs e)
    {
        LotteryStore.Save(viewModel.CreateState( ));

        viewModel.PropertyChanged -= ViewModelPropertyChanged;

        base.OnClosed(e);
    }

    private void ModeClick(object o, RoutedEventArgs e)
    {
        if (o is not RadioButton radio)
        {
            return;
        }

        viewModel.SetMode(ReferenceEquals(radio, CollectionRadio) ? LotteryMode.Collection : LotteryMode.Continuous);
    }

    private void MaxStepClick(object o, RoutedEventArgs e)
    {
        if (o is not Button button)
        {
            return;
        }

        viewModel.StepMax(ReferenceEquals(button, MaxUpButton) ? 1 : -1);
    }

    private void CountStepClick(object o, RoutedEventArgs e)
    {
        if (o is not Button button)
        {
            return;
        }

        viewModel.StepCount(ReferenceEquals(button, CountUpButton) ? 1 : -1);
    }

    private void NumberBoxKeyDown(object o, KeyEventArgs e)
    {
        if (o is not TextBox box || e.Key != Key.Enter)
        {
            return;
        }

        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource( );
        box.SelectAll( );
        e.Handled = true;
    }

    private void NumberBoxLostFocus(object o, RoutedEventArgs e)
    {
        if (o is not TextBox box)
        {
            return;
        }

        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource( );
    }

    private void ImportDatasetClick(object o, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new( ) { Filter = TextFilter, Multiselect = true };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var imported = 0;
        var total = 0;
        var notes = new List<string>( );
        Exception? firstError = null;

        foreach (var path in dialog.FileNames)
        {
            var fileName = Path.GetFileName(path);

            try
            {
                var items = LotteryViewModel.SplitItems(File.ReadAllText(path));
                if (items.Length == 0)
                {
                    notes.Add($"{fileName} 没有元素");
                    continue;
                }

                viewModel.AddDataset(Path.GetFileNameWithoutExtension(path), items);
                imported++;
                total += items.Length;
            }
            catch (Exception ex)
            {
                App.LogException(ex);
                firstError ??= ex;
                notes.Add($"无法读取 {fileName}");
            }
        }

        viewModel.Status = JoinStatus($"已导入 {imported} 个数据集，共 {total} 项", notes);

        if (firstError is not null)
        {
            App.ShowException(firstError, "部分文件无法读取");
        }
    }

    private void RemoveDatasetClick(object o, RoutedEventArgs e)
    {
        viewModel.RemoveSelectedDataset( );
    }

    private void DrawOneClick(object o, RoutedEventArgs e)
    {
        viewModel.DrawOne( );
    }

    private void DrawManyClick(object o, RoutedEventArgs e)
    {
        viewModel.DrawMany( );
    }

    private void WindowPreviewKeyDown(object o, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        Close( );
        e.Handled = true;
    }

    private void ViewModelPropertyChanged(object? o, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LotteryViewModel.Lower) or nameof(LotteryViewModel.Upper) or nameof(LotteryViewModel.Max))
        {
            UpdateRangeVisual( );
        }
    }

    private static string JoinStatus(string summary, List<string> notes)
    {
        return notes.Count == 0
            ? summary
            : string.Join(Environment.NewLine, [summary, .. notes]);
    }
}
