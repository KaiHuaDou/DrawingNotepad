using System.Windows.Input;

namespace InkCanvasNext.Tests;

/// <summary>内置命令的启用开关与可执行状态</summary>
public class CommandTests
{
    [Fact]
    public void Commands_CanExecuteFollowsCanvasState( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            Assert.False(InkCanvasNextCommands.Undo.CanExecute(null, host.Canvas));

            host.Canvas.Strokes.Add(MakeStroke( ));

            Assert.True(InkCanvasNextCommands.Undo.CanExecute(null, host.Canvas));
            Assert.False(host.Canvas.Focusable);
        });
    }

    [Fact]
    public void ZoomCommands_ChangeScale( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var canvas = host.Canvas;

            InkCanvasNextCommands.ZoomIn.Execute(null, canvas);
            Assert.True(canvas.CurrentScale > 1);

            InkCanvasNextCommands.ZoomOut.Execute(null, canvas);
            Assert.Equal(1.0, canvas.CurrentScale, 9);

            InkCanvasNextCommands.ZoomOut.Execute(null, canvas);
            Assert.True(canvas.CurrentScale < 1);

            InkCanvasNextCommands.ActualSize.Execute(null, canvas);
            Assert.Equal(1.0, canvas.CurrentScale, 9);
        });
    }

    [Fact]
    public void Commands_Enabled_UndoCanExecuteAndExecute( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Strokes.Add(MakeStroke( ));
            host.Canvas.UseDefaultCommands = true;

            Assert.True(host.Canvas.Focusable);
            Assert.True(InkCanvasNextCommands.Undo.CanExecute(null, host.Canvas));

            InkCanvasNextCommands.Undo.Execute(null, host.Canvas);

            Assert.Empty(host.Canvas.Strokes);
        });
    }

    [Fact]
    public void ReadOnly_DisablesEditingCommands( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            host.Canvas.Strokes.Add(MakeStroke( ));
            host.Canvas.UseDefaultCommands = true;
            host.Canvas.IsReadOnly = true;

            Assert.False(InkCanvasNextCommands.Undo.CanExecute(null, host.Canvas));
            Assert.False(InkCanvasNextCommands.Paste.CanExecute(null, host.Canvas));
        });
    }

    private static System.Windows.Ink.Stroke MakeStroke( )
    {
        return new System.Windows.Ink.Stroke(
            [new StylusPoint(100, 100), new StylusPoint(200, 100)],
            new System.Windows.Ink.DrawingAttributes
            {
                Color = System.Windows.Media.Colors.Black,
                Width = 3,
                Height = 3
            });
    }
}
