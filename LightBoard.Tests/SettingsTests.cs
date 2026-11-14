using System.Linq;
using System.Text.Json;

namespace LightBoard.Tests;

public class SettingsTests
{
    [Fact]
    public void RoundTrip_KeepsOnlySetValues( )
    {
        var data = new LightBoardSettings
        {
            EdgeScrollTopThreshold = 64,
            PalmEraseFingers = 4,
            EraserDiameter = 80,
            EraserMinLogicalDiameter = 2,
            ToolbarScale = 1.25
        };

        var json = JsonSerializer.Serialize(data, SettingsSerializerContext.Default.LightBoardSettings);
        var roundTrip = JsonSerializer.Deserialize(json, SettingsSerializerContext.Default.LightBoardSettings);

        Assert.Equal(data, roundTrip);
        Assert.Equal(5, JsonDocument.Parse(json).RootElement.EnumerateObject( ).Count( ));

        // 投影成员不进设置文件
        Assert.DoesNotContain("Effective", json);
    }

    [Fact]
    public void EmptySettings_WritesEmptyObject( )
    {
        var json = JsonSerializer.Serialize(new LightBoardSettings( ), SettingsSerializerContext.Default.LightBoardSettings);

        Assert.Equal("{}", json.Trim( ));
    }

    [Fact]
    public void OnlySetValues_Written( )
    {
        var json = JsonSerializer.Serialize(
            new LightBoardSettings { SmoothZoom = false, MaxHistoryCount = 50, SelectionDragMode = InkCanvasNext.SelectionDragMode.Rectangle },
            SettingsSerializerContext.Default.LightBoardSettings);

        Assert.Contains("\"SmoothZoom\": false", json);
        Assert.Contains("\"MaxHistoryCount\": 50", json);
        Assert.Contains("\"SelectionDragMode\": \"Rectangle\"", json);
        Assert.Equal(3, JsonDocument.Parse(json).RootElement.EnumerateObject( ).Count( ));
    }

    [Fact]
    public void Deserialize_MissingFields_FollowDefaults( )
    {
        var data = JsonSerializer.Deserialize("""{"EdgeScrollHint": false}""", SettingsSerializerContext.Default.LightBoardSettings);

        Assert.NotNull(data);
        Assert.False(data.EffectiveEdgeScrollHint);
        Assert.Null(data.SmoothZoom);
        Assert.True(data.EffectiveSmoothZoom);
        Assert.True(data.EffectiveFitToCurve);
        Assert.True(data.EffectiveTaper);
        Assert.Equal(InkCanvasNext.TaperParameters.PointsDefault, data.EffectiveTaperPoints);
        Assert.Equal(InkCanvasNext.InkCanvasNextOptions.EdgeScrollTopDefault, data.EffectiveEdgeScrollTopThreshold);
        Assert.Equal(InkCanvasNext.InkCanvasNextOptions.EdgeScrollBottomDefault, data.EffectiveEdgeScrollBottomThreshold);
        Assert.Equal(InkCanvasNext.InkCanvasNextOptions.EdgeScrollLeftDefault, data.EffectiveEdgeScrollLeftThreshold);
        Assert.Equal(InkCanvasNext.InkCanvasNextOptions.EdgeScrollRightDefault, data.EffectiveEdgeScrollRightThreshold);
        Assert.Equal(InkCanvasNext.InkCanvasNextOptions.PalmEraseFingersDefault, data.EffectivePalmEraseFingers);
        Assert.Equal(InkCanvasNext.EraserParameters.DiameterDefault, data.EffectiveEraserDiameter);
        Assert.Equal(InkCanvasNext.EraserParameters.PalmExtraDefault, data.EffectiveEraserPalmExtra);
        Assert.Equal(InkCanvasNext.EraserParameters.MinLogicalDiameterDefault, data.EffectiveEraserMinLogicalDiameter);
        Assert.Equal(InkCanvasNext.EraserParameters.RebuildThresholdDefault, data.EffectiveEraserRebuildThreshold);
        Assert.Equal(LightBoardSettings.ToolbarScaleDefault, data.EffectiveToolbarScale);
    }

