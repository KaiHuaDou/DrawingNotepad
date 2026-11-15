using System.Windows.Ink;

using InkCanvasNext;

namespace LightBoard.Tests.Toolbar;

[Collection("Toolbar")]
public class ToolbarAutoRevertTests
{
    [Fact]
    public void Highlighter_ReachesCount_ReturnsToPen( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            host.Window.AutoRevert = true;
            host.Window.AutoRevertCount = 1;
            ToolDriver.Toggle(host.Window.HighlighterToggle, true);
            Assert.True(host.Window.HighlighterActive);

            host.Canvas.Strokes.Add(TestStrokes.MakeStroke( ));

            Assert.False(host.Window.HighlighterActive);
            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);
            Assert.Equal(InkCanvasNextMode.Ink, host.Canvas.Mode);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Fact]
    public void Highlighter_NotReached_Stays( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            host.Window.AutoRevert = true;
            host.Window.AutoRevertCount = 2;
            ToolDriver.Toggle(host.Window.HighlighterToggle, true);

            host.Canvas.Strokes.Add(TestStrokes.MakeStroke( ));

            Assert.True(host.Window.HighlighterActive);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Fact]
    public void SecondStroke_Reverts( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            host.Window.AutoRevert = true;
            host.Window.AutoRevertCount = 2;
            ToolDriver.Toggle(host.Window.HighlighterToggle, true);

            host.Canvas.Strokes.Add(TestStrokes.MakeStroke( ));
            host.Canvas.Strokes.Add(TestStrokes.MakeStroke( ));

            Assert.False(host.Window.HighlighterActive);
            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);
        });
    }

    [Fact]
    public void ToolSwitch_ResetsCount( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            host.Window.AutoRevert = true;
            host.Window.AutoRevertCount = 2;
            ToolDriver.Toggle(host.Window.HighlighterToggle, true);
            host.Canvas.Strokes.Add(TestStrokes.MakeStroke( ));

            ToolEntry.Enter(host, InkCanvasNextMode.EraseArea);
            ToolDriver.Toggle(host.Window.HighlighterToggle, true);
            host.Canvas.Strokes.Add(TestStrokes.MakeStroke( ));

            Assert.True(host.Window.HighlighterActive);
        });
    }

    [Fact]
    public void Disabled_NeverReverts( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            host.Window.AutoRevert = false;
            ToolDriver.Toggle(host.Window.HighlighterToggle, true);

            for (var i = 0; i < 3; i++)
            {
                host.Canvas.Strokes.Add(TestStrokes.MakeStroke( ));
            }

            Assert.True(host.Window.HighlighterActive);
            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);
        });
    }

    [Fact]
    public void Selection_ClearedAfterUse_ReturnsToPen( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            host.Window.AutoRevert = true;
            host.Window.AutoRevertCount = 1;
            ToolEntry.Enter(host, InkCanvasNextMode.Select);

            var stroke = TestStrokes.MakeStroke( );
            host.Canvas.Strokes.Add(stroke);
            TestSelection.ForceSelection(host.Canvas, [stroke]);
            host.Canvas.DeleteSelected( );

            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);
            Assert.False(host.Window.SelectRadio.IsChecked);
        });
    }

    [Fact]
    public void Selection_WithoutUse_DoesNotCount( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            host.Window.AutoRevert = true;
            host.Window.AutoRevertCount = 1;

            ToolEntry.Enter(host, InkCanvasNextMode.Select);
            ToolEntry.Enter(host, InkCanvasNextMode.EraseArea);
            ToolEntry.Enter(host, InkCanvasNextMode.Select);

            Assert.Equal(InkCanvasNextMode.Select, host.Window.Mode);
            Assert.True(host.Window.SelectRadio.IsChecked);
        });
    }

    [Fact]
    public void Eraser_CountsDown_ThenReverts( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            host.Window.AutoRevert = true;
            host.Window.AutoRevertCount = 3;
            host.Window.ShowRevertCount = true;

            ToolEntry.Enter(host, InkCanvasNextMode.EraseArea);

            Assert.Equal("3", host.Canvas.EraserLabel);

            host.Window.CountToolOperation( );
            host.Window.CountToolOperation( );

            Assert.Equal("1", host.Canvas.EraserLabel);
            Assert.Equal(InkCanvasNextMode.EraseArea, host.Window.Mode);

            host.Window.CountToolOperation( );

            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);
            Assert.Null(host.Canvas.EraserLabel);
        });
    }

    [Fact]
    public void Count_HiddenForOtherTools( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            host.Window.AutoRevert = true;
            host.Window.AutoRevertCount = 3;
            host.Window.ShowRevertCount = true;

            ToolEntry.Enter(host, InkCanvasNextMode.EraseArea);
            Assert.Equal("3", host.Canvas.EraserLabel);

            ToolEntry.Enter(host, InkCanvasNextMode.Select);

            Assert.Null(host.Canvas.EraserLabel);
        });
    }

    [Fact]
    public void Count_HiddenWhenShowDisabled( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            host.Window.AutoRevert = true;
            host.Window.AutoRevertCount = 2;
            host.Window.ShowRevertCount = false;

            ToolEntry.Enter(host, InkCanvasNextMode.EraseArea);

            Assert.Null(host.Canvas.EraserLabel);

            host.Window.CountToolOperation( );
            host.Window.CountToolOperation( );

            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);
        });
    }
}
