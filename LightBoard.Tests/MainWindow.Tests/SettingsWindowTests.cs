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
    public void Window_BuildsOneRowPerField( )
    {
        UiThread.Run(( ) =>
        {
            var window = new SettingsWindow( );
            try
            {
                var root = (FrameworkElement)window.Content;
                window.Content = null;
                root.DataContext = window.DataContext;

                var parameters = new HwndSourceParameters("settings-test") { Width = 520, Height = 1000 };
                using var source = new HwndSource(parameters);
                source.RootVisual = root;
                root.UpdateLayout( );

                var boxes = Descendants(root).OfType<TextBox>( ).ToList( );
                var fieldBoxes = boxes.Where(box => box.DataContext is SettingsField).ToList( );

                Assert.Equal(13, fieldBoxes.Count);
                Assert.Contains(boxes, box => box.DataContext is SettingsEditor);
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
