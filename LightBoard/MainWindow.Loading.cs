using System;
using System.Windows;

namespace LightBoard;

public partial class MainWindow
{
    private void ShowLoading(string tip)
    {
        LoadingText.Text = tip;
        LoadingBar.IsIndeterminate = true;
        LoadingBorder.Visibility = Visibility.Visible;
    }

    private Progress<ExportProgress> ShowProgress( )
    {
        LoadingBar.IsIndeterminate = false;
        LoadingBorder.Visibility = Visibility.Visible;

        // 进度由后台线程推进：Progress 在 UI 线程构造，回调被投递回 UI 线程
        return new Progress<ExportProgress>(p =>
        {
            LoadingBar.Maximum = p.Total;
            LoadingBar.Value = p.Done;
            LoadingText.Text = $"{p.Tip} {p.Done}/{p.Total}";
        });
    }

    private void HideLoading( )
    {
        LoadingBorder.Visibility = Visibility.Hidden;
    }
}
