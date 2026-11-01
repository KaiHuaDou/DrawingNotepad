using System;
using System.Collections;
using System.ComponentModel;
using System.Globalization;

namespace LightBoard;

/// <summary>
/// 设置窗口的一个数值项：标签、单位、范围、步进，以及从设置记录取值与写回
/// </summary>
public sealed class SettingsField(
    string label,
    string unit,
    double min,
    double max,
    double step,
    bool isInteger,
    Func<LightBoardSettings, double> get,
    Func<LightBoardSettings, double, LightBoardSettings> set)
    : INotifyPropertyChanged, INotifyDataErrorInfo
{
    public string Label { get; } = label;

    public string Unit { get; } = unit;

    /// <summary>
    /// 悬停提示：范围与默认值
    /// </summary>
    public string Hint => $"{Format(min)} ~ {Format(max)}，默认 {Format(get(new LightBoardSettings( )))}";

    public string Text
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Text)));
        }
    } = "";

    public bool HasErrors => !TryParse(out _);

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public IEnumerable GetErrors(string? propertyName)
    {
        string[] errors = HasErrors && propertyName == nameof(Text) ? [$"应为 {Format(min)} ~ {Format(max)} 的数值"] : [];
        return errors;
    }

    public void Load(LightBoardSettings settings)
    {
        Text = Format(get(settings));
    }

    public LightBoardSettings Apply(LightBoardSettings settings)
    {
        return TryParse(out var value) ? set(settings, value) : settings;
    }

    /// <summary>
    /// 按步进增减，越界时贴到边界；文本无效时从默认值起算
    /// </summary>
    public void Step(int direction)
    {
        var current = TryParse(out var value) ? value : get(new LightBoardSettings( ));
        Text = Format(Math.Clamp(Math.Round(current + step * direction, 4), min, max));
    }

    private static string Format(double value)
    {
        return value.ToString("0.####", CultureInfo.InvariantCulture);
    }

    private bool TryParse(out double value)
    {
        if (isInteger)
        {
            var parsed = int.TryParse(Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number);
            value = number;
            return parsed && number >= min && number <= max;
        }

        var ok = double.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var real);
        value = real;
        return ok && real >= min && real <= max;
    }
}
