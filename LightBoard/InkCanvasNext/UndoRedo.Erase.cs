using System.Collections.Generic;
using System.Windows.Ink;

namespace InkCanvasNext;

// 擦除记账：触摸擦除周期与手写笔原生擦除共用同一条开口批次
public partial class InkCanvasNext
{
    /// <summary>
    /// 擦除周期内持续追加的历史项，非空表示批次还开着
    /// </summary>
    private StrokeChanges? eraseBatch;

    private bool nativeEraseActive;
    private bool nativeEraseChanged;
    private int nativeEraseGeneration;

    internal int BeginNativeErase( )
    {
        nativeEraseGeneration++;
        nativeEraseActive = true;
        nativeEraseChanged = false;
        return nativeEraseGeneration;
    }

    internal bool EndNativeErase(int generation)
    {
        if (generation != nativeEraseGeneration)
        {
            return false;
        }

        var changed = nativeEraseChanged;
        nativeEraseActive = false;
        nativeEraseChanged = false;
        SealEraseBatch( );
        return changed;
    }

    internal void CompleteNativeErase(int generation)
    {
        if (EndNativeErase(generation))
        {
            RaiseOperationCompleted(InkCanvasOperation.Erase);
        }
    }

    /// <summary>
    /// 批次留在历史里，之后的擦除开新项
    /// </summary>
    private void SealEraseBatch( )
    {
        eraseBatch = null;
    }

    private void TrackEraseChange(IReadOnlyList<Stroke> added, IReadOnlyList<Stroke> removed)
    {
        var batch = eraseBatch ??= OpenEraseBatch( );

        foreach (var stroke in removed)
        {
            batch.TrackRemoved(stroke);
        }

        foreach (var stroke in added)
        {
            batch.TrackAdded(stroke);
        }
    }

    private StrokeChanges OpenEraseBatch( )
    {
        var batch = new StrokeChanges([], []);
        PushChange(batch);
        return batch;
    }

    private void ResetNativeErase( )
    {
        nativeEraseGeneration++;
        nativeEraseActive = false;
        nativeEraseChanged = false;
    }
}
