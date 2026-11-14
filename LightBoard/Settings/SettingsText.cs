using System;
using System.ComponentModel;

namespace LightBoard;

/// <summary>
/// 文本项：未设置时显示程序默认值，输入后写入记录
/// </summary>
public sealed class SettingsText(
    string label,
    string key,
    Func<LightBoardSettings, string> effective,
    Func<LightBoardSettings, string?> raw,
    Func<LightBoardSettings, string?, LightBoardSettings> write) : ISettingItem
{
    private static readonly LightBoardSettings Defaults = new( );
    private string? entered;
    private string text = effective(Defaults);

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Label { get; } = label;

    public string Key { get; } = key;

    public bool IsSet => entered is not null;

    public bool HasErrors => false;

    public string Text
    {
        get => text;
        set
        {
            if (text == value)
            {
                return;
            }

            text = value;
            entered = value;
            Raise(nameof(Text));
            Raise(nameof(IsSet));
        }
    }

    public void Load(LightBoardSettings settings)
    {
        entered = raw(settings);
        text = entered ?? effective(settings);
        RaiseAll( );
    }

    public LightBoardSettings Apply(LightBoardSettings settings)
    {
        return write(settings, entered);
    }

    public void Clear( )
    {
        entered = null;
        text = effective(Defaults);
        RaiseAll( );
    }

    private void RaiseAll( )
    {
        Raise(nameof(Text));
        Raise(nameof(IsSet));
    }

    private void Raise(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
