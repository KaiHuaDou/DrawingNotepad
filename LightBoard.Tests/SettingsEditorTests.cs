using System.Linq;
using System.Text.Json;

using InkCanvasNext;

namespace LightBoard.Tests;

public class SettingsEditorTests
{
    [Fact]
    public void Field_OutOfRange_HasErrors( )
    {
        var field = Number(1, 200);

        field.Text = "0";
        Assert.True(field.HasErrors);

        field.Text = "201";
        Assert.True(field.HasErrors);

        field.Text = "";
        Assert.True(field.HasErrors);

        field.Text = "200";
        Assert.False(field.HasErrors);
    }

    [Fact]
    public void Field_NonNumeric_HasErrors( )
    {
        var field = Number(1, 200);

        foreach (var text in new[] { "abc", "--1", "1e", "0.5" })
        {
            field.Text = text;
            Assert.True(field.HasErrors, text);
        }
    }

    [Fact]
    public void Field_Integer_RejectsFraction( )
    {
        var field = Number(10, 1000, 10, isInteger: true);

        field.Text = "200.5";
        Assert.True(field.HasErrors);

        field.Text = "200";
        Assert.False(field.HasErrors);
    }

    [Fact]
    public void Field_Step_ClampsToRange( )
    {
        var field = Number(0, 1000, 8);

        field.Text = "96";
        field.Step(1);
        Assert.Equal("104", field.Text);

        field.Text = "1000";
        field.Step(1);
        Assert.Equal("1000", field.Text);

        field.Text = "0";
        field.Step(-1);
        Assert.Equal("0", field.Text);
    }

    [Fact]
    public void Field_Step_FromInvalidText_StartsAtDefault( )
    {
        var field = Number(0, 100);

        field.Text = "abc";
        field.Step(-1);

        Assert.Equal("7", field.Text);
        Assert.False(field.HasErrors);
    }

    [Fact]
    public void Field_Apply_WritesValue( )
    {
        var field = Number(0, 100);

        field.Text = "42";

        Assert.Equal(42, field.Apply(new LightBoardSettings( )).EraserPalmExtra);
    }

    [Fact]
    public void Field_Clear_ReturnsToUnset( )
    {
        var field = Number(0, 100);

        field.Text = "42";
        Assert.True(field.IsSet);

        field.Clear( );

        Assert.False(field.IsSet);
        Assert.Null(field.Apply(new LightBoardSettings( )).EraserPalmExtra);
        Assert.Equal(EraserParameters.PalmExtraDefault.ToString("0.####"), field.Text);
    }

    [Fact]
    public void Editor_Build_ReturnsSameWhenUntouched( )
    {
        var settings = new LightBoardSettings
        {
            TouchDisplacementThreshold = 21,
            EdgeScrollLeftThreshold = 120,
            MaxHistoryCount = 120,
            PalmEraseFingers = 4,
            EraserRebuildThreshold = 0.25,
            ToolbarScale = 1.25
        };

        Assert.Equal(settings, new SettingsEditor(settings).Build( ));
    }

    [Fact]
    public void Editor_Build_CollectsEveryItem( )
    {
        var editor = new SettingsEditor(new LightBoardSettings( ));

        Item<SettingsField>(editor, nameof(LightBoardSettings.TouchDisplacementThreshold)).Text = "25";
        Item<SettingsField>(editor, nameof(LightBoardSettings.TaperPoints)).Text = "12";
        Item<SettingsField>(editor, nameof(LightBoardSettings.EdgeScrollRightThreshold)).Text = "128";
        Item<SettingsField>(editor, nameof(LightBoardSettings.EraserMinLogicalDiameter)).Text = "2.5";
        Item<SettingsField>(editor, nameof(LightBoardSettings.MaxHistoryCount)).Text = "300";
        Item<SettingsField>(editor, nameof(LightBoardSettings.ToolbarScale)).Text = "1.5";
        Item<SettingsChoice>(editor, nameof(LightBoardSettings.PalmEraseFingers)).First = true;
        Item<SettingsChoice>(editor, nameof(LightBoardSettings.SelectionDragMode)).Second = true;
        Item<SettingsToggle>(editor, nameof(LightBoardSettings.EdgeScrollHint)).Checked = false;
        Item<SettingsToggle>(editor, nameof(LightBoardSettings.SmoothZoom)).Checked = false;
        Item<SettingsToggle>(editor, nameof(LightBoardSettings.Taper)).Checked = false;
        Item<SettingsToggle>(editor, nameof(LightBoardSettings.ToolAutoRevert)).Checked = true;
        Item<SettingsField>(editor, nameof(LightBoardSettings.ToolAutoRevertCount)).Text = "2";
        Item<SettingsToggle>(editor, nameof(LightBoardSettings.ShowRevertCount)).Checked = false;

        var settings = editor.Build( );

        Assert.Equal(25, settings.TouchDisplacementThreshold);
        Assert.Equal(12, settings.TaperPoints);
        Assert.Equal(128, settings.EdgeScrollRightThreshold);
        Assert.Equal(2.5, settings.EraserMinLogicalDiameter);
        Assert.Equal(300, settings.MaxHistoryCount);
        Assert.Equal(1.5, settings.ToolbarScale);
        Assert.Equal(4, settings.PalmEraseFingers);
        Assert.Equal(SelectionDragMode.Rectangle, settings.SelectionDragMode);
        Assert.False(settings.EdgeScrollHint);
        Assert.False(settings.SmoothZoom);
        Assert.False(settings.Taper);
        Assert.True(settings.ToolAutoRevert);
        Assert.Equal(2, settings.ToolAutoRevertCount);
        Assert.False(settings.ShowRevertCount);
        Assert.Null(settings.PinchLockDistance);
    }

