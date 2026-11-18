using System.Collections.Generic;
using System.ComponentModel;

namespace LightBoard;

public enum LotteryMode
{
    Continuous,
    Collection,
}

/// <summary>
/// 一个数据集：名称与全部元素，元素按导入顺序保留重复
/// </summary>
public sealed class LotteryDataset(string name, IReadOnlyList<string> values)
{
    private readonly List<string> items = [.. values];

    public string Name { get; } = name;

    public IReadOnlyList<string> Items => items;

    public int Count => items.Count;
}

/// <summary>
/// 不重复模式下的一个选项：文本与是否已被抽中，抽中后由视图显示为灰
/// </summary>
public sealed class LotteryOption(string text) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Text { get; } = text;

    public bool IsDrawn
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDrawn)));
        }
    }
}
