using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace LightBoard.Tests.Toolbar;

/// <summary>设置窗口：主题里的控件样式全部可解析，内容装在可滚动区域里，每项生成一行</summary>
[Collection("Toolbar")]
public class SettingsWindowTests
{
    [Fact]
    public void Window_LoadsFromTheme( )
    {
        UiThread.Run(( ) =>
        {
            var window = new SettingsWindow( );
            try
            {
                Assert.Equal(SystemParameters.PrimaryScreenHeight * 3 / 4, window.MaxHeight);

                var root = Assert.IsType<DockPanel>(window.Content);
                Assert.Contains(root.Children.OfType<ScrollViewer>( ), scroll => scroll.VerticalScrollBarVisibility == ScrollBarVisibility.Auto);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void CancelButton_ClosesWindow( )
    {
        UiThread.Run(( ) =>
        {
            var window = new SettingsWindow( );
            try
            {
                window.Show( );
                window.UpdateLayout( );

                // 非模态窗口不会再由 IsCancel 自动关闭
                window.CancelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                Assert.False(window.IsVisible);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void Window_BuildsOneRowPerField( )
    {
        UiThread.Run(( ) =>
        {
            var window = new SettingsWindow( );
            try
            {
                var root = (FrameworkElement) window.Content;
                window.Content = null;
                root.DataContext = window.DataContext;

                // 脱离窗口后行模板（隐式 DataTemplate）仍在窗口资源里，合并进来才能实例化
                root.Resources.MergedDictionaries.Add(window.Resources);
                var editor = Assert.IsType<SettingsEditor>(window.DataContext);

                var parameters = new HwndSourceParameters("settings-test") { Width = 780, Height = 620 };
                using var source = new HwndSource(parameters);
                source.RootVisual = root;
                root.UpdateLayout( );

                // 侧栏一次只显示一个分区，逐个切换核对每种条目的控件数量
                foreach (var section in editor.Sections)
                {
                    editor.SelectedSection = section;
                    root.UpdateLayout( );

                    var controls = Descendants(root).ToList( );

                    Assert.Equal(section.Items.OfType<SettingsField>( ).Count( ), controls.OfType<TextBox>( ).Count(box => box.DataContext is SettingsField));
                    Assert.Equal(section.Items.OfType<SettingsText>( ).Count( ), controls.OfType<TextBox>( ).Count(box => box.DataContext is SettingsText));
                    Assert.Equal(section.Items.OfType<SettingsToggle>( ).Count( ), controls.OfType<CheckBox>( ).Count(box => box.DataContext is SettingsToggle));
                    Assert.Equal(section.Items.OfType<SettingsChoice>( ).Count( ) * 2, controls.OfType<RadioButton>( ).Count(box => box.DataContext is SettingsChoice));

                    // 绑定确实把值送到控件上
                    foreach (var box in controls.OfType<TextBox>( ).Where(box => box.DataContext is SettingsField))
                    {
                        Assert.Equal(((SettingsField) box.DataContext).Text, box.Text);
                    }

                    foreach (var checkBox in controls.OfType<CheckBox>( ).Where(box => box.DataContext is SettingsToggle))
                    {
                        Assert.Equal(((SettingsToggle) checkBox.DataContext).Checked, checkBox.IsChecked);
                    }
                }
            }
            finally
            {
                window.Close( );
            }
        });
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;

            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
