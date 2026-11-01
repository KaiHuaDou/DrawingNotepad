using System.Windows.Controls.Primitives;
using System.Windows.Input;

using InkCanvasNext;

namespace LightBoard;

public partial class MainWindow
{
    // 快捷键挂在窗口上并指向画布：键盘输入不依赖画布是否拿到焦点
    private void InitializeCommands( )
    {
        AddKeyBinding(InkCanvasNextCommands.Undo, Key.Z, ModifierKeys.Control);
        AddKeyBinding(InkCanvasNextCommands.Redo, Key.Y, ModifierKeys.Control);
        AddKeyBinding(InkCanvasNextCommands.Redo, Key.Z, ModifierKeys.Control | ModifierKeys.Shift);
        AddKeyBinding(InkCanvasNextCommands.Delete, Key.Delete, ModifierKeys.None);
        AddKeyBinding(InkCanvasNextCommands.Copy, Key.C, ModifierKeys.Control);
        AddKeyBinding(InkCanvasNextCommands.Cut, Key.X, ModifierKeys.Control);
        AddKeyBinding(InkCanvasNextCommands.Paste, Key.V, ModifierKeys.Control);
        AddKeyBinding(InkCanvasNextCommands.Cancel, Key.Escape, ModifierKeys.None);
        AddKeyBinding(InkCanvasNextCommands.ZoomIn, Key.OemPlus, ModifierKeys.Control);
        AddKeyBinding(InkCanvasNextCommands.ZoomIn, Key.Add, ModifierKeys.Control);
        AddKeyBinding(InkCanvasNextCommands.ZoomOut, Key.OemMinus, ModifierKeys.Control);
        AddKeyBinding(InkCanvasNextCommands.ZoomOut, Key.Subtract, ModifierKeys.Control);
        AddKeyBinding(InkCanvasNextCommands.ActualSize, Key.D0, ModifierKeys.Control);
        AddKeyBinding(InkCanvasNextCommands.ActualSize, Key.NumPad0, ModifierKeys.Control);

        BindCommand(UndoButton, InkCanvasNextCommands.Undo);
        BindCommand(RedoButton, InkCanvasNextCommands.Redo);
        BindCommand(CopyButton, InkCanvasNextCommands.Copy);
        BindCommand(CutButton, InkCanvasNextCommands.Cut);
        BindCommand(DeleteButton, InkCanvasNextCommands.Delete);

        CommandManager.AddPreviewExecutedHandler(this, MainWindowPreviewExecuted);
    }

    private void AddKeyBinding(RoutedCommand command, Key key, ModifierKeys modifiers)
    {
        InputBindings.Add(new KeyBinding(command, key, modifiers) { CommandTarget = CanvasNext });
    }

    // 工具栏按钮与画布不在同一条路由上，不指定目标就找不到画布的命令绑定
    private void BindCommand(ButtonBase button, RoutedCommand command)
    {
        // 先给目标再给命令：命令赋值时就会查一次 CanExecute，此时目标必须已就位
        button.CommandTarget = CanvasNext;
        button.Command = command;
    }

    // 删除与剪切只做画布动作，执行前由窗口退出盖章
    private void MainWindowPreviewExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Command == InkCanvasNextCommands.Delete || e.Command == InkCanvasNextCommands.Cut)
        {
            ExitStamp( );
        }
    }
}
