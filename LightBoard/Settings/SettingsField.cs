using System;
using System.Collections;
using System.ComponentModel;
using System.Globalization;

namespace LightBoard;

/// <summary>
/// 数值项：单位、范围、步进，未设置时显示程序默认值
/// </summary>
public sealed class SettingsField(
    string label,
    string key,
    string unit,
    double min,
    double max,
    double step,
    bool isInteger,
    Func<LightBoardSettings, double> effective,
    Func<LightBoardSettings, double?> raw,
    Func<LightBoardSettings, double?, LightBoardSettings> write) : INotifyPropertyChanged, INotifyDataErrorInfo, ISettingItem
{
    private static readonly LightBoardSettings Defaults = new( );
    private double? value;
    private string text = Format(effective(Defaults));

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public string Label { get; } = label;

    public string Key { get; } = key;

    public string Unit { get; } = unit;

    /// <summary>
    /// 悬停提示：范围与默认值
    /// </summary>
    public string Hint => $"{Format(min)} ~ {Format(max)}，默认 {Format(effective(Defaults))}";

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
            Raise(nameof(Text));

            if (TryParse(text, out var parsed))
            {
                this.value = parsed;
                Raise(nameof(IsSet));
            }

            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Text)));
        }
    }

    public bool IsSet => value is not null;

    public bool HasErrors => !TryParse(text, out _);

    public IEnumerable GetErrors(string? propertyName)
    {
        return (string[]) (HasErrors && propertyName == nameof(Text) ? [$"应为 {Format(min)} ~ {Format(max)} 的数值"] : []);
    }

    public void Load(LightBoardSettings settings)
    {
        value = raw(settings);
        text = Format(value ?? effective(settings));
        RaiseAll( );
    }

    public LightBoardSettings Apply(LightBoardSettings settings)
    {
        return write(settings, value);
    }

    public void Clear( )
    {
        value = null;
        text = Format(effective(Defaults));
        RaiseAll( );
    }

    /// <summary>
    /// 按步进增减，越界时贴到边界；文本无效时从有效值起算
    /// </summary>
    public void Step(int direction)
    {
        var current = TryParse(text, out var parsed) ? parsed : value ?? effective(Defaults);
        var next = Math.Clamp(Math.Round(current + step * direction, 4), min, max);

        value = next;
        text = Format(next);
        RaiseAll( );
    }

    private static string Format(double value)
    {
        return value.ToString("0.####", CultureInfo.InvariantCulture);
    }

    private bool TryParse(string candidate, out double parsed)
    {
        if (isInteger)
        {
            var ok = int.TryParse(candidate, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number);
            parsed = number;
            return ok && number >= min && number <= max;
        }

        var valid = double.TryParse(candidate, NumberStyles.Float, CultureInfo.InvariantCulture, out var real);
        parsed = real;
        return valid && real >= min && real <= max;
    }

    private void RaiseAll( )
    {
        Raise(nameof(Text));
        Raise(nameof(IsSet));
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Text)));
    }

    private void Raise(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
