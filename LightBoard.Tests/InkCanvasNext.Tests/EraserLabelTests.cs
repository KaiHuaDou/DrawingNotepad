using System.Windows;

namespace InkCanvasNext.Tests;

/// <summary>
/// 橡皮反馈圆上的剩余次数：随反馈圆显隐，null 或空串只显示圆
/// </summary>
public class EraserLabelTests
{
    private static readonly Point Start = new(160, 300);

    [Fact]
    public void Label_ShowsWithFeedback( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.EraseArea;
            host.Canvas.EraserLabel = "3";

            var finger = host.Device( );
            finger.Down(host.Target, Start);

            Assert.Equal(Visibility.Visible, host.Canvas.EraserFeedback.Visibility);
            Assert.Equal(Visibility.Visible, host.Canvas.EraserFeedbackLabel.Visibility);
            Assert.Equal("3", host.Canvas.EraserFeedbackLabel.Text);

            finger.Up(host.Target, Start);

            Assert.Equal(Visibility.Collapsed, host.Canvas.EraserFeedback.Visibility);
            Assert.Equal(Visibility.Collapsed, host.Canvas.EraserFeedbackLabel.Visibility);
        });
    }

    [Fact]
    public void NoLabel_FeedbackOnly( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.EraseArea;

            var finger = host.Device( );
            finger.Down(host.Target, Start);

            Assert.Equal(Visibility.Visible, host.Canvas.EraserFeedback.Visibility);
            Assert.Equal(Visibility.Collapsed, host.Canvas.EraserFeedbackLabel.Visibility);

            finger.Up(host.Target, Start);
        });
    }

    [Fact]
    public void LabelCleared_HidesWhileFeedbackStays( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Mode = InkCanvasNextMode.EraseArea;
            host.Canvas.EraserLabel = "2";

            var finger = host.Device( );
            finger.Down(host.Target, Start);
            Assert.Equal(Visibility.Visible, host.Canvas.EraserFeedbackLabel.Visibility);

            host.Canvas.EraserLabel = null;

            Assert.Equal(Visibility.Collapsed, host.Canvas.EraserFeedbackLabel.Visibility);
            Assert.Equal(Visibility.Visible, host.Canvas.EraserFeedback.Visibility);

            finger.Up(host.Target, Start);
        });
    }
}