    [Fact]
    public void Editor_Choice_RoundTripsAndClears( )
    {
        var editor = new SettingsEditor(new LightBoardSettings( ));
        var choice = Item<SettingsChoice>(editor, nameof(LightBoardSettings.SelectionDragMode));

        Assert.True(choice.First);
        Assert.False(choice.Second);
        Assert.False(choice.IsSet);

        choice.Second = true;

        Assert.False(choice.First);
        Assert.True(choice.IsSet);
        Assert.Equal(SelectionDragMode.Rectangle, editor.Build( ).SelectionDragMode);

        choice.Clear( );

        Assert.True(choice.First);
        Assert.False(choice.IsSet);
        Assert.Null(editor.Build( ).SelectionDragMode);
    }

    [Fact]
    public void Editor_Reset_RestoresDefaults( )
    {
        var editor = new SettingsEditor(new LightBoardSettings { ToolbarScale = 1.5, PalmEraseFingers = 4 });
        Item<SettingsField>(editor, nameof(LightBoardSettings.TouchDisplacementThreshold)).Text = "99";

        editor.Reset( );

        Assert.Equal(new LightBoardSettings( ), editor.Build( ));
        Assert.False(editor.HasErrors);
    }

    [Fact]
    public void Editor_Load_KeepsHandEditedPrecision( )
    {
        var settings = new LightBoardSettings { EraserRebuildThreshold = 1.2345 };

        Assert.Equal(settings, new SettingsEditor(settings).Build( ));
    }

    [Fact]
    public void Editor_ProcessName_TrimsAndApplies( )
    {
        var editor = new SettingsEditor(new LightBoardSettings( ));

        Item<SettingsText>(editor, nameof(LightBoardSettings.SwitchOutProcessName)).Text = "  notepad  ";

        Assert.Equal("notepad", editor.Build( ).SwitchOutProcessName);
    }

    [Fact]
    public void Editor_EmptyProcessName_StaysUnset( )
    {
        var editor = new SettingsEditor(new LightBoardSettings { SwitchOutProcessName = "chrome" });

        Item<SettingsText>(editor, nameof(LightBoardSettings.SwitchOutProcessName)).Text = "   ";

        Assert.Null(editor.Build( ).SwitchOutProcessName);
    }

    [Fact]
    public void Editor_Reset_RestoresProcessName( )
    {
        var editor = new SettingsEditor(new LightBoardSettings { SwitchOutProcessName = "chrome" });

        editor.Reset( );

        Assert.Null(editor.Build( ).SwitchOutProcessName);
        Assert.Equal(new LightBoardSettings( ).EffectiveSwitchOutProcess, Item<SettingsText>(editor, nameof(LightBoardSettings.SwitchOutProcessName)).Text);
    }

    [Fact]
    public void Editor_InvalidField_RaisesHasErrors( )
    {
        var editor = new SettingsEditor(new LightBoardSettings( ));
        var raised = 0;
        editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsEditor.HasErrors))
            {
                raised++;
            }
        };

        Item<SettingsField>(editor, nameof(LightBoardSettings.TouchDisplacementThreshold)).Text = "abc";

        Assert.True(editor.HasErrors);
        Assert.True(raised > 0);
    }

    [Fact]
    public void Editor_FirstError_ReportsSectionAndItem( )
    {
        var editor = new SettingsEditor(new LightBoardSettings( ));

        Item<SettingsField>(editor, nameof(LightBoardSettings.ToolbarScale)).Text = "9";

        var error = editor.FirstError;

        Assert.NotNull(error);
        Assert.Equal("工具栏", error.Value.Section.Title);
        Assert.Equal(nameof(LightBoardSettings.ToolbarScale), error.Value.Item.Key);
    }

    [Fact]
    public void Items_CoverEverySettingProperty( )
    {
        var editor = new SettingsEditor(new LightBoardSettings( ));
        var keys = editor.Sections.SelectMany(section => section.Items).Select(item => item.Key).ToHashSet( );
        var properties = typeof(LightBoardSettings).GetProperties( )
            .Where(property => property.CanWrite)
            .Select(property => property.Name)
            .ToHashSet( );

        Assert.Empty(properties.Except(keys));
        Assert.Empty(keys.Except(properties));
    }

    [Fact]
    public void SettingsJson_MissingSelectionDragMode_StaysUnset( )
    {
        var settings = JsonSerializer.Deserialize("{}", SettingsSerializerContext.Default.LightBoardSettings);

        Assert.Null(settings!.SelectionDragMode);
        Assert.Equal(SelectionDragMode.Lasso, settings.EffectiveSelectionDragMode);
    }

    private static SettingsField Number(double min, double max, double step = 1, bool isInteger = false)
    {
        return new SettingsField("项", nameof(LightBoardSettings.EraserPalmExtra), "DIP", min, max, step, isInteger,
            settings => settings.EffectiveEraserPalmExtra,
            settings => settings.EraserPalmExtra,
            (settings, value) => settings with { EraserPalmExtra = value });
    }

    private static T Item<T>(SettingsEditor editor, string key)
        where T : class, ISettingItem
    {
        return editor.Sections
            .SelectMany(section => section.Items)
            .OfType<T>( )
            .First(item => item.Key == key);
    }
}
