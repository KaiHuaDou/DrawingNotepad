using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LightBoard;

public partial class MainWindow
{
    private ResourceDictionary? chromeLight;

    /// <summary>
    /// 外观应用：画布底色、工具栏配色与透明演示的窗口行为
    /// </summary>
    private void ApplyAppearance( )
    {
        var transparent = TransparentMenuItem.IsChecked;
        var underlay = CurrentUnderlay( );
        BoardState.Underlay = underlay;

        // 透明演示时工具栏压在任意桌面上，图标保持浅色
        ApplyChromePalette(underlay == Underlay.White && !transparent);

        CanvasNext.Background = transparent ? Brushes.Transparent : UnderlayBrush(underlay);
        Topmost = transparent;
        PassThroughBorder.Visibility = transparent ? Visibility.Visible : Visibility.Collapsed;

        // 透明演示时画布不画底衬，底色单选没有意义
        DefaultUnderlayRadio.IsEnabled = !transparent;
        WhiteUnderlayRadio.IsEnabled = !transparent;
        BlackUnderlayRadio.IsEnabled = !transparent;

        SyncChrome( );
    }

    /// <summary>
    /// 工具栏配色：纯白底色下换上浅色字典，其余情况移除它，取 Theme.xaml 的深色值
    /// </summary>
    private void ApplyChromePalette(bool light)
    {
        if (light)
        {
            chromeLight ??= new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/LightBoard;component/ChromeLight.xaml")
            };
            if (!Resources.MergedDictionaries.Contains(chromeLight))
            {
                Resources.MergedDictionaries.Add(chromeLight);
            }
        }
        else if (chromeLight is not null)
        {
            Resources.MergedDictionaries.Remove(chromeLight);
        }
    }

    // 窗口 chrome 的唯一写入口：计时、切换按钮与三块边框的显隐与底色
    private void SyncChrome( )
    {
        var transparent = TransparentMenuItem.IsChecked;
        var panel = AllPageToggle.IsChecked == true;

        TimeText.Visibility = transparent || panel ? Visibility.Collapsed : Visibility.Visible;
        SwitchOutButton.Visibility = transparent ? Visibility.Collapsed : Visibility.Visible;

        // 底色取资源引用而不是画刷实例，配色字典换下来时三块边框跟着变
        var surface = transparent ? "ContainerBrushSolid" : "ContainerBrush";
        LeftBorder.SetResourceReference(Border.BackgroundProperty, surface);
        CenterBorder.SetResourceReference(Border.BackgroundProperty, surface);
        RightBorder.SetResourceReference(Border.BackgroundProperty, transparent || panel ? "ContainerBrushSolid" : "ContainerBrush");
    }

    private void TransparentMenuItemClick(object o, RoutedEventArgs e)
    {
        if (o is not MenuItem { IsChecked: bool isChecked })
        {
            return;
        }

        ApplyAppearance( );
    }
}
