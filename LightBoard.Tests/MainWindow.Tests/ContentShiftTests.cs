using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;

namespace LightBoard.Tests.Toolbar;

/// <summary>画布向左/上扩展时宿主同步页背景盒与页视图</summary>
[Collection("Toolbar")]
public class ContentShiftTests
{
    [Fact]
    public void ContentShift_MovesPageBackgroundAndViewOffset( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            var page = BoardState.CurrentPage;
            page.Background = new DocumentBackground(@"C:\a.png", 1, new Rect(100, 100, 200, 200));
            host.Window.OnPageChanged(null, EventArgs.Empty);

            var boxBefore = page.Background!.Box;
            var offsetX = page.OffsetX;
            var offsetY = page.OffsetY;

            var shift = default(Vector);
            host.Canvas.ContentShifted += (_, e) => shift += e.Offset;
            host.Canvas.Strokes.Add(new Stroke(
            [
                new StylusPoint(-300, -200),
                new StylusPoint(-100, -50),
            ]));

            Assert.True(shift.X > 0);
            Assert.True(shift.Y > 0);
            Assert.Equal(boxBefore.X + shift.X, page.Background!.Box.X, 6);
            Assert.Equal(boxBefore.Y + shift.Y, page.Background!.Box.Y, 6);
            Assert.Equal(offsetX + shift.X * page.Scale, page.OffsetX, 6);
            Assert.Equal(offsetY + shift.Y * page.Scale, page.OffsetY, 6);
        });
    }
}
