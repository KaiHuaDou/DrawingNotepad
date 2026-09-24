using System.Windows.Media;

using InkCanvasNext;

namespace LightBoard.Tests.Toolbar;

[Collection("Toolbar")]
public class ToolbarColorTests
{
    private static readonly Color Red = Color.FromRgb(0xFF, 0x43, 0x45);

    [Theory]
    [InlineData(0x1E, 0x1E, 0x1E)]
    [InlineData(0xE6, 0xE6, 0xE6)]
    [InlineData(0xFF, 0x43, 0x45)]
    [InlineData(0xF9, 0x97, 0x39)]
    [InlineData(0xEC, 0xD2, 0x01)]
    [InlineData(0x91, 0x84, 0xEE)]
    [InlineData(0x2E, 0x7A, 0xE6)]
    [InlineData(0x4E, 0xC9, 0xB0)]
    public void Ink_ClickColor_UpdatesPenAndStaysInk(byte r, byte g, byte b)
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            var color = Color.FromRgb(r, g, b);

            ToolDriver.ClickRadio(host, host.FindColor(color));

            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);
            Assert.Equal(color, host.Canvas.DefaultDrawingAttributes.Color);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Fact]
    public void ReclickColor_IsIdempotent( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            var radio = host.FindColor(Red);

            ToolDriver.ClickRadio(host, radio);
            ToolDriver.ClickRadio(host, radio);

            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);
            Assert.Equal(Red, host.Canvas.DefaultDrawingAttributes.Color);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Theory]
    [InlineData(InkCanvasNextMode.EraseStroke)]
    [InlineData(InkCanvasNextMode.EraseArea)]
    [InlineData(InkCanvasNextMode.Select)]
    public void ToolMode_ClickColor_ReturnsToInk(InkCanvasNextMode tool)
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, tool);
            Assert.Equal(tool, host.Window.Mode);

            ToolDriver.ClickRadio(host, host.FindColor(Red));

            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);
            Assert.Equal(Red, host.Canvas.DefaultDrawingAttributes.Color);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Theory]
    [InlineData(InkCanvasNextMode.Line)]
    [InlineData(InkCanvasNextMode.Circle)]
    public void Shape_ClickColor_KeepsShapeAndUpdatesPen(InkCanvasNextMode shape)
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, shape);

            ToolDriver.ClickRadio(host, host.FindColor(Red));

            Assert.Equal(shape, host.Window.Mode);
            Assert.Equal(Red, host.Canvas.DefaultDrawingAttributes.Color);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Fact]
    public void Highlighter_ClickColor_ExitsHighlighterAndAppliesColor( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, InkCanvasNextMode.Highlighter);
            Assert.Equal(InkCanvasNextMode.Highlighter, host.Window.Mode);

            ToolDriver.ClickRadio(host, host.FindColor(Red));

            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);
            Assert.Equal(Red, host.Canvas.DefaultDrawingAttributes.Color);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Fact]
    public void SelectWithCloneStamp_ClickColor_ClearsStampAndReturnsToInk( )
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

            ToolDriver.ClickRadio(host, host.FindColor(Red));

            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);
            Assert.Equal(StampAction.None, canvas.StampAction);
            Assert.False(host.Window.CloneButton.IsChecked);
            ToolbarAssertions.AssertConsistent(host);
        });
    }
}