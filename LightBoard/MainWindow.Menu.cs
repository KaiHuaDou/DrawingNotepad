using System;
using System.Diagnostics;
using System.Windows;

using Ookii.Dialogs.Wpf;

namespace LightBoard;

// 菜单命令：关于、设置、调试层与独立工具窗口
public partial class MainWindow
{
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

    private void DebugLayerClick(object o, RoutedEventArgs e)
    {
        var isChecked = DebugLayerMenu.IsChecked;
        DebugLayer.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;
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
