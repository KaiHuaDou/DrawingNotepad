using System.Windows;

namespace InkCanvasNext.Tests;

/// <summary>
/// 视图模型：视口与内容坐标的互换、锚定缩放、平移、夹取，
/// 以及它与载体（LayoutTransform + ScrollViewer offset）实际映射的一致性。
/// </summary>
public class ViewTests
{
    [Theory]
    [InlineData(1.0, 0, 0, 100, 200, 100, 200)]
    [InlineData(2.0, 300, 1000, 100, 200, -100, -600)]
    [InlineData(0.5, 100, 50, 1000, 2000, 400, 950)]
    [InlineData(10.0, 5000, 20000, 700, 1600, 2000, -4000)]
    public void ToViewport_AppliesScaleThenOffset(
        double scale, double offsetX, double offsetY,
        double contentX, double contentY,
        double expectedX, double expectedY)
    {
        var view = new View(scale, offsetX, offsetY);

        var actual = view.ToViewport(new Point(contentX, contentY));

        Assert.Equal(expectedX, actual.X, 12);
        Assert.Equal(expectedY, actual.Y, 12);
    }

    [Theory]
    [InlineData(1.0, 0, 0)]
    [InlineData(2.0, 300, 1000)]
    [InlineData(0.5, 100, 50)]
    [InlineData(0.1, 0, 0)]
    [InlineData(10.0, 5000, 20000)]
    public void ToContent_IsInverseOfToViewport(double scale, double offsetX, double offsetY)
    {
        var view = new View(scale, offsetX, offsetY);

        var point = new Point(1234.5, 6789.25);

        Assert.Equal(point.X, view.ToContent(view.ToViewport(point)).X, 9);
        Assert.Equal(point.Y, view.ToContent(view.ToViewport(point)).Y, 9);
    }

    [Theory]
    [InlineData(1.0, 0, 0, 300, 400, 2.5)]
    [InlineData(2.0, 300, 1000, 640, 360, 0.5)]
    [InlineData(0.5, 100, 50, 10, 20, 10.0)]
    [InlineData(10.0, 5000, 20000, 1200, 700, 0.1)]
    public void ZoomAt_KeepsAnchorContentPointInPlace(
        double scale, double offsetX, double offsetY,
        double anchorX, double anchorY, double targetScale)
    {
        var view = new View(scale, offsetX, offsetY);
        var anchor = new Point(anchorX, anchorY);
        var content = view.ToContent(anchor);

        var zoomed = view.ZoomAt(anchor, targetScale);

        Assert.Equal(targetScale, zoomed.Scale, 12);
        Assert.Equal(anchor.X, zoomed.ToViewport(content).X, 9);
        Assert.Equal(anchor.Y, zoomed.ToViewport(content).Y, 9);
    }

    [Theory]
    [InlineData(1.0, 0, 0, 120, -80)]
    [InlineData(2.0, 300, 1000, -45, 60)]
    [InlineData(0.5, 100, 50, 0, 0)]
    [InlineData(10.0, 5000, 20000, 33.5, 7.25)]
    public void Pan_MovesContentOppositeToOffsetDelta(double scale, double offsetX, double offsetY, double dx, double dy)
    {
        var view = new View(scale, offsetX, offsetY);
        var content = new Point(500, 750);

        var before = view.ToViewport(content);
        var after = view.Pan(new Vector(dx, dy)).ToViewport(content);

        Assert.Equal(before.X - dx, after.X, 9);
        Assert.Equal(before.Y - dy, after.Y, 9);
    }

    [Theory]
    [InlineData(1.0, -100, -200, 0, 0, 0, 0)]
    [InlineData(1.0, 500, 500, 300, 400, 300, 400)]
    [InlineData(2.0, double.MaxValue, double.MaxValue, 14080, 33760, 14080, 33760)]
    [InlineData(1.0, 100, 100, -50, -50, 0, 0)]
    public void Clamp_BoundsOffsetsToZeroAndLimit(
        double scale, double offsetX, double offsetY,
        double maxOffsetX, double maxOffsetY,
        double expectedX, double expectedY)
    {
        var actual = new View(scale, offsetX, offsetY).Clamp(maxOffsetX, maxOffsetY);

        Assert.Equal(expectedX, actual.OffsetX, 12);
        Assert.Equal(expectedY, actual.OffsetY, 12);
    }

