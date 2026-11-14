using System;
using System.ComponentModel;
using System.Threading;

namespace LightBoard;

/// <summary>
/// 两值单选：未设置时按程序默认值选中一侧，点选后写入记录
/// </summary>
public sealed class SettingsChoice(
    string label,
    string key,
    string firstLabel,
    string secondLabel,
    Func<LightBoardSettings, bool> effectiveFirst,
    Func<LightBoardSettings, bool?> rawFirst,
    Func<LightBoardSettings, bool?, LightBoardSettings> write) : ISettingItem
{
    private static readonly LightBoardSettings Defaults = new( );

    private static int lastGroup;
    private bool? first;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Label { get; } = label;

    public string Key { get; } = key;

    public string FirstLabel { get; } = firstLabel;

    public string SecondLabel { get; } = secondLabel;

    /// <summary>
    /// 单选分组名；每组独立，避免同分区的两组互相取消
    /// </summary>
    public string GroupName { get; } = $"SettingChoice{Interlocked.Increment(ref lastGroup)}";

    public bool IsSet => first is not null;

    public bool HasErrors => false;

    public bool First
    {
        get => first ?? effectiveFirst(Defaults);
        set
        {
            if (value)
            {
                Select(firstValue: true);
            }
        }
    }

    public bool Second
    {
        get => !First;
        set
        {
            if (value)
            {
                Select(firstValue: false);
            }
        }
    }

    public void Load(LightBoardSettings settings)
    {
        first = rawFirst(settings);
        RaiseAll( );
    }

    public LightBoardSettings Apply(LightBoardSettings settings)
    {
        return write(settings, first);
    }

    public void Clear( )
    {
        first = null;
        RaiseAll( );
    }

    private void Select(bool firstValue)
    {
        if (first == firstValue)
        {
            return;
        }

        first = firstValue;
        RaiseAll( );
    }

    private void RaiseAll( )
    {
        Raise(nameof(First));
        Raise(nameof(Second));
        Raise(nameof(IsSet));
    }

    private void Raise(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
