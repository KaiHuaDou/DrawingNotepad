using System.Text.Json;

namespace LightBoard.Tests;

public class SettingsTests
{
    [Fact]
    public void RoundTrip_KeepsCanvasOptions( )
    {
        var data = new LightBoardSettings
        {
            PalmEraseFingers = 4,
            EraserDiameter = 80,
            EraserPalmExtra = 12,
            EraserMinLogicalDiameter = 2,
            EraserRebuildThreshold = 0.25
        };

        var json = JsonSerializer.Serialize(data, SettingsSerializerContext.Default.LightBoardSettings);
        var roundTrip = JsonSerializer.Deserialize(json, SettingsSerializerContext.Default.LightBoardSettings);

        Assert.Equal(data, roundTrip);
        Assert.DoesNotContain("\"CanvasOptions\"", json);
    }

    [Fact]
    public void Deserialize_MissingFields_UseDefaults( )
    {
        var data = JsonSerializer.Deserialize("""{"AutoScroll": false}""", SettingsSerializerContext.Default.LightBoardSettings);

        Assert.NotNull(data);
        Assert.False(data.AutoScroll);
        Assert.Equal(InkCanvasNext.InkCanvasNext.PalmEraseFingersDefault, data.PalmEraseFingers);
        Assert.Equal(InkCanvasNext.InkCanvasNext.EraserDiameterDefault, data.EraserDiameter);
        Assert.Equal(InkCanvasNext.InkCanvasNext.EraserPalmExtraDefault, data.EraserPalmExtra);
        Assert.Equal(InkCanvasNext.InkCanvasNext.EraserMinLogicalDiameterDefault, data.EraserMinLogicalDiameter);
        Assert.Equal(InkCanvasNext.InkCanvasNext.EraserRebuildThresholdDefault, data.EraserRebuildThreshold);
    }

    [Fact]
    public void CanvasOptions_CompareByValue( )
    {
        var a = new LightBoardSettings( ).CanvasOptions;
        var b = new LightBoardSettings( ).CanvasOptions;

        Assert.Equal(a, b);
        Assert.NotEqual(a, (new LightBoardSettings { EraserPalmExtra = 9 }).CanvasOptions);
    }

    [Fact]
    public void Normalize_ClampsOutOfRangeValues( )
    {
        var data = new LightBoardSettings
        {
            TouchDisplacementThreshold = 0,
            MaxHistoryCount = 5,
            PalmEraseFingers = 9,
            EraserDiameter = 0,
            EraserPalmExtra = -3,
            EraserMinLogicalDiameter = 0,
            EraserRebuildThreshold = 99
        };

        var normalized = Settings.Normalize(data);

        Assert.Equal(1, normalized.TouchDisplacementThreshold);
        Assert.Equal(10, normalized.MaxHistoryCount);
        Assert.Equal(InkCanvasNext.InkCanvasNext.PalmEraseFingersMax, normalized.PalmEraseFingers);
        Assert.Equal(1, normalized.EraserDiameter);
        Assert.Equal(0, normalized.EraserPalmExtra);
        Assert.Equal(0.1, normalized.EraserMinLogicalDiameter);
        Assert.Equal(10, normalized.EraserRebuildThreshold);
    }
}
