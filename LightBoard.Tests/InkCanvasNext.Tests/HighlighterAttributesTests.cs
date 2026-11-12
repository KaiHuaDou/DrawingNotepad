using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;

using LightBoard;

namespace InkCanvasNext.Tests;

/// <summary>
/// 高亮是笔属性：设置 IsHighlighter 不改变编辑模式，也不改变颜色、宽度与笔尖
/// </summary>
public class HighlighterAttributesTests
{
    [Fact]
    public void HighlighterAttribute_KeepsEditingMode( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            host.Canvas.DefaultDrawingAttributes.IsHighlighter = true;

            Assert.Equal(InkCanvasEditingMode.Ink, host.Canvas.InnerCanvasElement.EditingMode);
            Assert.Equal(InkCanvasNextMode.Ink, host.Canvas.Mode);
        });
    }

    [Fact]
    public void HighlighterAttribute_KeepsColorWidthAndTip( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var attrs = host.Canvas.DefaultDrawingAttributes;
            attrs.Color = Colors.Red;
            attrs.Width = attrs.Height = 24;
            attrs.StylusTip = StylusTip.Rectangle;

            attrs.IsHighlighter = true;

            Assert.True(attrs.IsHighlighter);
            Assert.Equal(Colors.Red, attrs.Color);
            Assert.Equal(24, attrs.Width);
            Assert.Equal(StylusTip.Rectangle, attrs.StylusTip);
            Assert.Equal(InkCanvasEditingMode.Ink, host.Canvas.InnerCanvasElement.EditingMode);
        });
    }
}
