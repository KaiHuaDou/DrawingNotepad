using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace LightBoard.Tests.Toolbar;

/// <summary>
/// 背景单选与画布底色的联动：选中项必须留在用户点的那一项上，翻页后不回退
/// </summary>
[Collection("Toolbar")]
public class BackgroundSwitchTests
{
    [Fact]
    public void BackgroundRadio_KeepsChoiceAfterPageChange( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            var white = BackgroundRadio(host, "纯白");
            var defaultRadio = BackgroundRadio(host, "默认");

            ClickRadio(host, white);

            Assert.True(white.IsChecked);
            Assert.False(defaultRadio.IsChecked);
            Assert.Same(Brushes.White, host.Canvas.Background);

            App.NewPage( );

            Assert.True(white.IsChecked);
            Assert.False(defaultRadio.IsChecked);
            Assert.Same(Brushes.White, host.Canvas.Background);
        });
    }

    [Fact]
    public void BackgroundRadio_BetweenPages_KeepsLastChoice( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            var white = BackgroundRadio(host, "纯白");
            var black = BackgroundRadio(host, "纯黑");

            ClickRadio(host, white);
            ClickRadio(host, black);

            Assert.True(black.IsChecked);
            Assert.False(white.IsChecked);
            Assert.Same(Brushes.Black, host.Canvas.Background);

            App.NewPage( );

            Assert.True(black.IsChecked);
            Assert.False(white.IsChecked);
            Assert.Same(Brushes.Black, host.Canvas.Background);
        });
    }

    private static RadioButton BackgroundRadio(MainWindowHost host, string content)
    {
        return host.RadiosInGroup("BackgroundGroup").Single(r => (r.Content as string)?.Trim( ) == content);
    }

    // 与真实点击同序：组内互斥取消其余项，再置为选中，最后触发 Click
    private static void ClickRadio(MainWindowHost host, RadioButton radio)
    {
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
}
