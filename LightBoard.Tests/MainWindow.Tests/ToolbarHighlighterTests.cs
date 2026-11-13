using System.Windows.Ink;
using System.Windows.Media;

using InkCanvasNext;

namespace LightBoard.Tests.Toolbar;

[Collection("Toolbar")]
public class ToolbarHighlighterTests
{
    private static readonly Color Red = Color.FromRgb(0xFF, 0x43, 0x45);

    [Fact]
    public void Ink_EnterHighlighter_AppliesHighlighterProfile( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );

            ToolDriver.Toggle(host.Window.HighlighterToggle, true);

            Assert.True(host.Window.HighlighterActive);
            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);
            Assert.Equal(InkCanvasNextMode.Ink, host.Canvas.Mode);
            Assert.Equal(Colors.Yellow, host.Canvas.DefaultDrawingAttributes.Color);
            Assert.True(host.Canvas.DefaultDrawingAttributes.IsHighlighter);
            Assert.Equal(StylusTip.Ellipse, host.Canvas.DefaultDrawingAttributes.StylusTip);
            Assert.True(host.Canvas.DefaultDrawingAttributes.FitToCurve);
            Assert.True(host.Canvas.DefaultDrawingAttributes.IgnorePressure);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Fact]
    public void Ink_EnterExitHighlighter_RestoresPenAndRadios( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ToolDriver.ClickRadio(host, host.FindColor(Red));
            ToolDriver.ClickRadio(host, host.FindThickness(5));

            ToolDriver.Toggle(host.Window.HighlighterToggle, true);
            Assert.True(host.Window.HighlighterActive);
            ToolDriver.Toggle(host.Window.HighlighterToggle, false);

            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);
            Assert.Equal(Red, host.Canvas.DefaultDrawingAttributes.Color);
            Assert.Equal(5, host.Canvas.DefaultDrawingAttributes.Width);
            Assert.False(host.Canvas.DefaultDrawingAttributes.IsHighlighter);
            Assert.False(host.Window.HighlighterToggle.IsChecked);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Theory]
    [InlineData(InkCanvasNextMode.EraseStroke)]
    [InlineData(InkCanvasNextMode.EraseArea)]
    [InlineData(InkCanvasNextMode.Select)]
    public void Tool_EnterExitHighlighter_RestoresTool(InkCanvasNextMode tool)
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, tool);

            ToolDriver.Toggle(host.Window.HighlighterToggle, true);
            Assert.True(host.Window.HighlighterActive);
            ToolDriver.Toggle(host.Window.HighlighterToggle, false);

            Assert.Equal(tool, host.Window.Mode);
            Assert.Equal(tool, host.Canvas.Mode);
            Assert.False(host.Canvas.DefaultDrawingAttributes.IsHighlighter);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Theory]
    [InlineData(InkCanvasNextMode.Line)]
    [InlineData(InkCanvasNextMode.Circle)]
    public void Shape_EnterExitHighlighter_FallsBackToInk(InkCanvasNextMode shape)
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, shape);

            ToolDriver.Toggle(host.Window.HighlighterToggle, true);
            ToolDriver.Toggle(host.Window.HighlighterToggle, false);

            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);
            Assert.False(host.Window.LineRadio.IsChecked);
            Assert.False(host.Window.CircleRadio.IsChecked);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Fact]
    public void ReenterHighlighter_ThenExit_RestoresPreviousTool( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, InkCanvasNextMode.Select);

            ToolDriver.Toggle(host.Window.HighlighterToggle, true);
            ToolDriver.Toggle(host.Window.HighlighterToggle, true);
            ToolDriver.Toggle(host.Window.HighlighterToggle, false);

            Assert.Equal(InkCanvasNextMode.Select, host.Window.Mode);
            Assert.Equal(InkCanvasNextMode.Select, host.Canvas.Mode);
            Assert.False(host.Window.HighlighterToggle.IsChecked);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Fact]
    public void ReclickHighlighter_WhileActive_IsIdempotent( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ToolDriver.Toggle(host.Window.HighlighterToggle, true);

            ToolDriver.Toggle(host.Window.HighlighterToggle, true);

            Assert.True(host.Window.HighlighterActive);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Fact]
    public void Tool_EnterHighlighter_ThenThickness_AppliesWidthAndReturnsToInk( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, InkCanvasNextMode.Select);

            ToolDriver.Toggle(host.Window.HighlighterToggle, true);
            ToolDriver.ClickRadio(host, host.FindThickness(10));

            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);
            Assert.Equal(10, host.Canvas.DefaultDrawingAttributes.Width);
            Assert.False(host.Canvas.DefaultDrawingAttributes.IsHighlighter);
            ToolbarAssertions.AssertConsistent(host);
        });
    }
}