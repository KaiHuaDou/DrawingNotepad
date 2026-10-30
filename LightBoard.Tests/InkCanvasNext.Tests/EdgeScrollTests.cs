using System.Windows;

namespace InkCanvasNext.Tests;

/// <summary>
/// 边缘热区的几何：热区矩形按缩放折算、包围盒的严格相交，以及清空热区所需的最小位移。
/// </summary>
public class EdgeScrollTests
{
    private const double Threshold = 100;
    private const double ViewportWidth = 800;
    private const double ViewportHeight = 600;

    private static readonly Size Viewport = new(ViewportWidth, ViewportHeight);

    [Theory]
    [InlineData((int) EdgeScrollSide.Top, 200, 100, 400, 50)]
    [InlineData((int) EdgeScrollSide.Bottom, 200, 350, 400, 50)]
    [InlineData((int) EdgeScrollSide.Left, 200, 100, 50, 300)]
    [InlineData((int) EdgeScrollSide.Right, 550, 100, 50, 300)]
    public void EdgeBandContentRect_DividesDepthByScale(
        int side, double left, double top, double width, double height)
    {
        // 偏移 400/200、缩放 2：内容窗口为 200,100 起 400x300，热区深度 100 视口 DIP 折算为 50 内容 DIP
        var band = InkCanvasNext.EdgeBandContentRect(
            new View(2.0, 400, 200), Viewport, Threshold, (EdgeScrollSide) side);

        Assert.Equal(new Rect(left, top, width, height), band);
    }

    [Theory]
    [InlineData(0, 0, 1, 100)]
    [InlineData(800, 600, 1, 0)]
    [InlineData(800, 600, 0, 100)]
    public void EdgeBandContentRect_InvalidSizeOrThreshold_ReturnsEmpty(double width, double height, double scale, double threshold)
    {
        var band = InkCanvasNext.EdgeBandContentRect(
            new View(scale, 0, 0), new Size(width, height), threshold, EdgeScrollSide.Bottom);

        Assert.True(band.IsEmpty);
    }

    [Theory]
    [InlineData(50, 50, true)]
    [InlineData(90, 90, true)]
    [InlineData(-20, -20, false)]
    [InlineData(100, 0, false)]   // 边界相接
    [InlineData(0, 100, false)]
    public void BandHasInk_RequiresOverlapOnBothAxes(double left, double top, bool expected)
    {
        var band = new Rect(0, 0, 100, 100);

        Assert.Equal(expected, InkCanvasNext.BandHasInk([new Rect(left, top, 10, 10)], band));
    }

    [Fact]
    public void BandClearDelta_Bottom_SkipsWholeInkLayers( )
    {
        var band = new Rect(0, 500, 200, Threshold);
        List<Rect> bounds =
        [
            new(0, 520, 50, 20),   // 520..540
            new(0, 560, 50, 20),   // 560..580，第一段位移后仍与热区相交
        ];

        // 跳到 540 后热区为 540..640，与第二段相交，再跳到 580
        Assert.Equal(80, InkCanvasNext.BandClearDelta(bounds, band, EdgeScrollSide.Bottom));
    }

    [Fact]
    public void BandClearDelta_Top_MirrorsBottom( )
    {
        var band = new Rect(0, 500, 200, Threshold);
        List<Rect> bounds = [new(0, 560, 50, 20)];

        // 向上跳 40：热区变为 460..560，与笔画的上边相接，视为脱开
        Assert.Equal(40, InkCanvasNext.BandClearDelta(bounds, band, EdgeScrollSide.Top));
    }

    [Theory]
    [InlineData((int) EdgeScrollSide.Bottom, 480)]   // 笔画在热区上方
    [InlineData((int) EdgeScrollSide.Bottom, 600)]   // 与热区下边界相接
    [InlineData((int) EdgeScrollSide.Top, 620)]      // 笔画在热区下方
    [InlineData((int) EdgeScrollSide.Top, 480)]      // 与热区上边界相接
    public void BandClearDelta_InkOutsideBand_ReturnsZero(int side, double inkTop)
    {
        var band = new Rect(0, 500, 200, Threshold);

        Assert.Equal(0, InkCanvasNext.BandClearDelta(
            [new Rect(0, inkTop, 50, 20)], band, (EdgeScrollSide) side));
    }

    [Fact]
    public void BandClearDelta_IgnoresInkOutsideCrossAxis( )
    {
        var band = new Rect(0, 500, 200, Threshold);

        Assert.Equal(0, InkCanvasNext.BandClearDelta([new Rect(400, 520, 50, 20)], band, EdgeScrollSide.Bottom));
        Assert.Equal(40, InkCanvasNext.BandClearDelta([new Rect(0, 520, 50, 20)], band, EdgeScrollSide.Bottom));
    }

    [Fact]
    public void BandClearDelta_RightAndLeft_MoveOnXAxis( )
    {
        var band = new Rect(500, 0, Threshold, 300);

        Assert.Equal(40, InkCanvasNext.BandClearDelta([new Rect(520, 0, 20, 50)], band, EdgeScrollSide.Right));
        Assert.Equal(40, InkCanvasNext.BandClearDelta([new Rect(560, 0, 20, 50)], band, EdgeScrollSide.Left));
    }
}
