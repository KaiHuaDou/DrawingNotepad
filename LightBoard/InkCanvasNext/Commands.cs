using System.Windows;
using System.Windows.Input;

namespace InkCanvasNext;

/// <summary>
/// 控件的默认命令：构造期注册绑定，<see cref="InkCanvasNext.UseDefaultCommands"/> 为 true 时由 CanExecute 启用。
/// </summary>
public static class InkCanvasNextCommands
{
    public static readonly RoutedUICommand Undo = new(
        "撤销",
        nameof(Undo),
        typeof(InkCanvasNextCommands),
        [new KeyGesture(Key.Z, ModifierKeys.Control)]);

    public static readonly RoutedUICommand Redo = new(
        "重做",
        nameof(Redo),
        typeof(InkCanvasNextCommands),
        [
            new KeyGesture(Key.Y, ModifierKeys.Control),
            new KeyGesture(Key.Z, ModifierKeys.Control | ModifierKeys.Shift)
        ]);

    public static readonly RoutedUICommand Delete = new(
        "删除",
        nameof(Delete),
        typeof(InkCanvasNextCommands),
        [new KeyGesture(Key.Delete)]);

    public static readonly RoutedUICommand Copy = new(
        "复制",
        nameof(Copy),
        typeof(InkCanvasNextCommands),
        [new KeyGesture(Key.C, ModifierKeys.Control)]);

    public static readonly RoutedUICommand Cut = new(
        "剪切",
        nameof(Cut),
        typeof(InkCanvasNextCommands),
        [new KeyGesture(Key.X, ModifierKeys.Control)]);

    public static readonly RoutedUICommand Paste = new(
        "粘贴",
        nameof(Paste),
        typeof(InkCanvasNextCommands),
        [new KeyGesture(Key.V, ModifierKeys.Control)]);

    public static readonly RoutedUICommand Cancel = new(
        "取消",
        nameof(Cancel),
        typeof(InkCanvasNextCommands),
        [new KeyGesture(Key.Escape)]);
}

public partial class InkCanvasNext
{
    private void InitializeCommands( )
    {
        CommandBindings.Add(new CommandBinding(InkCanvasNextCommands.Undo, UndoExecuted, UndoCanExecute));
        CommandBindings.Add(new CommandBinding(InkCanvasNextCommands.Redo, RedoExecuted, RedoCanExecute));
        CommandBindings.Add(new CommandBinding(InkCanvasNextCommands.Delete, DeleteExecuted, DeleteCanExecute));
        CommandBindings.Add(new CommandBinding(InkCanvasNextCommands.Copy, CopyExecuted, CopyCanExecute));
        CommandBindings.Add(new CommandBinding(InkCanvasNextCommands.Cut, CutExecuted, CutCanExecute));
        CommandBindings.Add(new CommandBinding(InkCanvasNextCommands.Paste, PasteExecuted, PasteCanExecute));
        CommandBindings.Add(new CommandBinding(InkCanvasNextCommands.Cancel, CancelExecuted, CancelCanExecute));
    }

    private void UndoExecuted(object o, ExecutedRoutedEventArgs e)
    {
        Undo( );
        e.Handled = true;
    }

    private void UndoCanExecute(object o, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = UseDefaultCommands && CanUndo && !IsReadOnly;
    }

    private void RedoExecuted(object o, ExecutedRoutedEventArgs e)
    {
        Redo( );
        e.Handled = true;
    }

    private void RedoCanExecute(object o, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = UseDefaultCommands && CanRedo && !IsReadOnly;
    }

    private void DeleteExecuted(object o, ExecutedRoutedEventArgs e)
    {
        DeleteSelected( );
        e.Handled = true;
    }

    private void DeleteCanExecute(object o, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = UseDefaultCommands && HasSelection && !IsReadOnly;
    }

    private void CopyExecuted(object o, ExecutedRoutedEventArgs e)
    {
        e.Handled = CopySelected( );
    }

    private void CopyCanExecute(object o, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = UseDefaultCommands && HasSelection;
    }

    private void CutExecuted(object o, ExecutedRoutedEventArgs e)
    {
        e.Handled = CutSelected( );
    }

    private void CutCanExecute(object o, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = UseDefaultCommands && HasSelection && !IsReadOnly;
    }

    private void PasteExecuted(object o, ExecutedRoutedEventArgs e)
    {
        var viewport = ViewportSize;
        var center = CurrentView.ToContent(new Point(viewport.Width / 2, viewport.Height / 2));
        e.Handled = StampPasteAt(center);
    }

    private void PasteCanExecute(object o, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = UseDefaultCommands && !IsReadOnly;
    }

    private void CancelExecuted(object o, ExecutedRoutedEventArgs e)
    {
        ResetTouchState( );
        e.Handled = true;
    }

    private void CancelCanExecute(object o, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = UseDefaultCommands;
    }
}
