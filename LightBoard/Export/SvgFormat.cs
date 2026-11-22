using System.Globalization;
using System.Windows.Media;

namespace LightBoard;

/// <summary>
/// SVG 输出使用的数值与颜色写法
/// </summary>
internal static class SvgFormat
{
    /// <summary>
    /// 数值保留四位小数，小数点固定
    /// </summary>
    public static string Number(double value)
    {
        var text = value.ToString("0.####", CultureInfo.InvariantCulture);
        return text == "-0" ? "0" : text;
    }

    /// <summary>
    /// 颜色写成不带 alpha 的十六进制形式
    /// </summary>
    public static string Color(Color color)
    {
        return string.Create(CultureInfo.InvariantCulture, $"#{color.R:X2}{color.G:X2}{color.B:X2}");
    }
}
