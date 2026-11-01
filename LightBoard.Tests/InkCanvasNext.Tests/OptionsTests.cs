using System.Windows;

namespace InkCanvasNext.Tests;

/// <summary>构造期选项的字段与派生</summary>
public class OptionsTests
{
    [Fact]
    public void TouchSpacingThreshold_Custom_OverridesDerived( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost(options: new( ) { TouchSpacingThreshold = 500 });

            Assert.Equal(500, Math.Sqrt(host.Canvas.distanceThreshold2), 6);
        });
    }

    [Fact]
    public void TouchSpacingThreshold_Default_DerivesFromWorkArea( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var expected = InkCanvasNext.DistanceThresholdFactor * SystemParameters.WorkArea.Width;

            Assert.Equal(expected, Math.Sqrt(host.Canvas.distanceThreshold2), 6);
        });
    }
}
