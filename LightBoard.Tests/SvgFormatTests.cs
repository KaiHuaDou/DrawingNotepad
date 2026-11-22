using System.Globalization;
using System.Windows.Media;

namespace LightBoard.Tests;

public class SvgFormatTests
{
    [Fact]
    public void Number_DifferentCulture_KeepsDecimalPoint( )
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            Assert.Equal("1.25", SvgFormat.Number(1.25));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void Number_RoundsToFourDecimals( )
    {
        Assert.Equal("1.2346", SvgFormat.Number(1.23456));
    }

    [Fact]
    public void Number_NegativeZero_WritesZero( )
    {
        Assert.Equal("0", SvgFormat.Number(-0.00001));
    }

    [Fact]
    public void Color_WithAlpha_DropsAlpha( )
    {
        Assert.Equal("#2040C0", SvgFormat.Color(Color.FromArgb(0x60, 0x20, 0x40, 0xC0)));
    }
}
