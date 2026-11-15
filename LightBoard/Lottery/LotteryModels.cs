using System.Collections.Generic;

namespace LightBoard;

public enum LotteryMode
{
    Continuous,
    Collection,
}

/// <summary>
/// 一个数据集：名称与全部元素，元素按导入顺序保留重复，可由窗口按文本整体改写
/// </summary>
public sealed class LotteryDataset(string name, IReadOnlyList<string> values)
{
    private readonly List<string> items = [.. values];

    public string Name { get; } = name;

    public IReadOnlyList<string> Items => items;

    public int Count => items.Count;

    internal void Replace(IReadOnlyList<string> values)
    {
        items.Clear( );
        items.AddRange(values);
    }
}
