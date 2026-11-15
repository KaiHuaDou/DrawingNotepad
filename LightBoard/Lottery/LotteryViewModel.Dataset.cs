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
    /// 装入存档：先恢复连续模式的数值，再装数据集与选中项，最后切模式
    /// </summary>
    internal void Restore(LotteryState state)
    {
        SetMax(state.Max);
        SetLower(state.Lower);
        SetUpper(state.Upper);
        SetCount(state.Count);

        foreach (var item in state.Datasets)
        {
            if (item.Items.Count > 0)
            {
                Datasets.Add(new LotteryDataset(UniqueName(item.Name), item.Items));
            }
        }

        SelectedDataset = Datasets.FirstOrDefault(dataset => dataset.Name == state.Selected) ?? Datasets.LastOrDefault( );
        SetMode(state.Mode);
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
            Count = Count,
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
