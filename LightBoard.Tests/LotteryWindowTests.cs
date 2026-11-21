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
                Assert.Equal("5", countBox.Text);

                var noRepeat = Assert.IsType<CheckBox>(window.FindName("NoRepeatCheck"));
                Assert.False(noRepeat.IsChecked);

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
    public void Close_CommitsTypedCount( )
    {
        UiThread.Run(( ) =>
        {
            var window = new LotteryWindow( );
            window.Show( );
            window.UpdateLayout( );

            var box = Assert.IsType<TextBox>(window.FindName("CountBox"));
            box.SetCurrentValue(TextBox.TextProperty, "7");
            window.Close( );

            Assert.Equal(7, LotteryStore.Load( ).ContinuousCount);
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

                model.Lower = 10;
                model.Upper = 30;

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

                model.Mode = LotteryMode.Collection;
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

                model.Mode = LotteryMode.Collection;
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
    public void NoRepeat_ShowsOptionsAndGraysDrawn( )
    {
        UiThread.Run(( ) =>
        {
            var window = new LotteryWindow( );
            try
            {
                window.Show( );
                window.UpdateLayout( );

                var model = Assert.IsType<LotteryViewModel>(window.DataContext);
                var scroll = Assert.IsType<ScrollViewer>(window.FindName("OptionScroll"));
                var list = Assert.IsType<ItemsControl>(window.FindName("OptionList"));
                var reset = Assert.IsType<Button>(window.FindName("ResetNoRepeatButton"));

                model.Mode = LotteryMode.Collection;
                model.AddDataset("名单", ["甲", "乙", "丙"]);
                model.NoRepeat = true;
                window.Dispatcher.Invoke(( ) => { }, DispatcherPriority.Background);

                Assert.Equal(Visibility.Visible, scroll.Visibility);
                Assert.Equal(Visibility.Visible, reset.Visibility);
                Assert.Equal(3, list.Items.Count);

                model.DrawOne( );
                window.UpdateLayout( );
                window.Dispatcher.Invoke(( ) => { }, DispatcherPriority.Background);

                // 被抽中的选项仍在列表里，只是标记为已抽
                Assert.Single(model.Options, option => option.IsDrawn);

                // 结果区与选项区平分剩余高度
                var host = Assert.IsType<Grid>(window.FindName("ResultHost"));
                Assert.Equal(host.ActualHeight, scroll.ActualHeight, 1);

                reset.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                window.Dispatcher.Invoke(( ) => { }, DispatcherPriority.Background);

                Assert.All(model.Options, option => Assert.False(option.IsDrawn));
                Assert.Empty(model.Results);

                model.Mode = LotteryMode.Continuous;
                window.Dispatcher.Invoke(( ) => { }, DispatcherPriority.Background);

                Assert.Equal(Visibility.Collapsed, scroll.Visibility);
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

                model.Mode = LotteryMode.Collection;
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

                model.Mode = LotteryMode.Collection;
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
                Assert.Equal("1", Assert.Single(model.Options).Text);
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
                var panel = Assert.IsType<Grid>(window.FindName("ResultPanel"));

                Assert.True(model.IsContinuous);
                Assert.Equal(body.ActualWidth, panel.ActualWidth, 1);

                model.Mode = LotteryMode.Collection;
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

    [Fact]
    public void RestoredState_SyncsModeAndSettings( )
    {
        UiThread.Run(( ) =>
        {
            LotteryStore.Save(new LotteryState
            {
                Mode = LotteryMode.Collection,
                Max = 20,
                Lower = 3,
                Upper = 8,
                CollectionCount = 2,
                NoRepeat = true,
                DrawnIndices = [1],
                Selected = "名单",
                Datasets = [new( ) { Name = "名单", Items = ["甲", "乙", "丙"] }],
            });

            var window = new LotteryWindow( );
            try
            {
                window.Show( );
                window.UpdateLayout( );
                window.Dispatcher.Invoke(( ) => { }, DispatcherPriority.Background);

                var model = Assert.IsType<LotteryViewModel>(window.DataContext);
                var continuousRadio = Assert.IsType<RadioButton>(window.FindName("ContinuousRadio"));
                var collectionRadio = Assert.IsType<RadioButton>(window.FindName("CollectionRadio"));
                var collection = Assert.IsType<DockPanel>(window.FindName("CollectionPanel"));
                var range = Assert.IsType<DockPanel>(window.FindName("RangeRow"));

                Assert.True(model.IsCollection);
                Assert.False(continuousRadio.IsChecked);
                Assert.True(collectionRadio.IsChecked);
                Assert.Equal(Visibility.Visible, collection.Visibility);
                Assert.Equal(Visibility.Collapsed, range.Visibility);

                Assert.Equal(20, model.Max);
                Assert.Equal(3, model.Lower);
                Assert.Equal(8, model.Upper);
                Assert.Equal(2, model.Count);
                Assert.Equal("名单", model.SelectedDataset?.Name);

                var noRepeat = Assert.IsType<CheckBox>(window.FindName("NoRepeatCheck"));
                Assert.True(model.NoRepeat);
                Assert.True(noRepeat.IsChecked);
                Assert.True(model.Options[1].IsDrawn);
                Assert.Equal(2, model.PoolSize);
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
