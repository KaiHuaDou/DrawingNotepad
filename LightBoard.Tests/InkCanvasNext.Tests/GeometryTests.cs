using System.Windows;

namespace InkCanvasNext.Tests;

public class GeometryTests
{
    [Theory]
    [InlineData(0, 0, 3, 4, 5)]
    [InlineData(1, 1, 1, 1, 0)]
    [InlineData(-2, -2, 1, 2, 5)]
    [InlineData(0, 0, 0, -6, 6)]
    public void Distance_ReturnsEuclideanDistance(double ax, double ay, double bx, double by, double expected)
    {
        var actual = Geometry.Distance(new Point(ax, ay), new Point(bx, by));
        Assert.Equal(expected, actual, 12);
    }

    [Theory]
    [InlineData(0, 0, 3, 4, 25)]
    [InlineData(1, 1, 1, 1, 0)]
    [InlineData(2, 3, 2, 7, 16)]
    public void Distance2_ReturnsSquaredDistance(double ax, double ay, double bx, double by, double expected)
    {
        var actual = Geometry.Distance2(new Point(ax, ay), new Point(bx, by));
        Assert.Equal(expected, actual, 12);
    }
}
