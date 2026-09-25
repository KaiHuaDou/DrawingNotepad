using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace LightBoard.Tests.Toolbar;

// RightBorder 面板开合的布局契约:终态全部由内容实测,动画只做过渡,写回后恢复自适应。
// 断言依赖真实布局与动画时钟,窗口必须实际显示(未显示的 Window 上 UpdateLayout 不完整、动画时钟不推进)。
[Collection("Toolbar")]
public class RightBorderTests
{
    [Fact]
    public void Expand_ConstrainedViewport_ColumnWidthMatchesContent( )
    {
        UiThread.Run(( ) =>
        {
            using var host = CreateVisibleHost(out var window);
            var border = window.RightBorder;

            // 复现 AllPageToogleClick 的测量序列:锁宽补布局后,列宽应是内容需求而非被视口压缩
            var from = border.ActualWidth;
            border.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(from, from, TimeSpan.FromSeconds(0.2)));
            window.PagePreviewsBox.Visibility = Visibility.Visible;
            border.UpdateLayout( );

            var column = ((GridView) window.PagePreviewsBox.View).Columns[0];

            // 视口约 212,列内容(页码 + 225 宽缩略图)必须完整报出需求
            Assert.True(column.ActualWidth >= 226, $"列宽 {column.ActualWidth:0.#} 被视口压缩,未报出内容需求");
        });
    }

    [Fact]
    public void Toggle_ExpandThenCollapse_RestoresAutoSize( )
    {
        UiThread.Run(( ) =>
        {
            using var host = CreateVisibleHost(out var window);
            var border = window.RightBorder;
            var chrome = border.Padding.Left * 2 + border.BorderThickness.Left * 2;
            var initial = border.ActualWidth;

            Toggle(window, true);
            PumpUntil(
                ( ) => Math.Abs(border.ActualWidth - ExpandedWidth(window)) < 0.5,
                2000,
                "展开终态宽",
                ( ) => $"ActualWidth={border.ActualWidth:0.#} Width={border.Width} Height={border.Height:0.#} "
                    + $"列宽={((GridView) window.PagePreviewsBox.View).Columns[0].ActualWidth:0.#} "
                    + $"按钮行={window.PageButtonsRow.ActualWidth:0.#}");

            Assert.Equal(ExpandedWidth(window), border.ActualWidth, 0.5);
            Assert.True(double.IsNaN(border.Width), $"展开写回后 Width 应为 Auto,实际 {border.Width}");
            Assert.Equal(window.ActualHeight - border.Margin.Bottom, border.ActualHeight, 0.5);
            Assert.Equal(window.ActualHeight - border.Margin.Bottom, border.Height, 0.5);

            Toggle(window, false);
            PumpUntil(( ) => Math.Abs(border.ActualWidth - initial) < 0.5, 2000, "收起终态宽");

            Assert.Equal(initial, border.ActualWidth, 0.5);
            Assert.True(double.IsNaN(border.Width), $"收起写回后 Width 应为 Auto,实际 {border.Width}");
            Assert.Equal(window.PageButtonsRow.ActualHeight + chrome, border.ActualHeight, 0.5);
            Assert.True(double.IsNaN(border.Height), $"收起写回后 Height 应为 Auto,实际 {border.Height}");
        });
    }

    [Fact]
    public void RapidToggles_FinalStateMatchesDesign( )
    {
        UiThread.Run(( ) =>
        {
            using var host = CreateVisibleHost(out var window);
            var border = window.RightBorder;
            var chrome = border.Padding.Left * 2 + border.BorderThickness.Left * 2;

            for (var i = 0; i < 10; i++)
            {
                Toggle(window, i % 2 == 0);
            }

            Toggle(window, true);
            PumpUntil(( ) => Math.Abs(border.ActualWidth - ExpandedWidth(window)) < 0.5, 2000, "连点后展开终态宽");
            Assert.Equal(ExpandedWidth(window), border.ActualWidth, 0.5);

            Toggle(window, false);
            PumpUntil(( ) => Math.Abs(border.ActualWidth - (window.PageButtonsRow.ActualWidth + chrome)) < 0.5, 2000, "连点后收起终态宽");
            Assert.Equal(window.PageButtonsRow.ActualWidth + chrome, border.ActualWidth, 0.5);
        });
    }

    [Fact]
    public void CollapsedWidth_FollowsPageNumberText( )
    {
        UiThread.Run(( ) =>
        {
            using var host = CreateVisibleHost(out var window);
            var border = window.RightBorder;
            var chrome = border.Padding.Left * 2 + border.BorderThickness.Left * 2;
            var initial = border.ActualWidth;

            Toggle(window, true);
            PumpUntil(
                ( ) => Math.Abs(border.ActualWidth - ExpandedWidth(window)) < 0.5,
                2000,
                "展开终态宽",
                ( ) => $"ActualWidth={border.ActualWidth:0.#} 期望={ExpandedWidth(window):0.#} Width={border.Width}");
            Toggle(window, false);
            PumpUntil(( ) => Math.Abs(border.ActualWidth - initial) < 0.5, 2000, "收起终态宽");

            (window.AllPageToogle.Content as TextBlock)!.Text = "10/12";
            border.UpdateLayout( );

            Assert.True(
                border.ActualWidth > initial + 5,
                $"页码变宽后收起宽度应自适应,实际 {border.ActualWidth:0.#},初始 {initial:0.#}");
            Assert.Equal(window.PageButtonsRow.ActualWidth + chrome, border.ActualWidth, 0.5);
        });
    }

    [Fact]
    public void Collapse_KeepsCollapseAndPrevButtonsAndRestores( )
    {
        UiThread.Run(( ) =>
        {
            using var host = CreateVisibleHost(out var window);
            var border = window.RightBorder;
            var collapse = window.CollapseExpandButton;
            var prev = window.PrevPageButton;
            var kept = border.Padding.Left + border.BorderThickness.Left
                + collapse.Margin.Left + collapse.ActualWidth
                + prev.Margin.Left + prev.ActualWidth
                + border.Padding.Right + border.BorderThickness.Right;

            collapse.IsChecked = true;
            collapse.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            var expectedShift = border.ActualWidth - kept;
            PumpUntil(( ) => Math.Abs(window.RightTransform.X - expectedShift) < 0.5, 2000, "折叠位移");

            Assert.Equal(expectedShift, window.RightTransform.X, 0.5);

            collapse.IsChecked = false;
            collapse.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            PumpUntil(( ) => Math.Abs(window.RightTransform.X) < 0.5, 2000, "展开归位");

            Assert.Equal(0, window.RightTransform.X, 0.5);
        });
    }

    [Fact]
    public void Fold_WhileExpanded_CollapsesPreviewToo( )
    {
        UiThread.Run(( ) =>
        {
            using var host = CreateVisibleHost(out var window);
            var border = window.RightBorder;
            var collapse = window.CollapseExpandButton;
            var prev = window.PrevPageButton;
            var chrome = border.Padding.Left * 2 + border.BorderThickness.Left * 2;
            var kept = border.Padding.Left + border.BorderThickness.Left
                + collapse.Margin.Left + collapse.ActualWidth
                + prev.Margin.Left + prev.ActualWidth
                + border.Padding.Right + border.BorderThickness.Right;
            var collapsedWidth = window.PageButtonsRow.ActualWidth + chrome;

            Toggle(window, true);
            PumpUntil(( ) => Math.Abs(border.ActualWidth - ExpandedWidth(window)) < 0.5, 2000, "展开终态宽");

            collapse.IsChecked = true;
            collapse.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            PumpUntil(( ) => Math.Abs(window.RightTransform.X - (collapsedWidth - kept)) < 0.5, 2000, "折叠位移");

            Assert.Equal(Visibility.Collapsed, window.PagePreviewsBox.Visibility);
            Assert.False(window.AllPageToogle.IsChecked);
            Assert.Equal(collapsedWidth, border.ActualWidth, 0.5);
            Assert.True(double.IsNaN(border.Width), $"折叠收起后 Width 应为 Auto,实际 {border.Width}");

            collapse.IsChecked = false;
            collapse.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            PumpUntil(( ) => Math.Abs(window.RightTransform.X) < 0.5, 2000, "展开归位");

            Assert.Equal(collapsedWidth, border.ActualWidth, 0.5);
            Assert.Equal(Visibility.Collapsed, window.PagePreviewsBox.Visibility);
        });
    }

    private static MainWindowHost CreateVisibleHost(out MainWindow window)
    {
        var host = new MainWindowHost( );
        window = host.Window;
        // Maximized 与 ShowActivated=false 不能组合 Show;用声明尺寸 1920×1080 布局,断言全用动态 Actual 值
        window.WindowState = WindowState.Normal;
        window.ShowActivated = false;
        window.Show( );
        window.UpdateLayout( );
        return host;
    }

    private static void Toggle(MainWindow window, bool expand)
    {
        window.AllPageToogle.IsChecked = expand;
        window.AllPageToogle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    }

    private static double ExpandedWidth(MainWindow window)
    {
        var border = window.RightBorder;
        var chrome = border.Padding.Left * 2 + border.BorderThickness.Left * 2;
        var box = window.PagePreviewsBox;
        box.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return Math.Max(window.PageButtonsRow.ActualWidth, box.DesiredSize.Width) + chrome;
    }

    private static void PumpUntil(Func<bool> condition, int timeoutMs, string what, Func<string>? diagnose = null)
    {
        var watch = Stopwatch.StartNew( );
        while (!condition( ) && watch.ElapsedMilliseconds < timeoutMs)
        {
            var frame = new DispatcherFrame( );
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(( ) => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }

        Assert.True(condition( ), $"等待 {what} 超时({timeoutMs} ms){(diagnose is null ? "" : $",现场:{diagnose( )}")}");

        // 条件满足后再泵一个动画时长,让 Completed 写回与布局跑完再断言
        var stable = new DispatcherFrame( );
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Background,
            (_, _) => stable.Continue = false, Dispatcher.CurrentDispatcher);
        Dispatcher.PushFrame(stable);
        timer.Stop( );
    }
}
