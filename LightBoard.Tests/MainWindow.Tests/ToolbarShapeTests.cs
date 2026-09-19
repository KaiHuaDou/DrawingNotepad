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
            Assert.False(host.Window.HighLighterToggle.IsChecked);
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