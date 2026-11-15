using System.Collections.Generic;

namespace InkCanvasNext;

public sealed class HistorySnapshot
{
    internal HistorySnapshot(IHistoryChange[] changes, int position)
    {
        Changes = changes;
        Position = position;
    }

    internal IReadOnlyList<IHistoryChange> Changes { get; }

    /// <summary>
    /// 历史位置：之前的条目为可撤销项，之后的条目为可重做项
    /// </summary>
    public int Position { get; }
}
