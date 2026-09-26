using System.Windows.Controls;
using System.Windows.Media;

using InkCanvasNext;

namespace LightBoard.Tests.Toolbar;

/// <summary>
/// 状态机一致性不变量：mode、画布模式、DrawingAttributes、全部控件勾选与 StampAction 必须同构。
/// </summary>
internal static class ToolbarAssertions
{
    /// <summary>stamp 非空时跳过印章清理断言（供印章激活态用例使用）。</summary>
    public static void AssertConsistent(MainWindowHost host, StampAction? stamp = null)
    {
        var window = host.Window;
        var canvas = window.CanvasNext;
        var mode = window.Mode;
        var attrs = canvas.DefaultDrawingAttributes;

        if (mode == InkCanvasNextMode.Highlighter)
        {
            Assert.True(window.HighlighterToggle.IsChecked);
            Assert.Equal(InkCanvasNextMode.Ink, canvas.Mode);
            Assert.Equal(Colors.Yellow, attrs.Color);
            Assert.Equal(36, attrs.Width);
            Assert.Equal(36, attrs.Height);
            Assert.True(attrs.IsHighlighter);
            AssertToolbarRadiosUnchecked(host);
        }
        else
        {
            Assert.False(window.HighlighterToggle.IsChecked);
            Assert.Equal(mode, canvas.Mode);
            Assert.False(attrs.IsHighlighter);

            if (IsToolMode(mode))
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

            Assert.Equal(mode == InkCanvasNextMode.Line, window.LineRadio.IsChecked == true);
            Assert.Equal(mode == InkCanvasNextMode.Circle, window.CircleRadio.IsChecked == true);
            Assert.Equal(mode == InkCanvasNextMode.EraseStroke, window.EraseStrokeRadio.IsChecked == true);
            Assert.Equal(mode == InkCanvasNextMode.EraseArea, window.EraseAreaRadio.IsChecked == true);
            Assert.Equal(mode == InkCanvasNextMode.Select, window.SelectRadio.IsChecked == true);
        }

        if (stamp is null)
        {
            Assert.Equal(StampAction.None, canvas.StampAction);
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
