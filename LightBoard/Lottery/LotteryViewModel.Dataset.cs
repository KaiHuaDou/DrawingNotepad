using System;
using System.Collections.Generic;
using System.Linq;

namespace LightBoard;

// 抽选状态的数据集部分：导入、增删、重名处理与存档
public sealed partial class LotteryViewModel
{
    public void AddDataset(string name, IReadOnlyList<string> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        var dataset = new LotteryDataset(UniqueName(name), items);
        Datasets.Add(dataset);
        SelectedDataset = dataset;
    }

    /// <summary>
    /// 按逗号或换行拆分，去掉每项首尾空白与空项
    /// </summary>
    internal static string[] SplitItems(string text)
    {
        return text.Split([',', '，', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public void RemoveSelectedDataset( )
    {
        if (SelectedDataset is not { } dataset)
        {
            return;
        }

        var index = Datasets.IndexOf(dataset);
        if (index < 0)
        {
            return;
        }

        Datasets.RemoveAt(index);
        SelectedDataset = Datasets.Count == 0 ? null : Datasets[Math.Min(index, Datasets.Count - 1)];
    }

    /// <summary>
    /// 装入存档：先恢复连续模式的数值与数据集，再切模式，最后恢复抽选数量
    /// </summary>
    internal void Restore(LotteryState state)
    {
        Max = state.Max;
        Lower = state.Lower;
        Upper = state.Upper;

        foreach (var item in state.Datasets)
        {
            if (item.Items.Count > 0)
            {
                Datasets.Add(new LotteryDataset(UniqueName(item.Name), item.Items));
            }
        }

        SelectedDataset = Datasets.FirstOrDefault(dataset => dataset.Name == state.Selected) ?? Datasets.LastOrDefault( );

        // 两种模式的数量先就位，切模式时才有可取的设定
        continuousCount = state.ContinuousCount;
        collectionCount = state.CollectionCount;

        Mode = state.Mode;

        // 数量最后恢复：候选集合由模式与数据集决定，先恢复会被连续模式的范围夹小
        Count = Mode == LotteryMode.Continuous ? continuousCount : collectionCount;

        NoRepeat = state.NoRepeat;
        if (state.NoRepeat && state.Selected is not null && SelectedDataset?.Name == state.Selected)
        {
            RestoreDrawRecord(state.DrawnIndices);
        }
    }

    /// <summary>
    /// 存档用的快照
    /// </summary>
    internal LotteryState CreateState( )
    {
        return new( )
        {
            Selected = SelectedDataset?.Name,
            Mode = Mode,
            Max = Max,
            Lower = Lower,
            Upper = Upper,
            ContinuousCount = continuousCount,
            CollectionCount = collectionCount,
            NoRepeat = NoRepeat,
            DrawnIndices = [.. Options.Select((option, index) => (option, index))
                .Where(pair => pair.option.IsDrawn)
                .Select(pair => pair.index)],
            Datasets = [.. Datasets.Select(dataset => new LotteryStateDataset
            {
                Name = dataset.Name,
                Items = [.. dataset.Items],
            })],
        };
    }

    private string UniqueName(string name)
    {
        var candidate = name;

        for (var suffix = 2; Datasets.Any(dataset => dataset.Name == candidate); suffix++)
        {
            candidate = $"{name} {suffix}";
        }

        return candidate;
    }
}
