using System.Windows.Media;

using InkCanvasNext;

namespace LightBoard.Tests.Toolbar;

[Collection("Toolbar")]
public class ToolbarShapeTests
{
    [Fact]
    public void Ink_LineRadio_ActivatesLine( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );

            ToolDriver.ClickRadio(host, host.Window.LineRadio);

            Assert.Equal(InkCanvasNextMode.Line, host.Window.Mode);
            Assert.True(host.Window.LineRadio.IsChecked);
            Assert.False(host.Window.CircleRadio.IsChecked);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Fact]
    public void Ink_CircleRadio_ActivatesCircle( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );

            ToolDriver.ClickRadio(host, host.Window.CircleRadio);

            Assert.Equal(InkCanvasNextMode.Circle, host.Window.Mode);
            Assert.True(host.Window.CircleRadio.IsChecked);
            Assert.False(host.Window.LineRadio.IsChecked);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Fact]
    public void LineThenCircle_TogglesExclusively( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, InkCanvasNextMode.Line);

            ToolDriver.ClickRadio(host, host.Window.CircleRadio);

            Assert.Equal(InkCanvasNextMode.Circle, host.Window.Mode);
            Assert.False(host.Window.LineRadio.IsChecked);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Fact]
    public void CircleThenLine_TogglesExclusively( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, InkCanvasNextMode.Circle);

            ToolDriver.ClickRadio(host, host.Window.LineRadio);

            Assert.Equal(InkCanvasNextMode.Line, host.Window.Mode);
            Assert.False(host.Window.CircleRadio.IsChecked);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Theory]
    [InlineData(InkCanvasNextMode.Line)]
    [InlineData(InkCanvasNextMode.Circle)]
    public void ReclickShape_ReturnsToInkAndReactivates(InkCanvasNextMode shape)
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, shape);
            Assert.Equal(shape, host.Window.Mode);

            var radio = shape == InkCanvasNextMode.Line ? host.Window.LineRadio : host.Window.CircleRadio;
            ToolDriver.ClickRadio(host, radio);
            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);
            Assert.Equal(InkCanvasNextMode.Ink, host.Canvas.Mode);
            ToolbarAssertions.AssertConsistent(host);

            ToolDriver.ClickRadio(host, radio);
            Assert.Equal(shape, host.Window.Mode);
            Assert.Equal(shape, host.Canvas.Mode);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    // 形状模式下点当前已选中的颜色/粗细即取消形状；点其他选项保持形状（由
    // Shape_ClickColor/Thickness_KeepsShapeAndUpdatesPen 防护，其目标相对默认当前项为非当前）。
    [Theory]
    [InlineData(InkCanvasNextMode.Line)]
    [InlineData(InkCanvasNextMode.Circle)]
    public void Shape_ReclickCurrentColor_ReturnsToInk(InkCanvasNextMode shape)
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            var purple = Color.FromRgb(0x91, 0x84, 0xEE);

            // 先点紫色建立"当前颜色"，再进形状模式
            ToolDriver.ClickRadio(host, host.FindColor(purple));
            ToolEntry.Enter(host, shape);
            Assert.Equal(shape, host.Window.Mode);

            ToolDriver.ClickRadio(host, host.FindColor(purple));
            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);
            Assert.Equal(InkCanvasNextMode.Ink, host.Canvas.Mode);
            Assert.Equal(purple, host.Canvas.DefaultDrawingAttributes.Color);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Theory]
    [InlineData(InkCanvasNextMode.Line)]
    [InlineData(InkCanvasNextMode.Circle)]
    public void Shape_ReclickCurrentThickness_ReturnsToInk(InkCanvasNextMode shape)
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );

            ToolDriver.ClickRadio(host, host.FindThickness(5));
            ToolEntry.Enter(host, shape);
            Assert.Equal(shape, host.Window.Mode);

            ToolDriver.ClickRadio(host, host.FindThickness(5));
            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);
            Assert.Equal(InkCanvasNextMode.Ink, host.Canvas.Mode);
            Assert.Equal(5, host.Canvas.DefaultDrawingAttributes.Width);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Theory]
    [InlineData(InkCanvasNextMode.EraseStroke)]
    [InlineData(InkCanvasNextMode.EraseArea)]
    [InlineData(InkCanvasNextMode.Select)]
    public void ToolMode_LineRadio_OverridesWithoutGoingInk(InkCanvasNextMode tool)
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, tool);
            Assert.Equal(tool, host.Window.Mode);

            ToolDriver.ClickRadio(host, host.Window.LineRadio);

            Assert.Equal(InkCanvasNextMode.Line, host.Window.Mode);
            Assert.Equal(InkCanvasNextMode.Line, host.Canvas.Mode);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Fact]
    public void Shape_EnterTool_Overrides( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, InkCanvasNextMode.Line);
            Assert.Equal(InkCanvasNextMode.Line, host.Window.Mode);

            foreach (var tool in new[]
            {
                InkCanvasNextMode.EraseStroke,
                InkCanvasNextMode.EraseArea,
                InkCanvasNextMode.Select,
            })
            {
                ToolEntry.Enter(host, tool);
                Assert.Equal(tool, host.Window.Mode);
                Assert.Equal(tool, host.Canvas.Mode);
                ToolbarAssertions.AssertConsistent(host);
            }
        });
    }

    [Fact]
    public void Highlighter_LineRadio_ExitsHighlighterEntersLine( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, InkCanvasNextMode.Highlighter);
            Assert.Equal(InkCanvasNextMode.Highlighter, host.Window.Mode);

            ToolDriver.ClickRadio(host, host.Window.LineRadio);

            Assert.Equal(InkCanvasNextMode.Line, host.Window.Mode);
            Assert.False(host.Window.HighlighterToggle.IsChecked);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Fact]
    public void SelectWithCloneStamp_LineRadio_ClearsStamp( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, InkCanvasNextMode.Select);
            var canvas = host.Canvas;
            canvas.Strokes.Add(TestStrokes.MakeStroke( ));
            TestSelection.ForceSelection(canvas, canvas.Strokes);
            ToolDriver.Toggle(host.Window.CloneButton, true);
            Assert.Equal(StampAction.Clone, canvas.StampAction);

            ToolDriver.ClickRadio(host, host.Window.LineRadio);

            Assert.Equal(InkCanvasNextMode.Line, host.Window.Mode);
            Assert.Equal(StampAction.None, canvas.StampAction);
            Assert.False(host.Window.CloneButton.IsChecked);
            ToolbarAssertions.AssertConsistent(host);
        });
    }
}