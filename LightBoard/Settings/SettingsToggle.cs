using System;
using System.ComponentModel;

namespace LightBoard;

/// <summary>
/// 开关项：未设置时勾选态取程序默认值，点选后写入记录
/// </summary>
public sealed class SettingsToggle(
    string label,
    string key,
    Func<LightBoardSettings, bool> effective,
    Func<LightBoardSettings, bool?> raw,
    Func<LightBoardSettings, bool?, LightBoardSettings> write) : ISettingItem
{
    private static readonly LightBoardSettings Defaults = new( );
    private bool? value;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Label { get; } = label;

    public string Key { get; } = key;

    public bool IsSet => value is not null;

    public bool HasErrors => false;

    public bool Checked
    {
        get => value ?? effective(Defaults);
        set
        {
            if (Checked == value)
            {
                return;
            }

            this.value = value;
            Raise(nameof(Checked));
            Raise(nameof(IsSet));
        }
    }

    public void Load(LightBoardSettings settings)
    {
        value = raw(settings);
        RaiseAll( );
    }

    public LightBoardSettings Apply(LightBoardSettings settings)
    {
        return write(settings, value);
    }

    public void Clear( )
    {
        value = null;
        RaiseAll( );
    }

    private void RaiseAll( )
    {
        Raise(nameof(Checked));
        Raise(nameof(IsSet));
    }

    private void Raise(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
