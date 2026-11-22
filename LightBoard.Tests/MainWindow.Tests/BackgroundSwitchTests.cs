using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace LightBoard.Tests.Toolbar;

/// <summary>
/// 底色单选与透明演示的联动：选中项必须留在用户点的那一项上，翻页后不回退
/// </summary>
[Collection("Toolbar")]
public class BackgroundSwitchTests
{
    [Fact]
    public void UnderlayRadio_KeepsChoiceAfterPageChange( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            var white = UnderlayRadio(host, "纯白");
            var grid = UnderlayRadio(host, "网格");

            ClickRadio(host, white);

            Assert.True(white.IsChecked);
            Assert.False(grid.IsChecked);
            Assert.Same(Brushes.White, host.Canvas.Background);

            BoardState.NewPage( );

            Assert.True(white.IsChecked);
            Assert.False(grid.IsChecked);
            Assert.Same(Brushes.White, host.Canvas.Background);
        });
    }

    [Fact]
    public void UnderlayRadio_BetweenPages_KeepsLastChoice( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            var white = UnderlayRadio(host, "纯白");
            var black = UnderlayRadio(host, "纯黑");

            ClickRadio(host, white);
            ClickRadio(host, black);

            Assert.True(black.IsChecked);
            Assert.False(white.IsChecked);
            Assert.Same(Brushes.Black, host.Canvas.Background);

            BoardState.NewPage( );

            Assert.True(black.IsChecked);
            Assert.False(white.IsChecked);
            Assert.Same(Brushes.Black, host.Canvas.Background);
        });
    }

    [Fact]
    public void Transparent_EnablesPassThrough_GridRestores( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );

            ClickMenuItem(host, host.Window.TransparentMenuItem, true);

            Assert.Same(Brushes.Transparent, host.Canvas.Background);
            Assert.True(host.Window.Topmost);
            Assert.Equal(Visibility.Visible, host.Window.PassThroughBorder.Visibility);

            ClickMenuItem(host, host.Window.TransparentMenuItem, false);

            Assert.False(host.Window.Topmost);
            Assert.Equal(Visibility.Collapsed, host.Window.PassThroughBorder.Visibility);
            Assert.Same(host.Window.FindResource("MajorGridBrush"), host.Canvas.Background);
        });
    }

    [Fact]
    public void Transparent_DisablesUnderlayRadios( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            var radios = host.RadiosInGroup("UnderlayGroup").ToArray( );

            ClickMenuItem(host, host.Window.TransparentMenuItem, true);

            Assert.All(radios, r => Assert.False(r.IsEnabled));

            ClickMenuItem(host, host.Window.TransparentMenuItem, false);

            Assert.All(radios, r => Assert.True(r.IsEnabled));
        });
    }

    [Fact]
    public void UnderlayRadio_WhileTransparent_KeepsCanvasTransparent( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            var white = UnderlayRadio(host, "纯白");

            ClickMenuItem(host, host.Window.TransparentMenuItem, true);

            // 透明演示时底色单选已置灰，直接触发处理器时画布仍保持透明
            white.IsChecked = true;
            white.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, white));

            Assert.Same(Brushes.Transparent, host.Canvas.Background);
        });
    }

    [Fact]
    public void TransparentMenuItem_TracksWindowState( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );

            Assert.False(host.Window.TransparentMenuItem.IsChecked);

            ClickMenuItem(host, host.Window.TransparentMenuItem, true);

            Assert.True(host.Window.TransparentMenuItem.IsChecked);
            Assert.True(host.Window.Topmost);
        });
    }

    [Fact]
    public void VideoMenuItem_StartsUnchecked( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );

            Assert.False(host.Window.VideoMenuItem.IsChecked);
        });
    }

    private static RadioButton UnderlayRadio(MainWindowHost host, string content)
    {
        return host.RadiosInGroup("UnderlayGroup").Single(r => (r.Content as string)?.Trim( ) == content);
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

    // 勾选项的 Click 在 IsChecked 改好之后才触发，顺序与真实点击一致
    private static void ClickMenuItem(MainWindowHost host, MenuItem item, bool isChecked)
    {
        item.IsChecked = isChecked;
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));
    }
}
