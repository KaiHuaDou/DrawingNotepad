using System.Windows;
using System.Windows.Input;

namespace InkCanvasNext;

/// <summary>
/// 画布控件的默认命令：构造期注册绑定，可用性由画布状态决定
/// <see cref="InkCanvasNext.UseDefaultCommands"/> 只控制控件是否接管键盘焦点与自身快捷键
/// </summary>
public static class InkCanvasNextCommands
{
    /// <summary>
    /// 撤销上一步墨迹操作
    /// </summary>
    public static readonly RoutedUICommand Undo = new(
        "撤销",
        nameof(Undo),
        typeof(InkCanvasNextCommands),
        [new KeyGesture(Key.Z, ModifierKeys.Control)]);

    /// <summary>
    /// 重做已撤销的操作
    /// </summary>
    public static readonly RoutedUICommand Redo = new(
        "重做",
        nameof(Redo),
        typeof(InkCanvasNextCommands),
        [
            new KeyGesture(Key.Y, ModifierKeys.Control),
            new KeyGesture(Key.Z, ModifierKeys.Control | ModifierKeys.Shift)
        ]);

    /// <summary>
    /// 删除选中笔画
    /// </summary>
    public static readonly RoutedUICommand Delete = new(
        "删除",
        nameof(Delete),
        typeof(InkCanvasNextCommands),
        [new KeyGesture(Key.Delete)]);

    /// <summary>
    /// 复制选中笔画到剪贴板
    /// </summary>
    public static readonly RoutedUICommand Copy = new(
        "复制",
        nameof(Copy),
        typeof(InkCanvasNextCommands),
        [new KeyGesture(Key.C, ModifierKeys.Control)]);

    /// <summary>
    /// 剪切选中笔画到剪贴板并从画布删除
    /// </summary>
    public static readonly RoutedUICommand Cut = new(
        "剪切",
        nameof(Cut),
        typeof(InkCanvasNextCommands),
        [new KeyGesture(Key.X, ModifierKeys.Control)]);

    /// <summary>
    /// 粘贴剪贴板墨迹到视口中心
    /// </summary>
    public static readonly RoutedUICommand Paste = new(
        "粘贴",
        nameof(Paste),
        typeof(InkCanvasNextCommands),
        [new KeyGesture(Key.V, ModifierKeys.Control)]);

    /// <summary>
    /// 取消进行中的手势并回到静止状态
    /// </summary>
    public static readonly RoutedUICommand Cancel = new(
        "取消",
        nameof(Cancel),
        typeof(InkCanvasNextCommands),
        [new KeyGesture(Key.Escape)]);

    /// <summary>
    /// 以视口中心为锚点放大画布
    /// </summary>
    public static readonly RoutedUICommand ZoomIn = new(
        "放大",
        nameof(ZoomIn),
        typeof(InkCanvasNextCommands),
        [
            new KeyGesture(Key.OemPlus, ModifierKeys.Control),
            new KeyGesture(Key.Add, ModifierKeys.Control)
        ]);

    /// <summary>
    /// 以视口中心为锚点缩小画布
    /// </summary>
    public static readonly RoutedUICommand ZoomOut = new(
        "缩小",
        nameof(ZoomOut),
        typeof(InkCanvasNextCommands),
        [
            new KeyGesture(Key.OemMinus, ModifierKeys.Control),
            new KeyGesture(Key.Subtract, ModifierKeys.Control)
        ]);

    /// <summary>
    /// 缩放到 100%
    /// </summary>
    public static readonly RoutedUICommand ActualSize = new(
        "实际大小",
        nameof(ActualSize),
        typeof(InkCanvasNextCommands),
        [
            new KeyGesture(Key.D0, ModifierKeys.Control),
            new KeyGesture(Key.NumPad0, ModifierKeys.Control)
        ]);
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
        CommandBindings.Add(new CommandBinding(InkCanvasNextCommands.Cancel, CancelExecuted));
        CommandBindings.Add(new CommandBinding(InkCanvasNextCommands.ZoomIn, ZoomInExecuted));
        CommandBindings.Add(new CommandBinding(InkCanvasNextCommands.ZoomOut, ZoomOutExecuted));
        CommandBindings.Add(new CommandBinding(InkCanvasNextCommands.ActualSize, ActualSizeExecuted));
    }

    private void UndoExecuted(object o, ExecutedRoutedEventArgs e)
    {
        Undo( );
        e.Handled = true;
    }

    private void UndoCanExecute(object o, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = CanUndo && !IsReadOnly;
    }

    private void RedoExecuted(object o, ExecutedRoutedEventArgs e)
    {
        Redo( );
        e.Handled = true;
    }

    private void RedoCanExecute(object o, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = CanRedo && !IsReadOnly;
    }

    private void DeleteExecuted(object o, ExecutedRoutedEventArgs e)
    {
        DeleteSelected( );
        e.Handled = true;
    }

    private void DeleteCanExecute(object o, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = HasSelection && !IsReadOnly;
    }

    private void CopyExecuted(object o, ExecutedRoutedEventArgs e)
    {
        e.Handled = CopySelected( );
    }

    private void CopyCanExecute(object o, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = HasSelection;
    }

    private void CutExecuted(object o, ExecutedRoutedEventArgs e)
    {
        e.Handled = CutSelected( );
    }

    private void CutCanExecute(object o, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = HasSelection && !IsReadOnly;
    }

    private void PasteExecuted(object o, ExecutedRoutedEventArgs e)
    {
        var viewport = ViewportSize;
        var center = CurrentView.ToContent(new Point(viewport.Width / 2, viewport.Height / 2));
        e.Handled = StampPasteAt(center);
    }

    private void PasteCanExecute(object o, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = !IsReadOnly;
    }

    private void CancelExecuted(object o, ExecutedRoutedEventArgs e)
    {
        ResetTouchState( );
        e.Handled = true;
    }

    private void ZoomInExecuted(object o, ExecutedRoutedEventArgs e)
    {
        ZoomTo(CurrentView.Scale * WheelZoomFactor);
        e.Handled = true;
    }

    private void ZoomOutExecuted(object o, ExecutedRoutedEventArgs e)
    {
        ZoomTo(CurrentView.Scale / WheelZoomFactor);
        e.Handled = true;
    }

    private void ActualSizeExecuted(object o, ExecutedRoutedEventArgs e)
    {
        ZoomTo(1.0);
        e.Handled = true;
    }
}
