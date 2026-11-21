using System.Globalization;

namespace LightBoard.Tests;

/// <summary>抽选状态：范围与数量的夹紧、映射按行对应、抽选结果落在可选集合内</summary>
public class LotteryViewModelTests
{
    [Fact]
    public void Defaults( )
    {
        var model = new LotteryViewModel( );

        Assert.Equal(LotteryMode.Continuous, model.Mode);
        Assert.Equal(51, model.Max);
        Assert.Equal(1, model.Lower);
        Assert.Equal(51, model.Upper);
        Assert.Equal(5, model.Count);
        Assert.Equal(51, model.PoolSize);
        Assert.True(model.CanDraw);
    }

    [Fact]
    public void SetMax_ClampsRangeAndCount( )
    {
        var model = new LotteryViewModel( );

        model.SetMax(10);
        Assert.Equal(10, model.Upper);

        model.SetLower(8);
        model.SetCount(3);
        model.SetMax(5);

        Assert.Equal(5, model.Lower);
        Assert.Equal(5, model.Upper);
        Assert.Equal(1, model.Count);
        Assert.Equal(1, model.PoolSize);
    }

    [Fact]
    public void SetLower_StopsAtUpper( )
    {
        var model = new LotteryViewModel( );
        model.SetUpper(20);

        model.SetLower(30);

        Assert.Equal(20, model.Lower);
    }

    [Fact]
    public void SetUpper_StopsAtLower( )
    {
        var model = new LotteryViewModel( );
        model.SetLower(20);

        model.SetUpper(5);

        Assert.Equal(20, model.Upper);
    }

    [Fact]
    public void TextProperties_RejectInvalid( )
    {
        var model = new LotteryViewModel
        {
            MaxText = "abc"
        };
        Assert.Equal(51, model.Max);
        Assert.Equal("51", model.MaxText);

        model.CountText = "0";
        Assert.Equal(1, model.Count);
        Assert.Equal("1", model.CountText);
    }

    [Fact]
    public void MaxText_ClampsToLimit( )
    {
        var model = new LotteryViewModel
        {
            MaxText = "99999"
        };

        Assert.Equal(LotteryViewModel.MaxLimit, model.Max);
        Assert.Equal("9999", model.MaxText);
    }

    [Fact]
    public void DrawOne_UsesPool( )
    {
        var model = new LotteryViewModel( );
        model.SetLower(10);
        model.SetUpper(20);

        model.DrawOne( );

        var item = Assert.Single(model.Results);
        Assert.InRange(int.Parse(item, CultureInfo.InvariantCulture), 10, 20);
    }

    [Fact]
    public void DrawMany_ReturnsDistinctResults( )
    {
        var model = new LotteryViewModel( );
        model.SetCount(5);

        model.DrawMany( );

        Assert.Equal(5, model.Results.Count);
        Assert.Equal(5, model.Results.Distinct( ).Count( ));
    }

    [Fact]
    public void DrawMany_ClampsCount( )
    {
        var model = new LotteryViewModel( );
        model.SetLower(5);
        model.SetUpper(7);
        model.SetCount(9);

        Assert.Equal(3, model.Count);

        model.DrawMany( );

        Assert.Equal(3, model.Results.Count);
    }

    [Fact]
    public void Draws_AreIndependent( )
    {
        var model = new LotteryViewModel( );
        model.SetUpper(2);

        var values = new List<string>( );
        for (var index = 0; index < 30; index++)
        {
            model.DrawOne( );
            values.Add(model.Results[0]);
        }

        Assert.Equal(2, model.PoolSize);
        Assert.True(values.Distinct( ).Count( ) < values.Count);
    }

    [Fact]
    public void AddDataset_SelectsLast( )
    {
        var model = new LotteryViewModel( );

        model.AddDataset("甲", ["1", "2"]);
        model.AddDataset("乙", ["3"]);

        Assert.Equal(2, model.Datasets.Count);
        Assert.Equal("乙", model.SelectedDataset?.Name);
    }

    [Fact]
    public void AddDataset_UniqueNames( )
    {
        var model = new LotteryViewModel( );

        model.AddDataset("名单", ["甲"]);
        model.AddDataset("名单", ["乙"]);

        Assert.Equal(["名单", "名单 2"], model.Datasets.Select(dataset => dataset.Name));
    }

