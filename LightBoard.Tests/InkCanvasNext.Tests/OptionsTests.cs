using System.Windows;

namespace InkCanvasNext.Tests;

/// <summary>构造期选项的收拢与派生</summary>
public class OptionsTests
{
    [Fact]
    public void TouchSpacingThreshold_Custom_Kept( )
    {
        var options = new InkCanvasNextOptions { TouchSpacingThreshold = 500 };

        Assert.Equal(500, options.TouchSpacingThreshold, 6);
    }

    [Fact]
    public void TouchSpacingThreshold_Default_DerivesFromWorkArea( )
    {
        var options = new InkCanvasNextOptions( );
        var expected = InkCanvasNextOptions.DistanceThresholdFactor * SystemParameters.WorkArea.Width;

        Assert.Equal(expected, options.TouchSpacingThreshold, 6);
    }

    [Fact]
    public void TouchSpacingThreshold_Invalid_DerivesFromWorkArea( )
    {
        var options = new InkCanvasNextOptions { TouchSpacingThreshold = -1 };

        Assert.Equal(new InkCanvasNextOptions( ).TouchSpacingThreshold, options.TouchSpacingThreshold, 6);
    }

    [Fact]
    public void SquaredThresholds_FollowInitValues( )
    {
        var options = new InkCanvasNextOptions
        {
            TouchDisplacementThreshold = 7,
            PanZoomDisplaceThreshold = 9,
            PinchLockDistance = 11
        };

        Assert.Equal(49, options.TouchDisplacementThreshold2, 6);
        Assert.Equal(81, options.PanZoomDisplaceThreshold2, 6);
        Assert.Equal(121, options.PinchLockDistance2, 6);
        Assert.Equal(options.TouchSpacingThreshold * options.TouchSpacingThreshold, options.TouchSpacingThreshold2, 6);
    }
}
