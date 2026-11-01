using System.Text.Json;

namespace LightBoard.Tests;

public class SettingsTests
{
    [Fact]
    public void RoundTrip_KeepsCanvasOptions( )
    {
        var data = new LightBoardSettings
        {
            EdgeScrollTopThreshold = 64,
            EdgeScrollBottomThreshold = 240,
            EdgeScrollLeftThreshold = 120,
            EdgeScrollRightThreshold = 8,
            PalmEraseFingers = 4,
            EraserDiameter = 80,
            EraserPalmExtra = 12,
            EraserMinLogicalDiameter = 2,
            EraserRebuildThreshold = 0.25,
            ToolbarScale = 1.25
        };

        var json = JsonSerializer.Serialize(data, SettingsSerializerContext.Default.LightBoardSettings);
        var roundTrip = JsonSerializer.Deserialize(json, SettingsSerializerContext.Default.LightBoardSettings);

        Assert.Equal(data, roundTrip);
        Assert.DoesNotContain("\"CanvasOptions\"", json);
    }

    [Fact]
    public void Deserialize_MissingFields_UseDefaults( )
    {
        var data = JsonSerializer.Deserialize("""{"EdgeScrollHint": false}""", SettingsSerializerContext.Default.LightBoardSettings);

        Assert.NotNull(data);
        Assert.False(data.EdgeScrollHint);
        Assert.Equal(InkCanvasNext.InkCanvasNext.EdgeScrollTopDefaultThreshold, data.EdgeScrollTopThreshold);
        Assert.Equal(InkCanvasNext.InkCanvasNext.EdgeScrollBottomDefaultThreshold, data.EdgeScrollBottomThreshold);
        Assert.Equal(InkCanvasNext.InkCanvasNext.EdgeScrollLeftDefaultThreshold, data.EdgeScrollLeftThreshold);
        Assert.Equal(InkCanvasNext.InkCanvasNext.EdgeScrollRightDefaultThreshold, data.EdgeScrollRightThreshold);
        Assert.Equal(InkCanvasNext.InkCanvasNext.PalmEraseFingersDefault, data.PalmEraseFingers);
        Assert.Equal(InkCanvasNext.InkCanvasNext.EraserDiameterDefault, data.EraserDiameter);
        Assert.Equal(InkCanvasNext.InkCanvasNext.EraserPalmExtraDefault, data.EraserPalmExtra);
        Assert.Equal(InkCanvasNext.InkCanvasNext.EraserMinLogicalDiameterDefault, data.EraserMinLogicalDiameter);
        Assert.Equal(InkCanvasNext.InkCanvasNext.EraserRebuildThresholdDefault, data.EraserRebuildThreshold);
        Assert.Equal(LightBoardSettings.ToolbarScaleDefault, data.ToolbarScale);
    }

    [Fact]
    public void CanvasOptions_CompareByValue( )
    {
        var a = new LightBoardSettings( ).CanvasOptions;
        var b = new LightBoardSettings( ).CanvasOptions;

        Assert.Equal(a, b);
        Assert.NotEqual(a, new LightBoardSettings { EraserPalmExtra = 9 }.CanvasOptions);
    }

    [Fact]
    public void CanvasOptions_MapsAllFields( )
    {
        var data = new LightBoardSettings
        {
            EdgeScrollHint = false,
            EdgeScrollTopThreshold = 65,
            EdgeScrollBottomThreshold = 193,
            EdgeScrollLeftThreshold = 65,
            EdgeScrollRightThreshold = 97,
            TouchDisplacementThreshold = 21,
            PanZoomDisplaceThreshold = 31,
            PinchLockDistance = 25,
            MaxHistoryCount = 201,
            PalmEraseFingers = 4,
            EraserDiameter = 80,
            EraserPalmExtra = 12,
            EraserMinLogicalDiameter = 2,
            EraserRebuildThreshold = 0.25
        };

        var options = data.CanvasOptions;

        Assert.Equal(4, options.PalmEraseFingers);
        Assert.Equal(21, options.TouchDisplacementThreshold);
        Assert.Equal(31, options.PanZoomDisplaceThreshold);
        Assert.Equal(25, options.PinchLockDistance);
        Assert.Equal(0, options.EdgeScrollTopThreshold);
        Assert.Equal(0, options.EdgeScrollBottomThreshold);
        Assert.Equal(0, options.EdgeScrollLeftThreshold);
        Assert.Equal(0, options.EdgeScrollRightThreshold);
        Assert.Equal(201, options.MaxHistoryCount);
        Assert.Equal(80, options.Eraser.Diameter);
        Assert.Equal(12, options.Eraser.PalmExtra);
        Assert.Equal(2, options.Eraser.MinLogicalDiameter);
        Assert.Equal(0.25, options.Eraser.RebuildThreshold);
    }

    [Fact]
    public void CanvasOptions_EdgeScrollEnabled_PassesThresholds( )
    {
        var options = new LightBoardSettings
        {
            EdgeScrollHint = true,
            EdgeScrollTopThreshold = 65,
            EdgeScrollBottomThreshold = 193,
            EdgeScrollLeftThreshold = 65,
            EdgeScrollRightThreshold = 97
        }.CanvasOptions;

        Assert.Equal(65, options.EdgeScrollTopThreshold);
        Assert.Equal(193, options.EdgeScrollBottomThreshold);
        Assert.Equal(65, options.EdgeScrollLeftThreshold);
        Assert.Equal(97, options.EdgeScrollRightThreshold);
    }

    [Fact]
    public void Normalize_ClampsOutOfRangeValues( )
    {
        var data = new LightBoardSettings
        {
            EdgeScrollTopThreshold = -5,
            EdgeScrollBottomThreshold = 5000,
            TouchDisplacementThreshold = 0,
            MaxHistoryCount = 5,
            PalmEraseFingers = 9,
            EraserDiameter = 0,
            EraserPalmExtra = -3,
            EraserMinLogicalDiameter = 0,
            EraserRebuildThreshold = 99,
            ToolbarScale = 0
        };

        var normalized = LightBoardSettings.Normalize(data);

        Assert.Equal(0, normalized.EdgeScrollTopThreshold);
        Assert.Equal(1000, normalized.EdgeScrollBottomThreshold);
        Assert.Equal(1, normalized.TouchDisplacementThreshold);
        Assert.Equal(10, normalized.MaxHistoryCount);
        Assert.Equal(InkCanvasNext.InkCanvasNext.PalmEraseFingersMax, normalized.PalmEraseFingers);
        Assert.Equal(1, normalized.EraserDiameter);
        Assert.Equal(0, normalized.EraserPalmExtra);
        Assert.Equal(0.1, normalized.EraserMinLogicalDiameter);
        Assert.Equal(10, normalized.EraserRebuildThreshold);
        Assert.Equal(0.75, normalized.ToolbarScale);
        Assert.Equal(1.5, LightBoardSettings.Normalize(data with { ToolbarScale = 99 }).ToolbarScale);
    }

    [Fact]
    public void EdgeHintGap_ScalesBarHeight( )
    {
        Assert.Equal(95, MainWindow.EdgeHintGap(54, 5, 1.5));
        Assert.Equal(68, MainWindow.EdgeHintGap(54, 5, 1));
    }
}
