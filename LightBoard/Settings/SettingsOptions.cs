using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;

namespace LightBoard;

/// <summary>
/// 多值单选：未设置时按程序默认值选中一项，点选后写入记录
/// </summary>
public sealed class SettingsOptions : ISettingItem
{
    private static readonly LightBoardSettings Defaults = new( );
    private static int lastGroup;

    private readonly Func<LightBoardSettings, int> effectiveIndex;
    private readonly Func<LightBoardSettings, int?> rawIndex;
    private readonly Func<LightBoardSettings, int?, LightBoardSettings> write;
    private int? index;

    public SettingsOptions(
        string label,
        string key,
        IReadOnlyList<string> labels,
        Func<LightBoardSettings, int> effectiveIndex,
        Func<LightBoardSettings, int?> rawIndex,
        Func<LightBoardSettings, int?, LightBoardSettings> write)
    {
        Label = label;
        Key = key;
        this.effectiveIndex = effectiveIndex;
        this.rawIndex = rawIndex;
        this.write = write;
        GroupName = $"SettingOptions{Interlocked.Increment(ref lastGroup)}";
        Options = [.. labels.Select((text, value) => new SettingsOption(this, value, text))];
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Label { get; }

    public string Key { get; }

    /// <summary>
    /// 单选分组名；每组独立，避免同分区的多组互相取消
    /// </summary>
    public string GroupName { get; }

    public IReadOnlyList<SettingsOption> Options { get; }

    public bool IsSet => index is not null;

    public bool HasErrors => false;

    public void Load(LightBoardSettings settings)
    {
        index = rawIndex(settings);
        RaiseAll( );
    }

    public LightBoardSettings Apply(LightBoardSettings settings)
    {
        return write(settings, index);
    }

    public void Clear( )
    {
        index = null;
        RaiseAll( );
    }

    internal bool IsSelected(int value)
    {
        return (index ?? effectiveIndex(Defaults)) == value;
    }

    internal void Select(int value)
    {
        if (index == value)
        {
            return;
        }

        index = value;
        RaiseAll( );
    }

    private void RaiseAll( )
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSet)));
        foreach (var option in Options)
        {
            option.RaiseIsSelected( );
        }
    }
}

/// <summary>
/// 多值单选的一个选项
/// </summary>
public sealed class SettingsOption(SettingsOptions owner, int index, string label) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Label { get; } = label;

    public string GroupName => owner.GroupName;

    public bool IsSelected
    {
        get => owner.IsSelected(index);
        set
        {
            if (value)
            {
                owner.Select(index);
            }
        }
    }

    internal void RaiseIsSelected( )
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
    }
}