    [Fact]
    public void AddDataset_IgnoresEmpty( )
    {
        var model = new LotteryViewModel( );

        model.AddDataset("空", []);

        Assert.Empty(model.Datasets);
        Assert.Null(model.SelectedDataset);
    }

    [Fact]
    public void RemoveSelectedDataset_SelectsNeighbor( )
    {
        var model = new LotteryViewModel( );
        model.AddDataset("甲", ["1"]);
        model.AddDataset("乙", ["2"]);
        model.AddDataset("丙", ["3"]);
        model.SelectedDataset = model.Datasets[1];

        model.RemoveSelectedDataset( );
        Assert.Equal("丙", model.SelectedDataset?.Name);

        model.RemoveSelectedDataset( );
        Assert.Equal("甲", model.SelectedDataset?.Name);

        model.RemoveSelectedDataset( );
        Assert.Null(model.SelectedDataset);
    }

    [Fact]
    public void CollectionDraw_UsesDatasetItems( )
    {
        var model = new LotteryViewModel( );
        model.SetMode(LotteryMode.Collection);
        model.AddDataset("名单", ["张三", "李四"]);

        Assert.True(model.CanDraw);
        Assert.Equal(2, model.PoolSize);

        model.DrawOne( );

        var item = Assert.Single(model.Results);
        Assert.Contains(item, new[] { "张三", "李四" });
    }

    [Fact]
    public void CollectionDraw_KeepsDuplicates( )
    {
        var model = new LotteryViewModel( );
        model.SetMode(LotteryMode.Collection);

        model.AddDataset("名单", ["甲", "甲", "乙"]);

        Assert.Equal(3, model.PoolSize);
    }

    [Fact]
    public void Options_FollowDataset( )
    {
        var model = new LotteryViewModel( );
        model.SetMode(LotteryMode.Collection);
        model.AddDataset("名单", ["甲", "乙", "丙"]);

        Assert.Equal(["甲", "乙", "丙"], model.Options.Select(option => option.Text));
    }

    [Fact]
    public void NoRepeat_DrawsEachOptionOnce( )
    {
        var model = new LotteryViewModel( );
        model.SetMode(LotteryMode.Collection);
        model.AddDataset("名单", ["甲", "乙", "丙"]);
        model.NoRepeat = true;
        model.SetCount(2);

        model.DrawMany( );

        Assert.Equal(2, model.Results.Count);
        Assert.Equal(2, model.Options.Count(option => option.IsDrawn));
        Assert.Equal(1, model.PoolSize);

        model.DrawMany( );

        Assert.Single(model.Results);
        Assert.Equal(3, model.Options.Count(option => option.IsDrawn));
        Assert.Equal(0, model.PoolSize);
        Assert.False(model.CanDraw);
    }

    [Fact]
    public void ResetNoRepeat_ClearsMarksAndResults( )
    {
        var model = new LotteryViewModel( );
        model.SetMode(LotteryMode.Collection);
        model.AddDataset("名单", ["甲", "乙", "丙"]);
        model.NoRepeat = true;
        model.SetCount(2);

        model.DrawMany( );
        model.ResetNoRepeat( );

        Assert.All(model.Options, option => Assert.False(option.IsDrawn));
        Assert.Empty(model.Results);
        Assert.Equal(3, model.PoolSize);
        Assert.Equal(2, model.Count);
        Assert.True(model.CanDraw);
    }

    [Fact]
    public void NoRepeat_ToggleResetsProgress( )
    {
        var model = new LotteryViewModel( );
        model.SetMode(LotteryMode.Collection);
        model.AddDataset("名单", ["甲", "乙"]);
        model.NoRepeat = true;

        model.DrawOne( );
        Assert.Single(model.Options, option => option.IsDrawn);

        model.NoRepeat = false;
        model.NoRepeat = true;

        Assert.All(model.Options, option => Assert.False(option.IsDrawn));
        Assert.Equal(2, model.PoolSize);
        Assert.True(model.CanDraw);
    }

    [Fact]
    public void NoRepeat_DatasetChangeClearsMarks( )
    {
        var model = new LotteryViewModel( );
        model.SetMode(LotteryMode.Collection);
        model.AddDataset("甲", ["1", "2"]);
        model.NoRepeat = true;

        model.DrawOne( );
        model.AddDataset("乙", ["3"]);

        Assert.Equal("3", Assert.Single(model.Options).Text);
        Assert.False(model.Options[0].IsDrawn);
        Assert.Equal(1, model.PoolSize);
    }

