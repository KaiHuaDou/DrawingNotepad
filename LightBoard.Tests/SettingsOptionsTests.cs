using System;

namespace LightBoard.Tests;

public class SettingsOptionsTests
{
    [Fact]
    public void Load_SavedThirdIndex_SelectsThird( )
    {
        var item = Create( );
        item.Load(new LightBoardSettings { LastExportFormat = ExportFormat.Svg });

        Assert.True(item.IsSet);
        Assert.True(item.Options[2].IsSelected);
        Assert.False(item.Options[0].IsSelected);
    }

    [Fact]
    public void Select_Third_AppliesThirdValue( )
    {
        var item = Create( );
        item.Options[2].IsSelected = true;

        Assert.True(item.IsSet);
        Assert.Equal(ExportFormat.Svg, item.Apply(new LightBoardSettings( )).LastExportFormat);
    }

    [Fact]
    public void Clear_ReturnsToDefaultAndUnset( )
    {
        var item = Create( );
        item.Options[2].IsSelected = true;

        item.Clear( );

        Assert.False(item.IsSet);
        Assert.True(item.Options[0].IsSelected);
        Assert.Null(item.Apply(new LightBoardSettings( )).LastExportFormat);
    }

    [Fact]
    public void Load_InvalidIndex_StaysUnset( )
    {
        var item = Create( );
        item.Load(new LightBoardSettings { LastExportFormat = (ExportFormat) 99 });

        Assert.False(item.IsSet);
        Assert.True(item.Options[0].IsSelected);
    }

    [Fact]
    public void Select_RaisesIsSelectedForEveryOption( )
    {
        var item = Create( );
        var raised = 0;
        foreach (var option in item.Options)
        {
            option.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SettingsOption.IsSelected))
                {
                    raised++;
                }
            };
        }

        item.Options[1].IsSelected = true;

        Assert.Equal(3, raised);
    }

    private static SettingsOptions Create( )
    {
        return new SettingsOptions(
            "格式",
            nameof(LightBoardSettings.LastExportFormat),
            ["PNG 图片", "PDF 文档", "SVG 矢量图"],
            s => (int) s.EffectiveExportFormat,
            s => s.LastExportFormat is { } format && Enum.IsDefined(format) ? (int) format : null,
            (s, index) => s with { LastExportFormat = index is null ? null : (ExportFormat) index.Value });
    }
}
