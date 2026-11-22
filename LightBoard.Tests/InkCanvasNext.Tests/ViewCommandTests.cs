using System.Windows;

namespace InkCanvasNext.Tests;

/// <summary>视图操作：CurrentView 属性、FitToView、ZoomTo</summary>
public class ViewCommandTests
{
    [Fact]
    public void FitToView_CentersContentRect( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;
            var viewport = canvas.ViewportSize;

            canvas.FitToView(new Rect(100, 100, 400, 200));
            canvas.UpdateLayout( );

            var expectedScale = Math.Min(viewport.Width / 400, viewport.Height / 200);
            Assert.Equal(expectedScale, canvas.CurrentScale, 6);

            // 左上贴边时自动补足一屏余量会平移内容，视觉中心仍落在内容矩形中心
            var center = canvas.CurrentView.ToContent(new Point(viewport.Width / 2, viewport.Height / 2));
            Assert.Equal(300 + host.ContentShift.X, center.X, 6);
            Assert.Equal(200 + host.ContentShift.Y, center.Y, 6);
        });
    }

    [Fact]
    public void FitToView_AppliesMargin( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;
            var viewport = canvas.ViewportSize;
            const double margin = 100;

            canvas.FitToView(new Rect(1000, 1000, 400, 200), margin);
            canvas.UpdateLayout( );

            var expectedScale = Math.Min(
                (viewport.Width - 2 * margin) / 400,
                (viewport.Height - 2 * margin) / 200);
            Assert.Equal(expectedScale, canvas.CurrentScale, 6);
            Assert.Equal(1200 * expectedScale - viewport.Width / 2, canvas.OffsetX, 6);
            Assert.Equal(1100 * expectedScale - viewport.Height / 2, canvas.OffsetY, 6);
        });
    }

    [Fact]
    public void FitToView_EmptyRect_DoesNothing( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;
            var scale = canvas.CurrentScale;
            var x = canvas.OffsetX;
            var y = canvas.OffsetY;

            canvas.FitToView(Rect.Empty);

            Assert.Equal(scale, canvas.CurrentScale);
            Assert.Equal(x, canvas.OffsetX);
            Assert.Equal(y, canvas.OffsetY);
        });
    }

    [Fact]
    public void ZoomTo_KeepsViewportCenterContent( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;
            var viewport = canvas.ViewportSize;
            var center = new Point(viewport.Width / 2, viewport.Height / 2);
            var before = new Point(
                (center.X + canvas.OffsetX) / canvas.CurrentScale,
                (center.Y + canvas.OffsetY) / canvas.CurrentScale);

            canvas.ZoomTo(2);
            canvas.UpdateLayout( );

            var after = new Point(
                (center.X + canvas.OffsetX) / canvas.CurrentScale,
                (center.Y + canvas.OffsetY) / canvas.CurrentScale);

            Assert.Equal(2, canvas.CurrentScale, 6);
            Assert.Equal(before.X, after.X, 4);
            Assert.Equal(before.Y, after.Y, 4);
        });
    }

    [Fact]
    public void CurrentView_NegativeOffsets_ExtendCanvasAtOrigin( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;
            var viewport = canvas.ViewportSize;
            var before = canvas.CanvasSize;

            canvas.CurrentView = new View(1, -50, -50);
            canvas.UpdateLayout( );

            // 负偏移先夹到 0，再在左/上补足一屏余量
            Assert.Equal(viewport.Width, canvas.OffsetX, 6);
            Assert.Equal(viewport.Height, canvas.OffsetY, 6);
            Assert.Equal(1, canvas.CurrentScale, 6);
            Assert.Equal(before.Width + viewport.Width, canvas.CanvasSize.Width, 6);
            Assert.Equal(before.Height + viewport.Height, canvas.CanvasSize.Height, 6);
        });
    }

    [Fact]
    public void CurrentView_IgnoresNonFiniteValues( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;
            var scale = canvas.CurrentScale;
            var x = canvas.OffsetX;

            canvas.CurrentView = new View(double.NaN, double.NaN, double.NaN);

            Assert.Equal(scale, canvas.CurrentScale);
            Assert.Equal(x, canvas.OffsetX);
        });
    }
}
