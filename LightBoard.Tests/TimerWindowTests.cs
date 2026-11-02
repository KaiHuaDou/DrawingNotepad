using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

using LightBoard.Tests.Toolbar;

namespace LightBoard.Tests;

/// <summary>倒计时窗口：主题样式与绑定在构造时全部解析</summary>
[Collection("Toolbar")]
public class TimerWindowTests
{
    [Fact]
    public void Window_LoadsBindings( )
    {
        UiThread.Run(( ) =>
        {
            var window = new TimerWindow( );
            try
            {
                // 首次绑定在 DataBind 优先级处理
                window.Dispatcher.Invoke(( ) => { }, DispatcherPriority.Background);

                Assert.Equal("倒计时", window.Title);

                var timerButton = Assert.IsType<Button>(window.FindName("TimerButton"));
                Assert.False(timerButton.IsEnabled);
                Assert.Equal("\uE768", timerButton.Tag);

                var resetButton = Assert.IsType<Button>(window.FindName("ResetButton"));
                Assert.True(resetButton.IsEnabled);

                var hourUp = Assert.IsType<Button>(window.FindName("HourUpButton"));
                Assert.Equal(Visibility.Visible, hourUp.Visibility);

                var minuteSlider = Assert.IsType<Border>(window.FindName("MinuteSlider"));
                Assert.Equal(Visibility.Visible, minuteSlider.Visibility);

                var minuteBar = Assert.IsType<Grid>(window.FindName("MinuteBar"));
                Assert.Equal(Visibility.Visible, minuteBar.Visibility);

                var fiveTick = Assert.IsType<Button>(window.FindName("MinuteFiveButton"));
                Assert.Equal(Visibility.Visible, fiveTick.Visibility);
                Assert.Equal("5", fiveTick.Content);

                var previousHour = Assert.IsType<Button>(window.FindName("PreviousHourButton"));
                Assert.True(previousHour.IsEnabled);
                Assert.Equal(0, previousHour.Content);
                Assert.Equal("{0:00}", previousHour.ContentStringFormat);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void Bar_HiddenInCountup( )
    {
        UiThread.Run(( ) =>
        {
            var window = new TimerWindow( );
            try
            {
                window.Dispatcher.Invoke(( ) => { }, DispatcherPriority.Background);

                var timer = Assert.IsType<TimerViewModel>(window.DataContext);
                var minuteBar = Assert.IsType<Grid>(window.FindName("MinuteBar"));

                timer.SetMode(TimerMode.Countup);
                window.Dispatcher.Invoke(( ) => { }, DispatcherPriority.Background);

                Assert.Equal(Visibility.Collapsed, minuteBar.Visibility);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void Bar_FillDoesNotWidenContent( )
    {
        UiThread.Run(( ) =>
        {
            var window = new TimerWindow( );
            try
            {
                var content = Assert.IsType<FrameworkElement>(window.Content, exactMatch: false);
                var timer = Assert.IsType<TimerViewModel>(window.DataContext);
                var slider = Assert.IsType<FrameworkElement>(window.FindName("MinuteSlider"), exactMatch: false);
                var fill = Assert.IsType<FrameworkElement>(window.FindName("MinuteFill"), exactMatch: false);
                var natural = NaturalWidth(content);

                // 窗口被撑宽后内容仍应回到自身宽度，填充占满整段也不能反推列宽
                content.Measure(new Size(900, double.PositiveInfinity));
                content.Arrange(new Rect(0, 0, 900, content.DesiredSize.Height));

                timer.SetMinutes(55);
                window.Dispatcher.Invoke(( ) => { }, DispatcherPriority.Background);

                Assert.Equal(natural, NaturalWidth(content), 1);
                Assert.Equal(slider.ActualHeight, fill.ActualHeight);
                Assert.Equal(slider.ActualWidth, fill.Width, 1);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    private static double NaturalWidth(FrameworkElement content)
    {
        content.InvalidateMeasure( );
        content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        return content.DesiredSize.Width;
    }
}
