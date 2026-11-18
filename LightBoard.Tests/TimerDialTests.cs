using System.Windows;

namespace LightBoard.Tests;

/// <summary>
/// 钟面的纯计算：指针角度到分钟档的换算，剩余量到扫过角度
/// </summary>
public class TimerDialTests
{
    [Theory]
    [InlineData(0, -1, 0)]
    [InlineData(1, 0, 15)]
    [InlineData(0, 1, 30)]
    [InlineData(-1, 0, 45)]
    [InlineData(-0.1, -1, 59)]
    public void MinutesAt_ClockFacePositions(double x, double y, int expected)
    {
        Assert.Equal(expected, TimerDial.MinutesAt(new Vector(x, y)));
    }

    [Fact]
    public void MinutesAt_OneTickBeforeTwelve_IsFiftyNine( )
    {
        const double radians = 354 * Math.PI / 180;

        Assert.Equal(59, TimerDial.MinutesAt(new Vector(Math.Sin(radians), -Math.Cos(radians))));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.25, 90)]
    [InlineData(0.5, 180)]
    [InlineData(1, 360)]
    [InlineData(-0.5, 0)]
    [InlineData(1.5, 360)]
    public void SweepDegrees_FollowsProgress(double progress, double expected)
    {
        Assert.Equal(expected, TimerDial.SweepDegrees(progress), 6);
    }

    [Fact]
    public void IsOnRing_AcceptsOnlyRingBand( )
    {
        const double side = 200;
        var radius = (side - TimerDial.RingThickness) / 2;
        var half = TimerDial.RingThickness / 2;

        Assert.True(TimerDial.IsOnRing(new Vector(radius, 0), side));
        Assert.True(TimerDial.IsOnRing(new Vector(radius - half, 0), side));
        Assert.True(TimerDial.IsOnRing(new Vector(radius + half, 0), side));

        // 中间与环外都不响应
        Assert.False(TimerDial.IsOnRing(new Vector(0, 0), side));
        Assert.False(TimerDial.IsOnRing(new Vector(radius - half - 1, 0), side));
        Assert.False(TimerDial.IsOnRing(new Vector(radius + half + 1, 0), side));
    }
}
