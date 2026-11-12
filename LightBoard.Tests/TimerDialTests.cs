using System;
using System.Windows;

namespace LightBoard.Tests;

/// <summary>
/// 钟面的纯计算：指针角度到 5 分钟档的换算，剩余量到扫过角度
/// </summary>
public class TimerDialTests
{
    [Theory]
    [InlineData(0, -1, 0)]
    [InlineData(1, 0, 15)]
    [InlineData(0, 1, 30)]
    [InlineData(-1, 0, 45)]
    [InlineData(-0.1, -1, 0)]
    public void MinutesAt_ClockFacePositions(double x, double y, int expected)
    {
        Assert.Equal(expected, TimerDial.MinutesAt(new Vector(x, y)));
    }

    [Fact]
    public void MinutesAt_JustBeforeTwelve_RoundsToZero( )
    {
        var radians = 354 * Math.PI / 180;

        Assert.Equal(0, TimerDial.MinutesAt(new Vector(Math.Sin(radians), -Math.Cos(radians))));
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
}
