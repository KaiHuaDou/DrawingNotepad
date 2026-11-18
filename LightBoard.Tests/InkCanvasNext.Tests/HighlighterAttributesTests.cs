using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;

namespace InkCanvasNext.Tests;

/// <summary>
/// 高亮是笔触属性：写入高亮外观不改变编辑模式，颜色、宽度与笔尖原样保留
/// </summary>
public class HighlighterAttributesTests
{
    [Fact]
    public void HighlighterAttribute_KeepsColorWidthAndTip( )
    {
        StaTest.Run((Action) (( ) =>
        {
            using var host = new TouchHost( );
            var attributes = host.Canvas.DrawingAttributes.DrawingAttributes;
            attributes.Color = Colors.Red;
            attributes.Width = attributes.Height = 24;
            attributes.StylusTip = StylusTip.Rectangle;
            attributes.IsHighlighter = true;

            host.Canvas.DrawingAttributes = host.Canvas.DrawingAttributes with { DrawingAttributes = attributes };

            var tool = host.Canvas.DrawingAttributes;
            Assert.True(tool.DrawingAttributes.IsHighlighter);
            Assert.Equal(Colors.Red, tool.DrawingAttributes.Color);
            Assert.Equal(24, tool.DrawingAttributes.Width);
            Assert.Equal(StylusTip.Rectangle, tool.DrawingAttributes.StylusTip);
            Assert.Equal(InkCanvasEditingMode.Ink, (InkCanvasEditingMode) host.Canvas.InnerCanvas.EditingMode);
            Assert.Equal(InkCanvasNextMode.Ink, tool.Mode);
        }));
    }
}
