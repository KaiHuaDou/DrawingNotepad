using InkCanvasNext;

namespace LightBoard;

// 工具自动回退：橡皮、荧光笔、选择完成设定的操作次数后回到画笔
public partial class MainWindow
{
    private (bool Highlighter, InkCanvasNextMode Mode, StampAction Stamp) toolIdentity;
    private int toolOperationCount;
    private bool selectionUsed;

    /// <summary>
    /// 橡皮、荧光笔、选择用完是否自动回到画笔；构造期取自设置
    /// </summary>
    internal bool AutoRevert { get; set; }

    /// <summary>
    /// 自动回退前的操作次数；构造期取自设置
    /// </summary>
    internal int AutoRevertCount { get; set; }

    /// <summary>
    /// 是否在橡皮圈上显示剩余次数；构造期取自设置
    /// </summary>
    internal bool ShowRevertCount { get; set; }

    private void CanvasOperationCompleted(object? o, InkCanvasOperationEventArgs e)
    {
        if (e.Operation == InkCanvasOperation.Stroke && HighlighterActive)
        {
            CountToolOperation( );
        }
        else if (e.Operation == InkCanvasOperation.Erase && Mode is InkCanvasNextMode.EraseStroke or InkCanvasNextMode.EraseArea)
        {
            CountToolOperation( );
        }
    }

    /// <summary>
    /// 工具身份变化时清零：换工具、进出荧光笔、进出印章都从零开始计数
    /// </summary>
    private void TrackToolIdentity( )
    {
        var identity = (HighlighterActive, Mode, stamp);

        if (identity == toolIdentity)
        {
            return;
        }

        toolIdentity = identity;
        toolOperationCount = 0;
        selectionUsed = false;
    }

    // 选择按会话计数：选区建立不算，清空才算
    private void TrackSelectionSession( )
    {
        if (Mode != InkCanvasNextMode.Select || stamp != StampAction.None)
        {
            selectionUsed = false;
            return;
        }

        if (CanvasNext.HasSelection)
        {
            selectionUsed = true;
            return;
        }

        if (!selectionUsed)
        {
            return;
        }

        selectionUsed = false;
        CountToolOperation( );
    }

    /// <summary>
    /// 记一次工具操作；达到设定次数时回到画笔
    /// </summary>
    internal void CountToolOperation( )
    {
        if (!AutoRevert)
        {
            return;
        }

        if (++toolOperationCount < AutoRevertCount)
        {
            UpdateRevertLabel( );
            return;
        }

        RevertTool( );
    }

    private void RevertTool( )
    {
        HighlighterActive = false;
        Mode = InkCanvasNextMode.Ink;
        SyncToolState( );
    }

    // 剩余次数只在橡皮激活、启用自动回退与显示时出现，到 0 即回退并清空
    private void UpdateRevertLabel( )
    {
        var remaining = AutoRevertCount - toolOperationCount;
        var visible = AutoRevert
            && ShowRevertCount
            && Mode == InkCanvasNextMode.EraseArea
            && remaining > 0;

        CanvasNext.EraserLabel = visible ? remaining.ToString( ) : null;
    }
}
