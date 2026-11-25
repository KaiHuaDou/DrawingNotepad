using System.Windows.Ink;

namespace InkCanvasNext.Tests;

/// <summary>
/// 笔锋剖面、压力反解与参数收拢
/// </summary>
public class TaperTests
{
    [Fact]
    public void Points_ClampedToRange( )
    {
        Assert.Equal(TaperParameters.PointsMax, new TaperParameters { Points = 999 }.Points);
        Assert.Equal(TaperParameters.PointsMin, new TaperParameters { Points = 0 }.Points);
        Assert.Equal(TaperParameters.PointsDefault, new TaperParameters( ).Points);
    }

    [Fact]
    public void Tail_KeepsOneFullWidthPoint( )
    {
        Assert.Equal(8, Taper.Tail(200, new TaperParameters { Points = 8 }));
        Assert.Equal(4, Taper.Tail(5, new TaperParameters { Points = 8 }));
        Assert.Equal(0, Taper.Tail(1, new TaperParameters { Points = 8 }));
        Assert.Equal(0, Taper.Tail(5, null));
    }

    [Fact]
    public void Ratio_StartsFullWidth_AndEndsAtTip( )
    {
        Assert.Equal(1, Taper.Ratio(0, 8));
        Assert.Equal(Taper.TipRatio, Taper.Ratio(7, 8), 6);
        Assert.Equal(1, Taper.Ratio(0, 1));
    }

    [Fact]
    public void Ratio_DecreasesMonotonically( )
    {
        var previous = double.MaxValue;
        for (var k = 0; k < 8; k++)
        {
            var ratio = Taper.Ratio(k, 8);
            Assert.True(ratio < previous);
            previous = ratio;
        }
    }

    [Fact]
    public void Pressure_FullRatio_RestoresOriginal( )
    {
        Assert.Equal(0.5, (double) Taper.Pressure(0.5f, 1), 5);
    }

    [Fact]
    public void Pressure_TipRatio_ReachesPressureFloor( )
    {
        Assert.Equal(0, (double) Taper.Pressure(0.5f, Taper.TipRatio), 5);
    }

    [Fact]
    public void Pressure_ScalesWidthExactly( )
    {
        const float pressure = 0.4f;
        const double ratio = 0.6;
        var scaled = Taper.Pressure(pressure, ratio);

        Assert.Equal(ratio * (1.5 * pressure + 0.25), 1.5 * scaled + 0.25, 5);
    }

    [Fact]
    public void Pressure_BelowFloor_ClampsToZero( )
    {
        Assert.Equal(0, (double) Taper.Pressure(0.5f, 0.1), 5);
    }

    [Fact]
    public void Effective_FollowsEnabledIgnorePressureAndTail( )
    {
        StaTest.Run(( ) =>
        {
            var enabled = new TaperParameters( );
            var disabled = new TaperParameters { Enabled = false };
            var attributes = new DrawingAttributes( );

            Assert.Same(enabled, Taper.Effective(enabled, attributes));
            Assert.Null(Taper.Effective(disabled, attributes));
            Assert.Null(Taper.Effective(enabled, attributes, continues: true));

            attributes.IgnorePressure = true;
            Assert.Null(Taper.Effective(enabled, attributes));
        });
    }
}