    [Fact]
    public void CreateCanvasOptions_MapsAllFields( )
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
            SmoothZoom = false,
            FitToCurve = false,
            Taper = false,
            TaperPoints = 9,
            MaxHistoryCount = 201,
            PalmEraseFingers = 4,
            EraserDiameter = 80,
            EraserPalmExtra = 12,
            EraserMinLogicalDiameter = 2,
            EraserRebuildThreshold = 0.25
        };

        var options = data.CreateCanvasOptions( );

        Assert.Equal(4, options.PalmEraseFingers);
        Assert.Equal(21, options.TouchDisplacementThreshold);
        Assert.Equal(31, options.PanZoomDisplaceThreshold);
        Assert.Equal(25, options.PinchLockDistance);
        Assert.False(options.SmoothZoom);
        Assert.False(options.FitToCurve);
        Assert.False(options.Taper.Enabled);
        Assert.Equal(9, options.Taper.Points);
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
    public void CreateCanvasOptions_EdgeScrollEnabled_PassesThresholds( )
    {
        var options = new LightBoardSettings
        {
            EdgeScrollHint = true,
            EdgeScrollTopThreshold = 65,
            EdgeScrollBottomThreshold = 193,
            EdgeScrollLeftThreshold = 65,
            EdgeScrollRightThreshold = 97
        }.CreateCanvasOptions( );

        Assert.Equal(65, options.EdgeScrollTopThreshold);
        Assert.Equal(193, options.EdgeScrollBottomThreshold);
        Assert.Equal(65, options.EdgeScrollLeftThreshold);
        Assert.Equal(97, options.EdgeScrollRightThreshold);
    }

    [Fact]
    public void OutOfRangeValues_ClampedByConsumers( )
    {
        var data = new LightBoardSettings
        {
            EdgeScrollTopThreshold = -5,
            EdgeScrollBottomThreshold = 5000,
            TouchDisplacementThreshold = 0,
            TaperPoints = 0,
            MaxHistoryCount = 5000,
            PalmEraseFingers = 9,
            EraserDiameter = 0,
            EraserPalmExtra = -3,
            EraserMinLogicalDiameter = 0,
            EraserRebuildThreshold = 99,
            ToolbarScale = 0,
            SwitchOutProcessName = "   ",
            SelectionDragMode = (InkCanvasNext.SelectionDragMode) 99
        };

        var options = data.CreateCanvasOptions( );

        Assert.Equal(0, options.EdgeScrollTopThreshold);
        Assert.Equal(1000, options.EdgeScrollBottomThreshold);
        Assert.Equal(InkCanvasNext.InkCanvasNextOptions.GestureMin, options.TouchDisplacementThreshold);
        Assert.Equal(InkCanvasNext.TaperParameters.PointsMin, options.Taper.Points);
        Assert.Equal(InkCanvasNext.InkCanvasNextOptions.MaxHistoryCountMax, options.MaxHistoryCount);
        Assert.Equal(InkCanvasNext.InkCanvasNextOptions.PalmEraseFingersMax, options.PalmEraseFingers);
        Assert.Equal(InkCanvasNext.EraserParameters.DiameterMin, options.Eraser.Diameter);
        Assert.Equal(InkCanvasNext.EraserParameters.PalmExtraMin, options.Eraser.PalmExtra);
        Assert.Equal(InkCanvasNext.EraserParameters.MinLogicalDiameterMin, options.Eraser.MinLogicalDiameter);
        Assert.Equal(InkCanvasNext.EraserParameters.RebuildThresholdMax, options.Eraser.RebuildThreshold);
        Assert.Equal(InkCanvasNext.InkCanvasNextOptions.SelectionDragModeDefault, options.SelectionDragMode);
        Assert.Equal(0, (data with { MaxHistoryCount = 0 }).CreateCanvasOptions( ).MaxHistoryCount);
        Assert.Equal(SettingsLimits.ToolbarScaleMin, data.EffectiveToolbarScale);
        Assert.Equal(1.5, (data with { ToolbarScale = 99 }).EffectiveToolbarScale);
        Assert.Equal(SettingsLimits.SwitchOutProcessDefault, data.EffectiveSwitchOutProcess);
        Assert.Equal(InkCanvasNext.InkCanvasNextOptions.SelectionDragModeDefault, data.EffectiveSelectionDragMode);
        Assert.Equal(InkCanvasNext.InkCanvasNextOptions.PalmEraseFingersMax, data.EffectivePalmEraseFingers);
    }

    [Fact]
    public void EdgeHintGap_ScalesBarHeight( )
    {
        Assert.Equal(95, MainWindow.EdgeHintGap(54, 5, 1.5));
        Assert.Equal(68, MainWindow.EdgeHintGap(54, 5, 1));
    }
}
