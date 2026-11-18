using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace LightBoard.Tests.Toolbar;

/// <summary>
/// 工具栏配色随底色：纯白底色下图标与文字换近黑，其余情况取深色值
/// </summary>
[Collection("Toolbar")]
public class ChromePaletteTests
{
    private static readonly Color DarkForeground = ColorOf("#E3E3E3");
    private static readonly Color LightForeground = ColorOf("#1F1F1F");
    private static readonly Color LightText = ColorOf("#CC595959");
    private static readonly Color LightHover = ColorOf("#12000000");
    private static readonly Color LightSurface = ColorOf("#E6F2F2F2");
    private static readonly Color LightPopupSurface = ColorOf("#FFFFFF");

    [Fact]
    public void WhiteUnderlay_UsesLightChrome( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );

            ClickUnderlay(host, "纯白");
            host.Window.UpdateLayout( );

            Assert.Equal(LightForeground, ForegroundOf(host));
            Assert.Equal(LightText, ColorOf(host.Window.TimeText.Foreground));
            Assert.Equal(LightHover, ColorOf((Brush) host.Window.FindResource("ChromeHoverBrush")));
            Assert.Equal(LightSurface, ColorOf(host.Window.LeftBorder.Background));
            Assert.Equal(1, host.Window.LeftBorder.BorderThickness.Left);
            Assert.Contains(host.Window.Resources.MergedDictionaries, d => d.Contains("ChromeForegroundBrush"));
        });
    }

    [Fact]
    public void WhiteUnderlay_WhileTransparent_KeepsDarkChrome( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );

            ClickUnderlay(host, "纯白");
            ClickMenuItem(host.Window.TransparentMenuItem, true);
            host.Window.UpdateLayout( );

            Assert.Equal(DarkForeground, ForegroundOf(host));
        });
    }

    [Fact]
    public void WhiteUnderlay_AppliesToPopup( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );

            ClickUnderlay(host, "纯白");
            host.Window.UpdateLayout( );

            // 弹窗在窗口的逻辑树里，配色字典同样覆盖它
            var popup = (Border) host.Window.BackgroundPopup.Child;
            Assert.Equal(LightPopupSurface, ColorOf(popup.Background));
            Assert.Equal(LightForeground, ColorOf((Brush) popup.FindResource("ChromeForegroundBrush")));
        });
    }

    private static Color ForegroundOf(MainWindowHost host)
    {
        return ColorOf(host.Window.EraseAreaRadio.Foreground);
    }

    private static Color ColorOf(Brush brush)
    {
        return ((SolidColorBrush) brush).Color;
    }

    private static Color ColorOf(string hex)
    {
        return (Color) ColorConverter.ConvertFromString(hex);
    }

    // 与真实点击同序：组内互斥取消其余项，再置为选中，最后触发 Click
    private static void ClickUnderlay(MainWindowHost host, string content)
    {
        var radio = host.RadiosInGroup("UnderlayGroup").Single(r => (r.Content as string)?.Trim( ) == content);
        foreach (var sibling in host.RadiosInGroup(radio.GroupName))
        {
            if (!ReferenceEquals(sibling, radio))
            {
                sibling.IsChecked = false;
            }
        }

        radio.IsChecked = true;
        radio.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, radio));
    }

    private static void ClickMenuItem(MenuItem item, bool isChecked)
    {
        item.IsChecked = isChecked;
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));
    }
}
