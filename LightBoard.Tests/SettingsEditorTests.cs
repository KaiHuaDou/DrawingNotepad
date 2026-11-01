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
        Assert.Equal(1, field.Apply(new LightBoardSettings( )).ToolbarScale);
    }

    [Fact]
    public void Editor_Build_ReturnsBaselineWhenUntouched( )
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
    public void Editor_Build_CollectsEveryField( )
    {
        var editor = new SettingsEditor(new LightBoardSettings( ));
        editor.GestureFields[0].Text = "25";
        editor.EdgeFields[3].Text = "128";
        editor.EraserFields[2].Text = "2.5";
        editor.UndoFields[0].Text = "300";
        editor.ToolbarFields[0].Text = "1.5";
        editor.FourFingerErase = true;
        editor.EdgeScrollHint = false;

        var settings = editor.Build( );

        Assert.Equal(25, settings.TouchDisplacementThreshold);
        Assert.Equal(128, settings.EdgeScrollRightThreshold);
        Assert.Equal(2.5, settings.EraserMinLogicalDiameter);
        Assert.Equal(300, settings.MaxHistoryCount);
        Assert.Equal(1.5, settings.ToolbarScale);
        Assert.Equal(4, settings.PalmEraseFingers);
        Assert.False(settings.EdgeScrollHint);
    }

    [Fact]
    public void Editor_Reset_RestoresDefaults( )
    {
        var editor = new SettingsEditor(new LightBoardSettings { ToolbarScale = 1.5, PalmEraseFingers = 4 });
        editor.GestureFields[0].Text = "99";

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

        editor.SwitchOutProcess = "  notepad  ";

        Assert.Equal("notepad", editor.Build( ).SwitchOutProcessName);
    }

    [Fact]
    public void Editor_EmptyProcessName_KeepsBaseline( )
    {
        var editor = new SettingsEditor(new LightBoardSettings { SwitchOutProcessName = "chrome" });

        editor.SwitchOutProcess = "   ";

        Assert.Equal("chrome", editor.Build( ).SwitchOutProcessName);
    }

    [Fact]
    public void Editor_Reset_RestoresProcessName( )
    {
        var editor = new SettingsEditor(new LightBoardSettings { SwitchOutProcessName = "chrome" });

        editor.Reset( );

        Assert.Equal(new LightBoardSettings( ).SwitchOutProcessName, editor.Build( ).SwitchOutProcessName);
    }

    [Fact]
    public void Editor_CoversEveryNumericSetting( )
    {
        Assert.Equal(13, new SettingsEditor(new LightBoardSettings( )).Fields.Count);
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

        editor.GestureFields[0].Text = "abc";

        Assert.True(editor.HasErrors);
        Assert.True(raised > 0);
    }

    private static SettingsField Number(double min, double max, double step = 1, bool isInteger = false)
    {
        return new SettingsField("项", "DIP", min, max, step, isInteger,
            s => s.EraserPalmExtra, (s, v) => s with { EraserPalmExtra = v });
    }
}
