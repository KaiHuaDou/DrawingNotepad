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

            Assert.True(host.Canvas.DrawingAttributes.DrawingAttributes.IsHighlighter);
            Assert.Equal(InkCanvasNextMode.Ink, host.Canvas.DrawingAttributes.Mode);
            Assert.Equal(Colors.Yellow, host.Canvas.DrawingAttributes.DrawingAttributes.Color);
            Assert.True(host.Canvas.DrawingAttributes.DrawingAttributes.IsHighlighter);
            Assert.Equal(StylusTip.Ellipse, host.Canvas.DrawingAttributes.DrawingAttributes.StylusTip);
            Assert.True(host.Canvas.DrawingAttributes.DrawingAttributes.IgnorePressure);
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
            Assert.True(host.Canvas.DrawingAttributes.DrawingAttributes.IsHighlighter);
            ToolDriver.Toggle(host.Window.HighlighterToggle, false);

            Assert.Equal(InkCanvasNextMode.Ink, host.Canvas.DrawingAttributes.Mode);
            Assert.Equal(Red, host.Canvas.DrawingAttributes.DrawingAttributes.Color);
            Assert.Equal(5, host.Canvas.DrawingAttributes.DrawingAttributes.Width);
            Assert.False(host.Canvas.DrawingAttributes.DrawingAttributes.IsHighlighter);
            Assert.False(host.Window.HighlighterToggle.IsChecked);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Theory]
    [InlineData(InkCanvasNextMode.EraseStroke)]
    [InlineData(InkCanvasNextMode.EraseArea)]
    [InlineData(InkCanvasNextMode.Select)]
    [InlineData(InkCanvasNextMode.Line)]
    [InlineData(InkCanvasNextMode.Circle)]
    public void Tool_EnterExitHighlighter_FallsBackToInk(InkCanvasNextMode tool)
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, tool);

            ToolDriver.Toggle(host.Window.HighlighterToggle, true);
            Assert.True(host.Canvas.DrawingAttributes.DrawingAttributes.IsHighlighter);
            ToolDriver.Toggle(host.Window.HighlighterToggle, false);

            Assert.Equal(InkCanvasNextMode.Ink, host.Canvas.DrawingAttributes.Mode);
            Assert.False(host.Canvas.DrawingAttributes.DrawingAttributes.IsHighlighter);
            Assert.False(host.Window.LineRadio.IsChecked);
            Assert.False(host.Window.CircleRadio.IsChecked);
            Assert.False(host.Window.EraseStrokeRadio.IsChecked);
            Assert.False(host.Window.EraseAreaRadio.IsChecked);
            Assert.False(host.Window.SelectRadio.IsChecked);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Fact]
    public void ReclickHighlighter_WhileActive_IsIdempotent( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, InkCanvasNextMode.Select);
            ToolDriver.Toggle(host.Window.HighlighterToggle, true);

            ToolDriver.Toggle(host.Window.HighlighterToggle, true);

            Assert.True(host.Canvas.DrawingAttributes.DrawingAttributes.IsHighlighter);
            ToolbarAssertions.AssertConsistent(host);

            ToolDriver.Toggle(host.Window.HighlighterToggle, false);

            Assert.False(host.Canvas.DrawingAttributes.DrawingAttributes.IsHighlighter);
            Assert.Equal(InkCanvasNextMode.Ink, host.Canvas.DrawingAttributes.Mode);
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

            Assert.Equal(InkCanvasNextMode.Ink, host.Canvas.DrawingAttributes.Mode);
            Assert.Equal(10, host.Canvas.DrawingAttributes.DrawingAttributes.Width);
            Assert.False(host.Canvas.DrawingAttributes.DrawingAttributes.IsHighlighter);
            ToolbarAssertions.AssertConsistent(host);
        });
    }
}