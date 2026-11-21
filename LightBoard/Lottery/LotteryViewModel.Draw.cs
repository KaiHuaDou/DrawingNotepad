using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace LightBoard;

// 抽选状态的抽选部分：取下标，连续模式换成数字，集合模式取元素
public sealed partial class LotteryViewModel
{
    /// <summary>
    /// 不重复：集合模式下已抽中的选项不再进入候选，并显示在结果下方的选项列表里
    /// </summary>
    public bool NoRepeat
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            ClearDrawRecord( );

            Raise(nameof(NoRepeat));
        }
    }

    public ObservableCollection<LotteryOption> Options { get; } = [];

    /// <summary>
    /// 清空不重复抽选的已抽记录并开始新一轮
    /// </summary>
    public void ResetNoRepeat( )
    {
        if (NoRepeat)
        {
            ClearDrawRecord( );
        }
    }

    public void DrawOne( )
    {
        Draw(1);
    }

    public void DrawMany( )
    {
        Draw(Count);
    }

    private void Draw(int drawCount)
    {
        var pool = PoolSize;
        if (pool == 0)
        {
            return;
        }

        Results.Clear( );

        if (Mode == LotteryMode.Continuous)
        {
            foreach (var index in LotteryDraw.PickIndices(Random.Shared, pool, drawCount))
            {
                Results.Add((Lower + index).ToString(CultureInfo.InvariantCulture));
            }

            return;
        }

        if (SelectedDataset is not { } dataset)
        {
            return;
        }

        if (NoRepeat)
        {
            DrawCollectionWithoutRepeat(drawCount);
            return;
        }

        foreach (var index in LotteryDraw.PickIndices(Random.Shared, dataset.Count, drawCount))
        {
            Results.Add(dataset.Items[index]);
        }
    }

    private void DrawCollectionWithoutRepeat(int drawCount)
    {
        List<LotteryOption> remaining = [.. Options.Where(option => !option.IsDrawn)];

        foreach (var index in LotteryDraw.PickIndices(Random.Shared, remaining.Count, drawCount))
        {
            var option = remaining[index];
            option.IsDrawn = true;
            Results.Add(option.Text);
        }

        // 抽选数量保留为用户设定值，候选不足时少抽；抽完由 CanDraw 关闭按钮
        RaisePool( );
        Raise(nameof(CanIncreaseCount));
    }

    /// <summary>
    /// 装入存档的已抽记录：越界下标忽略，候选与按钮状态跟着刷新
    /// </summary>
    internal void RestoreDrawRecord(IReadOnlyList<int> drawnIndices)
    {
        foreach (var index in drawnIndices)
        {
            if ((uint) index < (uint) Options.Count)
            {
                Options[index].IsDrawn = true;
            }
        }

        RaisePool( );
        Raise(nameof(CanIncreaseCount));
    }

    private void ClearDrawRecord( )
    {
        ResetDrawMarks( );
        Results.Clear( );

        RaisePool( );
        Raise(nameof(CanIncreaseCount));
    }

    private void RefreshOptions( )
    {
        Options.Clear( );

        if (SelectedDataset is { } dataset)
        {
            foreach (var item in dataset.Items)
            {
                Options.Add(new LotteryOption(item));
            }
        }
    }

    private void ResetDrawMarks( )
    {
        foreach (var option in Options)
        {
            option.IsDrawn = false;
        }
    }
}
