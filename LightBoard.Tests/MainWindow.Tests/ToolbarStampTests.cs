using System.IO;
using System.Windows;
using System.Windows.Ink;

using InkCanvasNext;

namespace LightBoard.Tests.Toolbar;

[Collection("Toolbar")]
public class ToolbarStampTests
{
    [Fact]
    public void Clone_NoSelection_StaysUnchecked( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );

            ToolDriver.Toggle(host.Window.CloneButton, true);

            Assert.False(host.Window.CloneButton.IsChecked);
            Assert.Equal(StampAction.None, host.Canvas.StampAction);
        });
    }

    [Fact]
    public void Clone_WithSelection_ActivatesStampAndClearsPaste( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, InkCanvasNextMode.Select);
            var canvas = host.Canvas;
            canvas.Strokes.Add(TestStrokes.MakeStroke( ));
            TestSelection.ForceSelection(canvas, canvas.Strokes);
            Assert.True(canvas.HasSelection);

            ToolDriver.Toggle(host.Window.CloneButton, true);

            Assert.Equal(StampAction.Clone, canvas.StampAction);
            Assert.True(host.Window.CloneButton.IsChecked);
            Assert.False(host.Window.PasteButton.IsChecked);
            ToolbarAssertions.AssertConsistent(host, StampAction.Clone);
        });
    }

    [Fact]
    public void Paste_NoClipboardInk_StaysUnchecked( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            Clipboard.Clear( );

            ToolDriver.Toggle(host.Window.PasteButton, true);

            Assert.False(host.Window.PasteButton.IsChecked);
            Assert.Equal(StampAction.None, host.Canvas.StampAction);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Fact]
    public void Paste_WithClipboardInk_ActivatesStampAndClearsClone( )
    {
        UiThread.Run(( ) =>
        {
            PutInkOnClipboard( );
            using var host = new MainWindowHost( );

            ToolDriver.Toggle(host.Window.PasteButton, true);

            Assert.Equal(StampAction.Paste, host.Canvas.StampAction);
            Assert.True(host.Window.PasteButton.IsChecked);
            Assert.False(host.Window.CloneButton.IsChecked);
            ToolbarAssertions.AssertConsistent(host, StampAction.Paste);
        });
    }

    [Fact]
    public void ToggleOffActivePasteStamp_ExitsStamp( )
    {
        UiThread.Run(( ) =>
        {
            PutInkOnClipboard( );
            using var host = new MainWindowHost( );
            ToolDriver.Toggle(host.Window.PasteButton, true);
            Assert.Equal(StampAction.Paste, host.Canvas.StampAction);

            ToolDriver.Toggle(host.Window.PasteButton, false);

            Assert.Equal(StampAction.None, host.Canvas.StampAction);
            Assert.False(host.Window.PasteButton.IsChecked);
            Assert.False(host.Window.CloneButton.IsChecked);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Fact]
    public void ToggleOffActiveCloneStamp_ExitsStamp( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ActivateClone(host);

            ToolDriver.Toggle(host.Window.CloneButton, false);

            Assert.Equal(StampAction.None, host.Canvas.StampAction);
            Assert.False(host.Window.CloneButton.IsChecked);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Fact]
    public void Escape_ClearsActiveStamp( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ActivateClone(host);

            ToolDriver.Esc(host.Window);

            Assert.Equal(StampAction.None, host.Canvas.StampAction);
            Assert.False(host.Window.CloneButton.IsChecked);
            Assert.False(host.Window.PasteButton.IsChecked);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Fact]
    public void Cut_ClearsStampAndDeletesStrokes( )
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

            ToolDriver.Click(host.FindButtonByTag("\uE8C6"));

            Assert.Equal(StampAction.None, canvas.StampAction);
            Assert.Empty(canvas.Strokes);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Fact]
    public void CloneStamp_EnterEraseTool_ClearsStamp( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ActivateClone(host);

            ToolDriver.ClickRadio(host, host.Window.EraseStrokeRadio);

            Assert.Equal(StampAction.None, host.Canvas.StampAction);
            Assert.False(host.Window.CloneButton.IsChecked);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Fact]
    public void CloneStamp_EnterLineShape_ClearsStamp( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ActivateClone(host);

            ToolDriver.ClickRadio(host, host.Window.LineRadio);

            Assert.Equal(StampAction.None, host.Canvas.StampAction);
            Assert.False(host.Window.CloneButton.IsChecked);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Fact]
    public void CloneStamp_EnterHighlighter_ClearsStamp( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ActivateClone(host);

            ToolDriver.Toggle(host.Window.HighlighterToggle, true);

            Assert.Equal(StampAction.None, host.Canvas.StampAction);
            Assert.False(host.Window.CloneButton.IsChecked);
            ToolbarAssertions.AssertConsistent(host, StampAction.None);
        });
    }

    [Fact]
    public void Paste_FromToolMode_ExitsToolToInk( )
    {
        UiThread.Run(( ) =>
        {
            PutInkOnClipboard( );
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, InkCanvasNextMode.EraseStroke);

            ToolDriver.Toggle(host.Window.PasteButton, true);

            Assert.Equal(StampAction.Paste, host.Canvas.StampAction);
            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);
            Assert.False(host.Window.EraseStrokeRadio.IsChecked);
            ToolbarAssertions.AssertConsistent(host, StampAction.Paste);
        });
    }

    [Fact]
    public void Paste_FromShape_ExitsShapeToInk( )
    {
        UiThread.Run(( ) =>
        {
            PutInkOnClipboard( );
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, InkCanvasNextMode.Line);

            ToolDriver.Toggle(host.Window.PasteButton, true);

            Assert.Equal(StampAction.Paste, host.Canvas.StampAction);
            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);
            Assert.False(host.Window.LineRadio.IsChecked);
            ToolbarAssertions.AssertConsistent(host, StampAction.Paste);
        });
    }

    [Fact]
    public void Paste_FromHighlighter_ExitsToInk( )
    {
        UiThread.Run(( ) =>
        {
            PutInkOnClipboard( );
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, InkCanvasNextMode.Highlighter);

            ToolDriver.Toggle(host.Window.PasteButton, true);

            Assert.Equal(StampAction.Paste, host.Canvas.StampAction);
            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);
            Assert.False(host.Window.HighlighterToggle.IsChecked);
            Assert.False(host.Canvas.DefaultDrawingAttributes.IsHighlighter);
            ToolbarAssertions.AssertConsistent(host, StampAction.Paste);
        });
    }

    [Fact]
    public void Paste_FromTool_ToggleOff_RestoresInk( )
    {
        UiThread.Run(( ) =>
        {
            PutInkOnClipboard( );
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, InkCanvasNextMode.EraseStroke);
            ToolDriver.Toggle(host.Window.PasteButton, true);
            Assert.Equal(StampAction.Paste, host.Canvas.StampAction);
            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);

            ToolDriver.Toggle(host.Window.PasteButton, false);

            Assert.Equal(StampAction.None, host.Canvas.StampAction);
            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);
            Assert.False(host.Window.PasteButton.IsChecked);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Fact]
    public void Paste_NoInkClipboardFromShape_KeepsMode( )
    {
        UiThread.Run(( ) =>
        {
            Clipboard.Clear( );
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, InkCanvasNextMode.Line);

            ToolDriver.Toggle(host.Window.PasteButton, true);

            Assert.False(host.Window.PasteButton.IsChecked);
            Assert.Equal(StampAction.None, host.Canvas.StampAction);
            Assert.Equal(InkCanvasNextMode.Line, host.Window.Mode);
            Assert.True(host.Window.LineRadio.IsChecked);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    private static void ActivateClone(MainWindowHost host)
    {
        ToolEntry.Enter(host, InkCanvasNextMode.Select);
        var canvas = host.Canvas;
        canvas.Strokes.Add(TestStrokes.MakeStroke( ));
        TestSelection.ForceSelection(canvas, canvas.Strokes);
        ToolDriver.Toggle(host.Window.CloneButton, true);
        Assert.Equal(StampAction.Clone, canvas.StampAction);
    }

    private static void PutInkOnClipboard( )
    {
        var strokes = new StrokeCollection { TestStrokes.MakeStroke( ) };
        using var stream = new MemoryStream( );
        strokes.Save(stream, false);
        var data = new DataObject(StrokeCollection.InkSerializedFormat, stream);
        Clipboard.SetDataObject(data, true);
    }
}
