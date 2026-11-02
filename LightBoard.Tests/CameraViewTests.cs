using System.Windows;

namespace LightBoard.Tests;

/// <summary>
/// 展台取景几何：旋转后的等比适配、旋转前的布局尺寸
/// </summary>
public class CameraViewTests
{
    [Fact]
    public void Fit_NoRotation_FillsSameAspectArea( )
    {
        var rect = CameraView.Fit(new Size(1920, 1080), 0, new Size(1920, 1080));

        Assert.Equal(new Rect(0, 0, 1920, 1080), rect);
    }

    [Fact]
    public void Fit_NoRotation_CentersWhenAreaIsWider( )
    {
        var rect = CameraView.Fit(new Size(1920, 1080), 0, new Size(2560, 1080));

        Assert.Equal(320, rect.X, 9);
        Assert.Equal(0, rect.Y, 9);
        Assert.Equal(1920, rect.Width, 9);
        Assert.Equal(1080, rect.Height, 9);
    }

    [Theory]
    [InlineData(90)]
    [InlineData(270)]
    public void Fit_QuarterTurn_FitsSwappedAspect(int angle)
    {
        var rect = CameraView.Fit(new Size(1920, 1080), angle, new Size(1920, 1080));

        Assert.Equal(607.5, rect.Width, 9);
        Assert.Equal(1080, rect.Height, 9);
        Assert.Equal(656.25, rect.X, 9);
        Assert.Equal(0, rect.Y, 9);
    }

    [Fact]
    public void Fit_HalfTurn_KeepsAspect( )
    {
        var rect = CameraView.Fit(new Size(1920, 1080), 180, new Size(1920, 1080));

        Assert.Equal(new Rect(0, 0, 1920, 1080), rect);
    }

    [Theory]
    [InlineData(0, 1920, 1080)]
    [InlineData(90, 1080, 1920)]
    [InlineData(180, 1920, 1080)]
    [InlineData(270, 1080, 1920)]
    public void BeforeRotation_SwapsOnQuarterTurns(int angle, double width, double height)
    {
        var size = CameraView.BeforeRotation(new Size(1920, 1080), angle);

        Assert.Equal(width, size.Width, 9);
        Assert.Equal(height, size.Height, 9);
    }

    [Theory]
    [InlineData(0, 0, 1920, 1080)]
    [InlineData(1920, 1080, 0, 1080)]
    [InlineData(1920, 1080, 1920, 0)]
    public void Fit_DegenerateInput_ReturnsEmpty(double frameWidth, double frameHeight, double areaWidth, double areaHeight)
    {
        var rect = CameraView.Fit(new Size(frameWidth, frameHeight), 0, new Size(areaWidth, areaHeight));

        Assert.True(rect.IsEmpty);
    }
}
