using InkCanvasNext;

namespace LightBoard.Tests.Toolbar;

[Collection("Toolbar")]
public class ToolbarThicknessTests
{
    [Theory]
    [InlineData(1.0)]
    [InlineData(3.0)]
    [InlineData(5.0)]
    [InlineData(10.0)]
    public void Ink_ClickThickness_UpdatesPenAndStaysInk(double width)
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );

            ToolDriver.ClickRadio(host, host.FindThickness(width));

            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);
            Assert.Equal(width, host.Canvas.DefaultDrawingAttributes.Width);
            Assert.Equal(width, host.Canvas.DefaultDrawingAttributes.Height);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Fact]
    public void ReclickThickness_IsIdempotent( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            var radio = host.FindThickness(5);

            ToolDriver.ClickRadio(host, radio);
            ToolDriver.ClickRadio(host, radio);

            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);
            Assert.Equal(5, host.Canvas.DefaultDrawingAttributes.Width);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Theory]
    [InlineData(InkCanvasNextMode.EraseStroke)]
    [InlineData(InkCanvasNextMode.EraseArea)]
    [InlineData(InkCanvasNextMode.Select)]
    public void ToolMode_ClickThickness_ReturnsToInk(InkCanvasNextMode tool)
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, tool);

            ToolDriver.ClickRadio(host, host.FindThickness(5));

            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);
            Assert.Equal(5, host.Canvas.DefaultDrawingAttributes.Width);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Theory]
    [InlineData(InkCanvasNextMode.Line)]
    [InlineData(InkCanvasNextMode.Circle)]
    public void Shape_ClickThickness_KeepsShapeAndUpdatesPen(InkCanvasNextMode shape)
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, shape);

            ToolDriver.ClickRadio(host, host.FindThickness(10));

            Assert.Equal(shape, host.Window.Mode);
            Assert.Equal(10, host.Canvas.DefaultDrawingAttributes.Width);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Fact]
    public void Highlighter_ClickThickness_ExitsHighlighterAndAppliesWidth( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, InkCanvasNextMode.Highlighter);

            ToolDriver.ClickRadio(host, host.FindThickness(5));

            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);
            Assert.Equal(5, host.Canvas.DefaultDrawingAttributes.Width);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Fact]
    public void SelectWithCloneStamp_ClickThickness_ClearsStampAndReturnsToInk( )
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

            ToolDriver.ClickRadio(host, host.FindThickness(10));

            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);
            Assert.Equal(StampAction.None, canvas.StampAction);
            Assert.False(host.Window.CloneButton.IsChecked);
            ToolbarAssertions.AssertConsistent(host);
        });
    }
}