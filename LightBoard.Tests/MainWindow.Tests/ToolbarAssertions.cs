using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;

using InkCanvasNext;

namespace LightBoard.Tests.Toolbar;

/// <summary>
/// 工具状态不变量：工具值、画布属性与全部控件勾选必须一致
/// </summary>
internal static class ToolbarAssertions
{
    /// <summary>stamp 非空时跳过印章清理断言（供印章激活态用例使用）</summary>
    public static void AssertConsistent(MainWindowHost host, StampAction? stamp = null)
    {
        var window = host.Window;
        var canvas = host.Canvas;
        var tool = canvas.DrawingAttributes;
        var attrs = tool.DrawingAttributes;

        if (attrs.IsHighlighter)
        {
            Assert.True(window.HighlighterToggle.IsChecked);
            Assert.Equal(InkCanvasNextMode.Ink, tool.Mode);
            Assert.Equal(Colors.Yellow, attrs.Color);
            Assert.Equal(36, attrs.Width);
            Assert.Equal(36, attrs.Height);
            Assert.True(attrs.IsHighlighter);
            Assert.Equal(StylusTip.Ellipse, attrs.StylusTip);
            Assert.True(attrs.IgnorePressure);
            AssertToolbarRadiosUnchecked(host);
        }
        else
        {
            Assert.False(window.HighlighterToggle.IsChecked);
            Assert.False(attrs.IsHighlighter);
            Assert.Equal(StylusTip.Ellipse, attrs.StylusTip);
            Assert.False(attrs.IgnorePressure);

            // 印章激活期间画笔单选与工具模式同样不勾选
            if (IsToolMode(tool.Mode) || stamp is not null and not StampAction.None)
            {
                Assert.True(host.ColorRadios.All(r => r.IsChecked == false));
                Assert.True(host.ThicknessRadios.All(r => r.IsChecked == false));
            }
            else
            {
                var colorChecked = host.ColorRadios.Where(r => r.IsChecked == true).ToList( );
                var thicknessChecked = host.ThicknessRadios.Where(r => r.IsChecked == true).ToList( );
                var colorRadio = Assert.Single(colorChecked);
                var thicknessRadio = Assert.Single(thicknessChecked);
                Assert.Equal(GetColor(colorRadio), attrs.Color);
                Assert.Equal(thicknessRadio.MinWidth, attrs.Width);
                Assert.Equal(thicknessRadio.MinWidth, attrs.Height);
            }

            Assert.Equal(tool.Mode == InkCanvasNextMode.Line, window.LineRadio.IsChecked == true);
            Assert.Equal(tool.Mode == InkCanvasNextMode.Circle, window.CircleRadio.IsChecked == true);
            Assert.Equal(tool.Mode == InkCanvasNextMode.EraseStroke, window.EraseStrokeRadio.IsChecked == true);
            Assert.Equal(tool.Mode == InkCanvasNextMode.EraseArea, window.EraseAreaRadio.IsChecked == true);
            Assert.Equal(tool.Mode == InkCanvasNextMode.Select, window.SelectRadio.IsChecked == true);
        }

        if (stamp is null)
        {
            Assert.Equal(StampAction.None, tool.Stamp);
            Assert.False(window.CloneButton.IsChecked);
            Assert.False(window.PasteButton.IsChecked);
        }
    }

    private static bool IsToolMode(InkCanvasNextMode mode)
    {
        return mode is InkCanvasNextMode.EraseStroke or InkCanvasNextMode.EraseArea or InkCanvasNextMode.Select;
    }

    private static void AssertToolbarRadiosUnchecked(MainWindowHost host)
    {
        Assert.True(host.ColorRadios.All(r => r.IsChecked == false));
        Assert.True(host.ThicknessRadios.All(r => r.IsChecked == false));
        Assert.False(host.Window.EraseStrokeRadio.IsChecked);
        Assert.False(host.Window.EraseAreaRadio.IsChecked);
        Assert.False(host.Window.SelectRadio.IsChecked);
        Assert.False(host.Window.LineRadio.IsChecked);
        Assert.False(host.Window.CircleRadio.IsChecked);
    }

    private static Color GetColor(RadioButton radio)
    {
        return ((SolidColorBrush) radio.Background).Color;
    }
}
