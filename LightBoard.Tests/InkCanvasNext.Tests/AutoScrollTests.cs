using System.Windows;

namespace InkCanvasNext.Tests;

/// <summary>
/// 抬手贴边自动滚动的目标视图：四边触发、角落双轴、阈值边界、截停与缩放下的落点映射。
/// </summary>
public class AutoScrollTests
{
    private const double V = InkCanvasNext.AutoScrollVerticalDefaultThreshold;
    private const double H = InkCanvasNext.AutoScrollHorizontalDefaultThreshold;

    private const double ViewportWidth = 1920;
    private const double ViewportHeight = 1080;

    [Theory]
    [InlineData(1.0, 500, 500, 960, 1040, 500, 556)]      // 贴下边：offsetY += V - 40
    [InlineData(1.0, 500, 500, 960, 40, 500, 444)]        // 贴上边：offsetY -= V - 40
    [InlineData(1.0, 300, 200, 1880, 540, 356, 200)]      // 贴右边：offsetX += V - 40
    [InlineData(1.0, 300, 200, 40, 540, 244, 200)]        // 贴左边：offsetX -= V - 40
    [InlineData(1.0, 300, 200, 30, 40, 234, 144)]         // 角落：两轴同时触发
    [InlineData(1.0, 300, 200, 960, 540, 300, 200)]       // 画布中部：不滚动
    [InlineData(1.0, 300, 200, 96, 984, 300, 200)]        // 恰在阈值上：不滚动
    [InlineData(1.0, 0, 0, 960, 30, 0, 0)]                // 已在画布顶部：下限截停
    [InlineData(1.0, 3000, 3000, 960, 1000, 3000, 3010)]  // 超出上限：上限截停
    public void EdgeAutoScrollTarget_ComputesOffsetPerAxis(
        double scale, double offsetX, double offsetY,
        double touchX, double touchY,
        double expectedX, double expectedY)
    {
        var actual = InkCanvasNext.EdgeAutoScrollTarget(
            new View(scale, offsetX, offsetY),
            new Point(touchX, touchY),
            new Size(ViewportWidth, ViewportHeight),
            V, H,
            3010, 3010);

        Assert.Equal(expectedX, actual.OffsetX, 12);
        Assert.Equal(expectedY, actual.OffsetY, 12);
        Assert.Equal(scale, actual.Scale, 12);
    }

    [Fact]
    public void EdgeAutoScrollTarget_PutsTouchPointAtThresholdFromEdge( )
    {
        var view = new View(2.0, 100, 200);
        var viewport = new Size(800, 600);
        var touch = new Point(770, 300);
        var content = view.ToContent(touch);

        var target = InkCanvasNext.EdgeAutoScrollTarget(
            view, touch, viewport, V, H, 10000, 10000);

        Assert.Equal(viewport.Width - H, target.ToViewport(content).X, 9);
        Assert.Equal(touch.Y, target.ToViewport(content).Y, 9);
    }
}
