using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LightBoard;

public partial class App
{
    /// <summary>
    /// 底色对应的画布底色；null 表示网格，导出与缩略图沿用各自的默认底色
    /// </summary>
    internal static Color? UnderlayColor => Underlay switch
    {
        Underlay.White => Colors.White,
        Underlay.Black => Colors.Black,
        _ => null,
    };
}

public partial class MainWindow
{
    private ResourceDictionary? chromeLight;

    /// <summary>
    /// 底色单选的唯一解码点：画布底衬按这里的结果分派
    /// </summary>
    private Underlay CurrentUnderlay( )
    {
        return WhiteUnderlayRadio.IsChecked == true ? Underlay.White
            : BlackUnderlayRadio.IsChecked == true ? Underlay.Black
            : Underlay.Grid;
    }

    /// <summary>
    /// 外观应用：画布底色、工具栏配色与透明演示的窗口行为
    /// </summary>
    private void ApplyAppearance( )
    {
        var transparent = TransparentMenuItem.IsChecked;
        var underlay = CurrentUnderlay( );
        App.Underlay = underlay;

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

    /// <summary>
    /// 板子的底色写回单选：打开 .lbf 后按记录选中对应项
    /// </summary>
    internal void SyncUnderlay( )
    {
        (App.Underlay switch
        {
            Underlay.White => WhiteUnderlayRadio,
            Underlay.Black => BlackUnderlayRadio,
            _ => DefaultUnderlayRadio,
        }).IsChecked = true;

        ApplyAppearance( );
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

    private static Brush UnderlayBrush(Underlay underlay)
    {
        return underlay switch
        {
            Underlay.White => Brushes.White,
            Underlay.Black => Brushes.Black,
            _ => MajorGridBrush,
        };
    }

    private void UnderlayRadioClick(object o, RoutedEventArgs e)
    {
        if (o is not RadioButton { IsChecked: true })
        {
            return;
        }

        ApplyAppearance( );

        // 缩略图按底色重画
        foreach (var page in App.Pages)
        {
            page.InvalidatePreview( );
        }
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