    [Fact]
    public void Clamp_KeepsScale( )
    {
        var actual = new View(2.5, 100, 100).Clamp(10, 10);

        Assert.Equal(2.5, actual.Scale, 12);
    }

    /// <summary>
    /// 与迁移前的双指公式等价：旧实现为 offset' = offset · s + panPoint₀ · s − first（s = k'/k），
    /// 即"以基线指位为锚点缩放，再按本帧指位位移平移"。
    /// </summary>
    [Theory]
    [InlineData(1.0, 200, 100, 150, 90, 2.0)]
    [InlineData(4.0, 800, 400, 640, 360, 1.0)]
    [InlineData(0.25, 40, 20, 33, 25, 9.0)]
    public void ZoomAtThenPan_MatchesPinchClosedForm(
        double scale, double offsetX, double offsetY,
        double baselineX, double baselineY,
        double targetScale)
    {
        var view = new View(scale, offsetX, offsetY);
        var baseline = new Point(baselineX, baselineY);
        var current = new Point(baselineX + 37, baselineY - 21);
        var s = targetScale / scale;

        var actual = view.ZoomAt(baseline, targetScale).Pan(baseline - current);

        Assert.Equal(offsetX * s + baselineX * s - current.X, actual.OffsetX, 9);
        Assert.Equal(offsetY * s + baselineY * s - current.Y, actual.OffsetY, 9);
    }

    /// <summary>
    /// 与迁移前的光标缩放公式等价：旧实现为 offset' = offset · k + 光标 · (k − 1)，k = k'/k。
    /// </summary>
    [Theory]
    [InlineData(1.0, 300, 1000, 640, 360, 2.0)]
    [InlineData(2.0, 300, 1000, 640, 360, 0.5)]
    [InlineData(8.0, 6400, 12800, 100, 200, 10.0)]
    public void ZoomAt_MatchesCursorClosedForm(
        double scale, double offsetX, double offsetY,
        double cursorX, double cursorY, double targetScale)
    {
        var view = new View(scale, offsetX, offsetY);
        var k = targetScale / scale;

        var actual = view.ZoomAt(new Point(cursorX, cursorY), targetScale);

        Assert.Equal(offsetX * k + cursorX * (k - 1), actual.OffsetX, 9);
        Assert.Equal(offsetY * k + cursorY * (k - 1), actual.OffsetY, 9);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(10.0)]
    public void ZoomAt_AtExtremeScale_ProducesFiniteOffsets(double scale)
    {
        var view = new View(1.0, 1000, 2000);

        var actual = view
            .ZoomAt(new Point(1280, 800), scale)
            .Pan(new Vector(double.MaxValue / 4, -double.MaxValue / 4))
            .Clamp(double.MaxValue, double.MaxValue);

        Assert.True(double.IsFinite(actual.OffsetX));
        Assert.True(double.IsFinite(actual.OffsetY));
        Assert.Equal(scale, actual.Scale, 12);
    }

    /// <summary>
    /// 模型必须与载体的真实映射一致：载体是 LayoutTransform 承载缩放、ScrollViewer offset 承载平移，
    /// offset 因此以视口 DIP 计。
    /// </summary>
    [Theory]
    [InlineData(1.0, 0, 0)]
    [InlineData(2.0, 300, 1000)]
    [InlineData(0.5, 100, 200)]
    public void ToViewport_MatchesHostMapping(double scale, double offsetX, double offsetY)
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.CurrentScale = scale;
            host.Canvas.OffsetX = offsetX;
            host.Canvas.OffsetY = offsetY;
            host.Canvas.UpdateLayout( );

            var view = new View(host.Canvas.CurrentScale, host.Canvas.OffsetX, host.Canvas.OffsetY);
            var content = new Point(1000, 2000);

            var expected = view.ToViewport(content);
            var actual = host.Target.TranslatePoint(content, host.Canvas);

            Assert.Equal(expected.X, actual.X, 6);
            Assert.Equal(expected.Y, actual.Y, 6);
        });
    }
}
