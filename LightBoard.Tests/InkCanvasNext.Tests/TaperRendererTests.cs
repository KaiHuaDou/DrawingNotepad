using System.Windows.Media;

namespace InkCanvasNext.Tests;

/// <summary>
/// 在绘笔画由哪个渲染器显示：笔锋可用时由 TaperDynamicRenderer 接管（鼠标、触摸、笔同一条插件通道），
/// 荧光笔等忽略压力或半透明的笔回退原生渲染器
/// </summary>
public class TaperRendererTests
{
    [Fact]
    public void Pen_UsesTaperRendererWithTailLayer( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var inner = host.Canvas.InnerCanvas;

            Assert.False(inner.UsesNativeRenderer);
            Assert.NotNull(inner.TaperRenderer);
            Assert.True(inner.TaperRenderer!.HasTailLayer);
        });
    }

    [Fact]
    public void Highlighter_UsesNativeRenderer( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var inner = host.Canvas.InnerCanvas;

            inner.DefaultDrawingAttributes.IsHighlighter = true;
            inner.DefaultDrawingAttributes.IgnorePressure = true;

            Assert.True(inner.UsesNativeRenderer);
        });
    }

    [Fact]
    public void TranslucentColor_UsesNativeRenderer( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var inner = host.Canvas.InnerCanvas;

            // 半透明笔身的报文重叠处会叠色，交给原生渲染器统一处理
            inner.DefaultDrawingAttributes.Color = Color.FromArgb(128, 0, 0, 0);

            Assert.True(inner.UsesNativeRenderer);
        });
    }
}
