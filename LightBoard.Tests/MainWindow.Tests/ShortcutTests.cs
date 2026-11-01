using System.Windows.Input;
using System.Windows.Threading;

using InkCanvasNext;

namespace LightBoard.Tests.Toolbar;

/// <summary>窗口快捷键与工具栏命令的接线</summary>
public class ShortcutTests
{
    [Fact]
    public void DeleteKey_DeletesSelection( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, InkCanvasNextMode.Select);
            var canvas = host.Canvas;
            canvas.Strokes.Add(TestStrokes.MakeStroke( ));
            TestSelection.ForceSelection(canvas, canvas.Strokes);

            ToolDriver.KeyDown(host.Window, Key.Delete);

            Assert.Empty(canvas.Strokes);
        });
    }

    [Fact]
    public void UndoButton_FollowsCanUndo( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            Assert.False(host.Window.UndoButton.IsEnabled);

            host.Canvas.Strokes.Add(TestStrokes.MakeStroke( ));
            Dispatcher.CurrentDispatcher.Invoke(( ) => { }, DispatcherPriority.Background);

            Assert.True(host.Window.UndoButton.IsEnabled);
        });
    }
}