    [Fact]
    public void ModeSwitch_ClearsResults( )
    {
        var model = new LotteryViewModel( );
        model.DrawOne( );
        Assert.Single(model.Results);

        model.SetMode(LotteryMode.Collection);

        Assert.Equal(LotteryMode.Collection, model.Mode);
        Assert.Empty(model.Results);
        Assert.Equal(1, model.Count);
        Assert.Equal(0, model.PoolSize);
        Assert.False(model.CanDraw);
    }

    [Fact]
    public void RangeChange_ClearsResults( )
    {
        var model = new LotteryViewModel( );
        model.DrawOne( );

        model.SetLower(2);
        Assert.Empty(model.Results);

        model.DrawOne( );
        model.SetUpper(50);
        Assert.Empty(model.Results);
    }

    [Fact]
    public void CountChange_KeepsResults( )
    {
        var model = new LotteryViewModel( );
        model.DrawMany( );
        var count = model.Results.Count;

        model.SetCount(3);

        Assert.Equal(count, model.Results.Count);
    }

    [Fact]
    public void ModeSwitch_KeepsSelectedDataset( )
    {
        var model = new LotteryViewModel( );
        model.SetMode(LotteryMode.Collection);
        model.AddDataset("名单", ["甲"]);

        model.SetMode(LotteryMode.Continuous);
        model.SetMode(LotteryMode.Collection);

        Assert.Equal("名单", model.SelectedDataset?.Name);
        Assert.Equal(1, model.PoolSize);
    }

    [Fact]
    public void CreateState_RestoresDatasets( )
    {
        var model = new LotteryViewModel( );
        model.SetMode(LotteryMode.Collection);
        model.AddDataset("甲", ["1", "2"]);
        model.AddDataset("乙", ["3"]);
        model.SelectedDataset = model.Datasets[0];

        var restored = new LotteryViewModel( );
        restored.SetMode(LotteryMode.Collection);
        restored.Restore(model.CreateState( ));

        Assert.Equal(2, restored.Datasets.Count);
        Assert.Equal("甲", restored.SelectedDataset?.Name);
        Assert.Equal(["1", "2"], restored.Options.Select(option => option.Text));
        Assert.Equal(2, restored.PoolSize);
    }

    [Fact]
    public void Restore_CountKeepsCollectionValue( )
    {
        var model = new LotteryViewModel( );
        model.SetMax(3);
        model.SetMode(LotteryMode.Collection);
        model.AddDataset("名单", ["1", "2", "3", "4", "5", "6", "7", "8", "9", "10"]);
        model.SetCount(8);

        var restored = new LotteryViewModel( );
        restored.Restore(model.CreateState( ));

        Assert.Equal(LotteryMode.Collection, restored.Mode);
        Assert.Equal(8, restored.Count);
    }

    [Fact]
    public void CreateState_RestoresNoRepeatRecord( )
    {
        var model = new LotteryViewModel( );
        model.SetMode(LotteryMode.Collection);
        model.AddDataset("名单", ["甲", "乙", "丙"]);
        model.NoRepeat = true;
        model.DrawOne( );

        var restored = new LotteryViewModel( );
        restored.Restore(model.CreateState( ));

        Assert.True(restored.NoRepeat);
        Assert.Equal(
            Assert.Single(model.Options, option => option.IsDrawn).Text,
            Assert.Single(restored.Options, option => option.IsDrawn).Text);
        Assert.Equal(2, restored.PoolSize);
    }

    [Fact]
    public void CreateState_KeepsContinuousSettings( )
    {
        var model = new LotteryViewModel( );
        model.SetMax(30);
        model.SetLower(5);
        model.SetUpper(20);
        model.SetCount(4);

        var restored = new LotteryViewModel( );
        restored.Restore(model.CreateState( ));

        Assert.Equal(30, restored.Max);
        Assert.Equal(5, restored.Lower);
        Assert.Equal(20, restored.Upper);
        Assert.Equal(4, restored.Count);
        Assert.True(restored.IsContinuous);
    }

    [Fact]
    public void Restore_EmptyState_LeavesNothing( )
    {
        var model = new LotteryViewModel( );

        model.Restore(new LotteryState( ));

        Assert.Empty(model.Datasets);
        Assert.Null(model.SelectedDataset);
    }
}
