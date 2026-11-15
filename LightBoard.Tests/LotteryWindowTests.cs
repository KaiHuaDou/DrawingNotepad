using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

using LightBoard.Tests.Toolbar;

namespace LightBoard.Tests;

/// <summary>抽选窗口：两个模式面板互斥显示，抽选按钮跟随可选集合，结果区空状态随抽选消失</summary>
[Collection("Toolbar")]
public class LotteryWindowTests
{
    public LotteryWindowTests( )
    {
        File.Delete(LotteryStore.FilePath);
    }

    [Fact]
    public void Window_LoadsBindings( )
    {
        UiThread.Run(( ) =>
        {
            var window = new LotteryWindow( );
            try
            {
                window.Show( );
                window.UpdateLayout( );
                window.Dispatcher.Invoke(( ) => { }, DispatcherPriority.Background);

                var range = Assert.IsType<DockPanel>(window.FindName("RangeRow"));
                Assert.Equal(Visibility.Visible, range.Visibility);

                var collection = Assert.IsType<DockPanel>(window.FindName("CollectionPanel"));
                Assert.Equal(Visibility.Collapsed, collection.Visibility);

                var collectionRadio = Assert.IsType<RadioButton>(window.FindName("CollectionRadio"));
                Assert.Equal("集合模式", collectionRadio.Content);

                var maxBox = Assert.IsType<TextBox>(window.FindName("MaxBox"));
                Assert.Equal("51", maxBox.Text);

                var countBox = Assert.IsType<TextBox>(window.FindName("CountBox"));
                Assert.Equal("10", countBox.Text);

                var status = Assert.IsType<TextBlock>(window.FindName("StatusText"));
                Assert.Equal(Visibility.Collapsed, status.Visibility);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void RangeVisuals_FollowValues( )
    {
        UiThread.Run(( ) =>
        {
            var window = new LotteryWindow( );
            try
            {
                window.Show( );
                window.UpdateLayout( );

                var model = Assert.IsType<LotteryViewModel>(window.DataContext);
                var lowerThumb = Assert.IsType<Thumb>(window.FindName("RangeLowerThumb"));
                var upperThumb = Assert.IsType<Thumb>(window.FindName("RangeUpperThumb"));
                var fill = Assert.IsType<Border>(window.FindName("RangeFillBorder"));

                model.SetLower(10);
                model.SetUpper(30);

                var lower = Canvas.GetLeft(lowerThumb);
                var upper = Canvas.GetLeft(upperThumb);

                Assert.Equal(10, lowerThumb.Tag);
                Assert.Equal(30, upperThumb.Tag);
                Assert.True(lower >= 0);
                Assert.True(lower < upper);
                Assert.True(fill.Width > 0);
                Assert.Equal(30, lowerThumb.ActualHeight, 1);

                // 手柄里的数字靠模板内绑定绘制，TemplateBinding 不把整型转成字符串
                Assert.Equal("10", FirstText(lowerThumb)?.Text);
                Assert.Equal("30", FirstText(upperThumb)?.Text);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void ModeSwitch_TogglesPanels( )
    {
        UiThread.Run(( ) =>
        {
            var window = new LotteryWindow( );
            try
            {
                window.Show( );
                window.UpdateLayout( );

                var model = Assert.IsType<LotteryViewModel>(window.DataContext);
                var collectionRadio = Assert.IsType<RadioButton>(window.FindName("CollectionRadio"));
                var range = Assert.IsType<DockPanel>(window.FindName("RangeRow"));
                var collection = Assert.IsType<DockPanel>(window.FindName("CollectionPanel"));

                collectionRadio.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                window.Dispatcher.Invoke(( ) => { }, DispatcherPriority.Background);

                Assert.Equal(LotteryMode.Collection, model.Mode);
                Assert.Equal(Visibility.Collapsed, range.Visibility);
                Assert.Equal(Visibility.Visible, collection.Visibility);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void CollectionWithoutDataset_DisablesDraw( )
    {
        UiThread.Run(( ) =>
        {
            var window = new LotteryWindow( );
            try
            {
                window.Show( );
                window.UpdateLayout( );

                var model = Assert.IsType<LotteryViewModel>(window.DataContext);
                var drawOne = Assert.IsType<Button>(window.FindName("DrawOneButton"));
                var drawMany = Assert.IsType<Button>(window.FindName("DrawManyButton"));

                model.SetMode(LotteryMode.Collection);
                window.Dispatcher.Invoke(( ) => { }, DispatcherPriority.Background);

                Assert.False(drawOne.IsEnabled);
                Assert.False(drawMany.IsEnabled);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void DatasetSelection_EnablesDraw( )
    {
        UiThread.Run(( ) =>
        {
            var window = new LotteryWindow( );
            try
            {
                window.Show( );
                window.UpdateLayout( );

                var model = Assert.IsType<LotteryViewModel>(window.DataContext);
                var drawOne = Assert.IsType<Button>(window.FindName("DrawOneButton"));
                var drawMany = Assert.IsType<Button>(window.FindName("DrawManyButton"));

                model.SetMode(LotteryMode.Collection);
                model.AddDataset("名单", ["甲", "乙"]);
                window.Dispatcher.Invoke(( ) => { }, DispatcherPriority.Background);

                Assert.True(drawOne.IsEnabled);
                Assert.True(drawMany.IsEnabled);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void Results_ShowPlaceholder( )
    {
        UiThread.Run(( ) =>
        {
            var window = new LotteryWindow( );
            try
            {
                window.Show( );
                window.UpdateLayout( );
                window.Dispatcher.Invoke(( ) => { }, DispatcherPriority.Background);

                var model = Assert.IsType<LotteryViewModel>(window.DataContext);
                var list = Assert.IsType<ItemsControl>(window.FindName("ResultList"));
                var placeholder = Assert.IsType<TextBlock>(window.FindName("ResultPlaceholderText"));

                Assert.Equal(Visibility.Visible, placeholder.Visibility);
                Assert.Empty(list.Items);

                model.DrawOne( );
                window.Dispatcher.Invoke(( ) => { }, DispatcherPriority.Background);

                Assert.Equal(Visibility.Collapsed, placeholder.Visibility);
                Assert.Single(list.Items);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void DatasetTextBox_ShowsAndEditsItems( )
    {
        UiThread.Run(( ) =>
        {
            var window = new LotteryWindow( );
            try
            {
                window.Show( );
                window.UpdateLayout( );

                var model = Assert.IsType<LotteryViewModel>(window.DataContext);
                var box = Assert.IsType<TextBox>(window.FindName("DatasetTextBox"));

                Assert.False(box.IsEnabled);

                model.SetMode(LotteryMode.Collection);
                model.AddDataset("名单", ["甲", "乙"]);
                window.Dispatcher.Invoke(( ) => { }, DispatcherPriority.Background);

                Assert.True(box.IsEnabled);
                Assert.Equal("甲，乙", box.Text);

                box.Text = "甲，丙，丁";
                box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource( );
                window.Dispatcher.Invoke(( ) => { }, DispatcherPriority.Background);

                Assert.Equal(3, model.PoolSize);
                Assert.Equal("甲，丙，丁", box.Text);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void DatasetList_ShowsName( )
    {
        UiThread.Run(( ) =>
        {
            var window = new LotteryWindow( );
            try
            {
                window.Show( );
                window.UpdateLayout( );

                var model = Assert.IsType<LotteryViewModel>(window.DataContext);
                var list = Assert.IsType<ListBox>(window.FindName("DatasetList"));

                model.SetMode(LotteryMode.Collection);
                model.AddDataset("名单", ["甲"]);
                window.UpdateLayout( );
                window.Dispatcher.Invoke(( ) => { }, DispatcherPriority.Background);

                var row = Assert.IsType<ListBoxItem>(list.ItemContainerGenerator.ContainerFromIndex(0));
                var text = FirstText(row);

                Assert.NotNull(text);
                Assert.Equal("名单", text.Text);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void DatasetList_SelectionChangesModel( )
    {
        UiThread.Run(( ) =>
        {
            var window = new LotteryWindow( );
            try
            {
                window.Show( );
                window.UpdateLayout( );

                var model = Assert.IsType<LotteryViewModel>(window.DataContext);
                var list = Assert.IsType<ListBox>(window.FindName("DatasetList"));

                model.SetMode(LotteryMode.Collection);
                model.AddDataset("甲", ["1"]);
                model.AddDataset("乙", ["2", "3"]);
                window.UpdateLayout( );
                window.Dispatcher.Invoke(( ) => { }, DispatcherPriority.Background);

                Assert.Equal("乙", model.SelectedDataset?.Name);
                Assert.Equal(1, list.SelectedIndex);

                var first = Assert.IsType<ListBoxItem>(list.ItemContainerGenerator.ContainerFromIndex(0));
                first.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                {
                    RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                });
                window.Dispatcher.Invoke(( ) => { }, DispatcherPriority.Background);

                Assert.Equal("甲", model.SelectedDataset?.Name);
                Assert.Equal(1, model.PoolSize);
                Assert.Equal("1", model.SelectedDatasetText);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void ResultPanel_FillsRowInContinuous( )
    {
        UiThread.Run(( ) =>
        {
            var window = new LotteryWindow( );
            try
            {
                window.Show( );
                window.UpdateLayout( );

                var model = Assert.IsType<LotteryViewModel>(window.DataContext);
                var body = Assert.IsType<Grid>(window.FindName("BodyGrid"));
                var panel = Assert.IsType<DockPanel>(window.FindName("ResultPanel"));

                Assert.True(model.IsContinuous);
                Assert.Equal(body.ActualWidth, panel.ActualWidth, 1);

                model.SetMode(LotteryMode.Collection);
                window.UpdateLayout( );

                Assert.True(panel.ActualWidth < body.ActualWidth);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void Results_AreCentered( )
    {
        UiThread.Run(( ) =>
        {
            var window = new LotteryWindow( );
            try
            {
                window.Show( );
                window.UpdateLayout( );

                var model = Assert.IsType<LotteryViewModel>(window.DataContext);
                var list = Assert.IsType<ItemsControl>(window.FindName("ResultList"));
                var host = Assert.IsType<Grid>(window.FindName("ResultHost"));

                model.DrawOne( );
                window.UpdateLayout( );

                Assert.True(list.ActualWidth < host.ActualWidth);
                Assert.True(list.ActualHeight < host.ActualHeight);

                var center = list.TranslatePoint(new Point(list.ActualWidth / 2, list.ActualHeight / 2), host);

                Assert.Equal(host.ActualWidth / 2, center.X, 1);
                Assert.Equal(host.ActualHeight / 2, center.Y, 1);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    private static TextBlock? FirstText(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);

        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is TextBlock text)
            {
                return text;
            }

            if (FirstText(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
